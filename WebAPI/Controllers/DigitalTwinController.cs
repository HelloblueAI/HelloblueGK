using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.AI;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace HB_NLP_Research_Lab.WebAPI.Controllers
{
    /// <summary>
    /// Controller for managing engine digital twins
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Tags("Digital Twins")]
    public class DigitalTwinController : ControllerBase
    {
        private readonly HelloblueGKDbContext _context;
        private readonly DigitalTwinEngine _digitalTwinEngine;
        private readonly ILogger<DigitalTwinController> _logger;

        public DigitalTwinController(
            HelloblueGKDbContext context,
            DigitalTwinEngine digitalTwinEngine,
            ILogger<DigitalTwinController> logger)
        {
            _context = context;
            _digitalTwinEngine = digitalTwinEngine;
            _logger = logger;
        }

        /// <summary>
        /// Get all digital twins
        /// </summary>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<DigitalTwin>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllDigitalTwins([FromQuery] int? engineId = null)
        {
            try
            {
                var query = _context.DigitalTwins
                    .Include(dt => dt.Engine)
                    .AsQueryable();

                if (!ApplyCurrentUserFilter(ref query))
                {
                    return Forbid();
                }

                if (engineId.HasValue)
                {
                    query = query.Where(dt => dt.EngineId == engineId.Value);
                }

                var digitalTwins = await query
                    .OrderByDescending(dt => dt.CreatedAt)
                    .ToListAsync();

                return Ok(digitalTwins);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving digital twins");
                return StatusCode(500, "An error occurred while retrieving digital twins");
            }
        }

        /// <summary>
        /// Get digital twin by ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(DigitalTwin), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDigitalTwinById(int id)
        {
            try
            {
                var digitalTwin = await _context.DigitalTwins
                    .Include(dt => dt.Engine)
                    .FirstOrDefaultAsync(dt => dt.Id == id);

                if (digitalTwin == null)
                {
                    return NotFound(new { message = $"Digital twin with ID {id} not found" });
                }

                if (!CurrentUserCanAccessDigitalTwin(digitalTwin))
                {
                    return Forbid();
                }

                return Ok(digitalTwin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving digital twin {DigitalTwinId}", id);
                return StatusCode(500, "An error occurred while retrieving the digital twin");
            }
        }

        /// <summary>
        /// Create a new digital twin for an engine
        /// </summary>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(DigitalTwin), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateDigitalTwin([FromBody] CreateDigitalTwinRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validate engine exists
                var engine = await _context.Engines.FindAsync(request.EngineId);
                if (engine == null)
                {
                    return NotFound(new { message = $"Engine with ID {request.EngineId} not found" });
                }

                // Check if digital twin already exists for this engine
                var currentUsername = GetCurrentUsername();
                if (!User.IsInRole("Admin") && string.IsNullOrWhiteSpace(currentUsername))
                {
                    return Forbid();
                }

                var existingTwinQuery = _context.DigitalTwins
                    .Where(dt => dt.EngineId == request.EngineId && dt.IsActive);

                if (!User.IsInRole("Admin"))
                {
                    existingTwinQuery = existingTwinQuery.Where(dt => dt.CreatedBy == currentUsername);
                }

                var existingTwin = await existingTwinQuery.FirstOrDefaultAsync();

                if (existingTwin != null && !request.ForceCreate)
                {
                    return Conflict(new { message = $"Active digital twin already exists for engine {request.EngineId}. Use forceCreate=true to create a new one." });
                }

                // Create engine model from engine data
                var engineModel = new EngineModel
                {
                    Name = engine.Name,
                    Parameters = new Dictionary<string, double>
                    {
                        ["Thrust"] = engine.Thrust,
                        ["SpecificImpulse"] = engine.SpecificImpulse,
                        ["ChamberPressure"] = engine.ChamberPressure,
                        ["Efficiency"] = engine.Efficiency,
                        ["ExpansionRatio"] = engine.ExpansionRatio,
                        ["MassFlowRate"] = engine.MassFlowRate
                    }
                };

                // Create digital twin using DigitalTwinEngine
                var engineId = $"Engine_{request.EngineId}";
                var digitalTwinResult = await _digitalTwinEngine.CreateDigitalTwinAsync(engineId, engineModel);

                // Create database record
                var digitalTwin = new DigitalTwin
                {
                    EngineId = request.EngineId,
                    Name = request.Name ?? $"{engine.Name} Digital Twin",
                    PredictionAccuracy = digitalTwinResult.PredictionAccuracy,
                    RealTimeLearning = request.RealTimeLearning,
                    ModelDataJson = JsonSerializer.Serialize(digitalTwinResult),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    CreatedBy = currentUsername
                };

                _context.DigitalTwins.Add(digitalTwin);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetDigitalTwinById), new { id = digitalTwin.Id }, digitalTwin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating digital twin");
                return StatusCode(500, "An error occurred while creating the digital twin");
            }
        }

        /// <summary>
        /// Update digital twin with new learning data
        /// </summary>
        [HttpPost("{id}/learn")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(DigitalTwin), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateDigitalTwinLearning(int id, [FromBody] LearningDataRequest request)
        {
            try
            {
                var digitalTwin = await _context.DigitalTwins
                    .Include(dt => dt.Engine)
                    .FirstOrDefaultAsync(dt => dt.Id == id);

                if (digitalTwin == null)
                {
                    return NotFound(new { message = $"Digital twin with ID {id} not found" });
                }

                if (!digitalTwin.RealTimeLearning)
                {
                    return BadRequest(new { message = "Real-time learning is not enabled for this digital twin" });
                }

                // Update digital twin with learning data
                var engineId = $"Engine_{digitalTwin.EngineId}";
                
                // Convert telemetry data to TestFlightData format
                var flightData = new TestFlightData
                {
                    EngineId = engineId,
                    FlightDate = DateTime.UtcNow,
                    FlightMetrics = request.TelemetryData ?? new Dictionary<string, double>()
                };
                
                var learningResult = await _digitalTwinEngine.LearnFromTestFlightAsync(engineId, flightData);

                // Update database record
                digitalTwin.LastUpdated = DateTime.UtcNow;
                digitalTwin.ModelImprovement = learningResult.ModelImprovement?.ImprovementPercentage;
                digitalTwin.TrainingIterations = (digitalTwin.TrainingIterations ?? 0) + 1;
                digitalTwin.PredictionAccuracy = learningResult.UpdatedPredictionAccuracy?.OverallAccuracy ?? digitalTwin.PredictionAccuracy;

                var modelData = JsonSerializer.Deserialize<Dictionary<string, object>>(digitalTwin.ModelDataJson ?? "{}") 
                    ?? new Dictionary<string, object>();
                modelData["lastLearningUpdate"] = DateTime.UtcNow;
                modelData["learningResult"] = learningResult;
                digitalTwin.ModelDataJson = JsonSerializer.Serialize(modelData);

                await _context.SaveChangesAsync();

                return Ok(digitalTwin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating digital twin learning {DigitalTwinId}", id);
                return StatusCode(500, "An error occurred while updating digital twin learning");
            }
        }

        /// <summary>
        /// Get digital twin predictions
        /// </summary>
        [HttpPost("{id}/predict")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPredictions(int id, [FromBody] PredictionRequest request)
        {
            try
            {
                var digitalTwin = await _context.DigitalTwins
                    .Include(dt => dt.Engine)
                    .FirstOrDefaultAsync(dt => dt.Id == id);

                if (digitalTwin == null)
                {
                    return NotFound(new { message = $"Digital twin with ID {id} not found" });
                }

                if (!CurrentUserCanAccessDigitalTwin(digitalTwin))
                {
                    return Forbid();
                }

                var engineId = $"Engine_{digitalTwin.EngineId}";
                
                // Create prediction scenario from parameters
                var scenario = new PredictionScenario
                {
                    Name = request.ScenarioName ?? "Default Prediction",
                    Parameters = (request.ScenarioParameters ?? new Dictionary<string, double>())
                        .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                };
                
                var predictions = await _digitalTwinEngine.PredictEngineBehaviorAsync(engineId, scenario);

                return Ok(new
                {
                    digitalTwinId = id,
                    engineId = digitalTwin.EngineId,
                    predictions = predictions,
                    predictionAccuracy = digitalTwin.PredictionAccuracy,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting predictions for digital twin {DigitalTwinId}", id);
                return StatusCode(500, "An error occurred while getting predictions");
            }
        }

        /// <summary>
        /// Deactivate a digital twin
        /// </summary>
        [HttpPost("{id}/deactivate")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeactivateDigitalTwin(int id)
        {
            try
            {
                var digitalTwin = await _context.DigitalTwins.FindAsync(id);
                if (digitalTwin == null)
                {
                    return NotFound(new { message = $"Digital twin with ID {id} not found" });
                }

                digitalTwin.IsActive = false;
                digitalTwin.LastUpdated = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Digital twin deactivated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating digital twin {DigitalTwinId}", id);
                return StatusCode(500, "An error occurred while deactivating the digital twin");
            }
        }

        private bool ApplyCurrentUserFilter(ref IQueryable<DigitalTwin> query)
        {
            if (User.IsInRole("Admin"))
            {
                return true;
            }

            var currentUsername = GetCurrentUsername();
            if (string.IsNullOrWhiteSpace(currentUsername))
            {
                return false;
            }

            query = query.Where(dt => dt.CreatedBy == currentUsername);
            return true;
        }

        private bool CurrentUserCanAccessDigitalTwin(DigitalTwin digitalTwin)
        {
            if (User.IsInRole("Admin"))
            {
                return true;
            }

            var currentUsername = GetCurrentUsername();
            return !string.IsNullOrWhiteSpace(currentUsername) &&
                string.Equals(digitalTwin.CreatedBy, currentUsername, StringComparison.OrdinalIgnoreCase);
        }

        private string? GetCurrentUsername()
        {
            return User.Identity?.Name
                ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst("username")?.Value;
        }
    }

    /// <summary>
    /// Request model for creating a digital twin
    /// </summary>
    public class CreateDigitalTwinRequest
    {
        /// <summary>
        /// Engine ID to create digital twin for
        /// </summary>
        [Required]
        public int EngineId { get; set; }

        /// <summary>
        /// Digital twin name
        /// </summary>
        [MaxLength(200)]
        public string? Name { get; set; }

        /// <summary>
        /// Enable real-time learning
        /// </summary>
        public bool RealTimeLearning { get; set; } = true;

        /// <summary>
        /// Force create even if active twin exists
        /// </summary>
        public bool ForceCreate { get; set; } = false;
    }

    /// <summary>
    /// Request model for learning data
    /// </summary>
    public class LearningDataRequest
    {
        /// <summary>
        /// Telemetry data for learning
        /// </summary>
        public Dictionary<string, double>? TelemetryData { get; set; }
    }

    /// <summary>
    /// Request model for predictions
    /// </summary>
    public class PredictionRequest
    {
        /// <summary>
        /// Scenario name
        /// </summary>
        public string? ScenarioName { get; set; }

        /// <summary>
        /// Scenario description
        /// </summary>
        public string? ScenarioDescription { get; set; }

        /// <summary>
        /// Scenario parameters for prediction
        /// </summary>
        public Dictionary<string, double>? ScenarioParameters { get; set; }
    }
}

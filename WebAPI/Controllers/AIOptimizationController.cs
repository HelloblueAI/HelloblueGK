using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Authorization;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.WebAPI.Services;
using HB_NLP_Research_Lab.Core;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace HB_NLP_Research_Lab.WebAPI.Controllers
{
    /// <summary>
    /// Controller for AI-driven engine optimization runs
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Tags("AI Optimization")]
    public class AIOptimizationController : ControllerBase
    {
        private readonly HelloblueGKDbContext _context;
        private readonly AdvancedAIOptimizationEngine _optimizationEngine;
        private readonly ILogger<AIOptimizationController> _logger;
        private readonly IBackgroundWorkQueue _backgroundWorkQueue;

        public AIOptimizationController(
            HelloblueGKDbContext context,
            AdvancedAIOptimizationEngine optimizationEngine,
            ILogger<AIOptimizationController> logger,
            IBackgroundWorkQueue backgroundWorkQueue)
        {
            _context = context;
            _optimizationEngine = optimizationEngine;
            _logger = logger;
            _backgroundWorkQueue = backgroundWorkQueue;
        }

        /// <summary>
        /// Get all optimization runs
        /// </summary>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<AIOptimizationRunResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllOptimizations([FromQuery] int? engineId = null)
        {
            try
            {
                var query = _context.AIOptimizationRuns
                    .Include(o => o.Engine)
                    .AsQueryable();

                if (!ApplyCurrentUserFilter(ref query))
                {
                    return Forbid();
                }

                if (engineId.HasValue)
                {
                    query = query.Where(o => o.EngineId == engineId.Value);
                }

                var optimizations = await query
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return Ok(optimizations.Select(AIOptimizationRunResponse.FromEntity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving optimization runs");
                return StatusCode(500, "An error occurred while retrieving optimization runs");
            }
        }

        /// <summary>
        /// Get optimization run by ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(AIOptimizationRunResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOptimizationById(int id)
        {
            try
            {
                var optimization = await _context.AIOptimizationRuns
                    .Include(o => o.Engine)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (optimization == null)
                {
                    return NotFound(new { message = $"Optimization run with ID {id} not found" });
                }

                if (!CurrentUserCanAccessOptimization(optimization))
                {
                    return Forbid();
                }

                return Ok(AIOptimizationRunResponse.FromEntity(optimization));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving optimization {OptimizationId}", id);
                return StatusCode(500, "An error occurred while retrieving the optimization run");
            }
        }

        /// <summary>
        /// Start a new AI optimization run for an engine
        /// </summary>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(AIOptimizationRun), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> StartOptimization([FromBody] StartOptimizationRequest request)
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

                // Validate algorithm type
                var validAlgorithms = new[] { "Genetic", "NeuralNetwork", "ReinforcementLearning", "MultiObjective" };
                if (!validAlgorithms.Contains(request.AlgorithmType, StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = $"Invalid algorithm type. Valid types: {string.Join(", ", validAlgorithms)}" });
                }

                var currentUsername = GetCurrentUsername();
                if (string.IsNullOrWhiteSpace(currentUsername))
                {
                    return Forbid();
                }

                if (!EngineAccessPolicy.CanUseEngine(User, engine, currentUsername))
                {
                    return Forbid();
                }

                if (!_backgroundWorkQueue.TryAcquire(out var backgroundWorkSlot))
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                    {
                        message = "The server is currently running the maximum number of background workloads. Try again later."
                    });
                }

                using (backgroundWorkSlot)
                {
                    // Create optimization run record
                    var optimization = new AIOptimizationRun
                    {
                        EngineId = request.EngineId,
                        AlgorithmType = request.AlgorithmType,
                        Status = "Pending",
                        ParametersJson = JsonSerializer.Serialize(request.Parameters ?? new Dictionary<string, object>()),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = currentUsername
                    };

                    _context.AIOptimizationRuns.Add(optimization);
                    await _context.SaveChangesAsync();

                    // Run optimization asynchronously with a new scope to avoid DbContext disposal issues
                    var optimizationId = optimization.Id;
                    var engineId = engine.Id;
                    backgroundWorkSlot.Queue(async (serviceProvider, cancellationToken) =>
                    {
                        var scopedContext = serviceProvider.GetRequiredService<HelloblueGKDbContext>();
                        var scopedEngine = await scopedContext.Engines.FindAsync(
                            [engineId],
                            cancellationToken);
                        if (scopedEngine != null)
                        {
                            await ExecuteOptimizationAsync(optimizationId, scopedEngine, request, scopedContext);
                        }
                    }, $"optimization:{optimizationId}");

                    return CreatedAtAction(
                        nameof(GetOptimizationById),
                        new { id = optimization.Id },
                        AIOptimizationRunResponse.FromEntity(optimization));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting optimization");
                return StatusCode(500, "An error occurred while starting the optimization");
            }
        }

        /// <summary>
        /// Get optimization status
        /// </summary>
        [HttpGet("{id}/status")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOptimizationStatus(int id)
        {
            try
            {
                var optimization = await _context.AIOptimizationRuns.FindAsync(id);
                if (optimization == null)
                {
                    return NotFound(new { message = $"Optimization run with ID {id} not found" });
                }

                if (!CurrentUserCanAccessOptimization(optimization))
                {
                    return Forbid();
                }

                return Ok(new
                {
                    id = optimization.Id,
                    status = optimization.Status,
                    algorithmType = optimization.AlgorithmType,
                    startedAt = optimization.StartedAt,
                    completedAt = optimization.CompletedAt,
                    executionTimeSeconds = optimization.ExecutionTimeSeconds,
                    improvementPercentage = optimization.ImprovementPercentage,
                    generations = optimization.Generations,
                    bestFitness = optimization.BestFitness,
                    errorMessage = SanitizeErrorMessage(optimization)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving optimization status {OptimizationId}", id);
                return StatusCode(500, "An error occurred while retrieving optimization status");
            }
        }

        private async Task ExecuteOptimizationAsync(int optimizationId, Engine engine, StartOptimizationRequest request, HelloblueGKDbContext context)
        {
            try
            {
                var optimization = await context.AIOptimizationRuns.FindAsync(optimizationId);
                if (optimization == null) return;

                optimization.Status = "Running";
                optimization.StartedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();

                var startTime = DateTime.UtcNow;

                // Create optimization parameters from engine
                var parameters = new EngineDesignParameters
                {
                    Thrust = engine.Thrust,
                    SpecificImpulse = engine.SpecificImpulse,
                    ChamberPressure = engine.ChamberPressure,
                    Efficiency = engine.Efficiency
                };

                // Run AI optimization
                var result = await _optimizationEngine.OptimizeEngineDesignAsync(parameters);
                var innovationReport = await _optimizationEngine.AnalyzeInnovationAsync(parameters);

                var executionTime = (DateTime.UtcNow - startTime).TotalSeconds;

                // Calculate improvement
                var originalEfficiency = engine.Efficiency;
                var optimizedEfficiency = result.OptimizedParameters?.Efficiency ?? originalEfficiency;
                var improvement = originalEfficiency > 0 
                    ? ((optimizedEfficiency - originalEfficiency) / originalEfficiency) * 100 
                    : 0.0;

                // Update optimization with results
                optimization.Status = "Completed";
                optimization.CompletedAt = DateTime.UtcNow;
                optimization.ExecutionTimeSeconds = executionTime;
                optimization.ImprovementPercentage = improvement;
                optimization.Generations = result.OptimizationStages?.Length ?? 100;
                optimization.BestFitness = result.OverallImprovement;

                var results = new
                {
                    originalParameters = new
                    {
                        thrust = parameters.Thrust,
                        specificImpulse = parameters.SpecificImpulse,
                        chamberPressure = parameters.ChamberPressure,
                        efficiency = parameters.Efficiency
                    },
                    optimizedParameters = new
                    {
                        thrust = result.OptimizedParameters?.Thrust,
                        specificImpulse = result.OptimizedParameters?.SpecificImpulse,
                        chamberPressure = result.OptimizedParameters?.ChamberPressure,
                        efficiency = result.OptimizedParameters?.Efficiency
                    },
                    improvement = improvement,
                    overallImprovement = result.OverallImprovement,
                    innovationScore = innovationReport.InnovationScore,
                    generations = result.OptimizationStages?.Length ?? 100
                };

                optimization.ResultsJson = JsonSerializer.Serialize(results);
                await context.SaveChangesAsync();

                _logger.LogInformation("Optimization {OptimizationId} completed: {Improvement}% improvement", 
                    optimizationId, improvement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing optimization {OptimizationId}", optimizationId);

                var optimization = await context.AIOptimizationRuns.FindAsync(optimizationId);
                if (optimization != null)
                {
                    optimization.Status = "Failed";
                    optimization.CompletedAt = DateTime.UtcNow;
                    optimization.ErrorMessage = "Optimization failed. See server logs for details.";
                    await context.SaveChangesAsync();
                }
            }
        }

        private bool ApplyCurrentUserFilter(ref IQueryable<AIOptimizationRun> query)
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

            query = query.Where(o => o.CreatedBy == currentUsername);
            return true;
        }

        private bool CurrentUserCanAccessOptimization(AIOptimizationRun optimization)
        {
            if (User.IsInRole("Admin"))
            {
                return true;
            }

            var currentUsername = GetCurrentUsername();
            return !string.IsNullOrWhiteSpace(currentUsername) &&
                string.Equals(optimization.CreatedBy, currentUsername, StringComparison.OrdinalIgnoreCase);
        }

        private string? GetCurrentUsername()
        {
            return User.Identity?.Name
                ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst("username")?.Value;
        }

        private static string? SanitizeErrorMessage(AIOptimizationRun optimization)
        {
            return string.Equals(optimization.Status, "Failed", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(optimization.ErrorMessage)
                    ? "Optimization failed. See server logs for details."
                    : null;
        }
    }

    /// <summary>
    /// Safe optimization response that excludes internal diagnostics from failed runs.
    /// </summary>
    public class AIOptimizationRunResponse
    {
        public int Id { get; set; }
        public int EngineId { get; set; }
        public string AlgorithmType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ParametersJson { get; set; }
        public string? ResultsJson { get; set; }
        public double? ImprovementPercentage { get; set; }
        public int? Generations { get; set; }
        public double? BestFitness { get; set; }
        public double? ExecutionTimeSeconds { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CreatedBy { get; set; }
        public EngineSummaryResponse? Engine { get; set; }

        public static AIOptimizationRunResponse FromEntity(AIOptimizationRun optimization)
        {
            return new AIOptimizationRunResponse
            {
                Id = optimization.Id,
                EngineId = optimization.EngineId,
                AlgorithmType = optimization.AlgorithmType,
                Status = optimization.Status,
                ParametersJson = optimization.ParametersJson,
                ResultsJson = optimization.ResultsJson,
                ImprovementPercentage = optimization.ImprovementPercentage,
                Generations = optimization.Generations,
                BestFitness = optimization.BestFitness,
                ExecutionTimeSeconds = optimization.ExecutionTimeSeconds,
                ErrorMessage = string.Equals(optimization.Status, "Failed", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(optimization.ErrorMessage)
                        ? "Optimization failed. See server logs for details."
                        : null,
                CreatedAt = optimization.CreatedAt,
                StartedAt = optimization.StartedAt,
                CompletedAt = optimization.CompletedAt,
                CreatedBy = optimization.CreatedBy,
                Engine = optimization.Engine == null ? null : EngineSummaryResponse.FromEntity(optimization.Engine)
            };
        }
    }

    /// <summary>
    /// Request model for starting an optimization
    /// </summary>
    public class StartOptimizationRequest
    {
        /// <summary>
        /// Engine ID to optimize
        /// </summary>
        [Required]
        public int EngineId { get; set; }

        /// <summary>
        /// Algorithm type: Genetic, NeuralNetwork, ReinforcementLearning, or MultiObjective
        /// </summary>
        [Required]
        public string AlgorithmType { get; set; } = string.Empty;

        /// <summary>
        /// Optional optimization parameters
        /// </summary>
        public Dictionary<string, object>? Parameters { get; set; }
    }
}

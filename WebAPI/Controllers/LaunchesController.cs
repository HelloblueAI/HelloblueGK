using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.Core;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace HB_NLP_Research_Lab.WebAPI.Controllers
{
    /// <summary>
    /// Controller for managing rocket launches
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Tags("Launches")]
    public class LaunchesController : ControllerBase
    {
        private readonly HelloblueGKDbContext _context;
        private readonly HelloblueGKEngine _engine;
        private readonly ILogger<LaunchesController> _logger;
        private readonly IServiceProvider _serviceProvider;

        public LaunchesController(
            HelloblueGKDbContext context,
            HelloblueGKEngine engine,
            ILogger<LaunchesController> logger,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _engine = engine;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Get all launches
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Launch>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllLaunches([FromQuery] string? status = null)
        {
            try
            {
                var query = _context.Launches
                    .Include(l => l.Engine)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(l => l.Status == status);
                }

                var launches = await query
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync();

                return Ok(launches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving launches");
                return StatusCode(500, "An error occurred while retrieving launches");
            }
        }

        /// <summary>
        /// Get launch by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Launch), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLaunchById(int id)
        {
            try
            {
                var launch = await _context.Launches
                    .Include(l => l.Engine)
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (launch == null)
                {
                    return NotFound(new { message = $"Launch with ID {id} not found" });
                }

                return Ok(launch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving launch {LaunchId}", id);
                return StatusCode(500, "An error occurred while retrieving the launch");
            }
        }

        /// <summary>
        /// Schedule a new rocket launch
        /// </summary>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(Launch), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ScheduleLaunch([FromBody] ScheduleLaunchRequest request)
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

                if (!engine.IsActive)
                {
                    return BadRequest(new { message = "Cannot launch with inactive engine" });
                }

                // Create launch record
                var launch = new Launch
                {
                    MissionName = request.MissionName,
                    Description = request.Description,
                    EngineId = request.EngineId,
                    EngineCount = request.EngineCount,
                    Status = "Scheduled",
                    ScheduledAt = request.ScheduledAt ?? DateTime.UtcNow.AddHours(1),
                    LaunchParametersJson = JsonSerializer.Serialize(request.LaunchParameters ?? new Dictionary<string, object>()),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name
                };

                _context.Launches.Add(launch);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetLaunchById), new { id = launch.Id }, launch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling launch");
                return StatusCode(500, "An error occurred while scheduling the launch");
            }
        }

        /// <summary>
        /// Execute a scheduled launch
        /// </summary>
        [HttpPost("{id}/launch")]
        [Authorize]
        [ProducesResponseType(typeof(Launch), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExecuteLaunch(int id)
        {
            try
            {
                var launch = await _context.Launches
                    .Include(l => l.Engine)
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (launch == null)
                {
                    return NotFound(new { message = $"Launch with ID {id} not found" });
                }

                if (launch.Status != "Scheduled")
                {
                    return BadRequest(new { message = $"Launch is not in Scheduled status. Current status: {launch.Status}" });
                }

                // Update status and start launch
                launch.Status = "InProgress";
                launch.LaunchedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Execute launch asynchronously
                // Run launch asynchronously with a new scope to avoid DbContext disposal issues
                var launchId = launch.Id;
                _ = Task.Run(async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var scopedContext = scope.ServiceProvider.GetRequiredService<HelloblueGKDbContext>();
                    await ExecuteLaunchAsync(launchId, scopedContext);
                });

                return Ok(launch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing launch {LaunchId}", id);
                return StatusCode(500, "An error occurred while executing the launch");
            }
        }

        /// <summary>
        /// Cancel a scheduled launch
        /// </summary>
        [HttpPost("{id}/cancel")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CancelLaunch(int id)
        {
            try
            {
                var launch = await _context.Launches.FindAsync(id);
                if (launch == null)
                {
                    return NotFound(new { message = $"Launch with ID {id} not found" });
                }

                if (launch.Status != "Scheduled")
                {
                    return BadRequest(new { message = $"Cannot cancel launch with status: {launch.Status}" });
                }

                launch.Status = "Cancelled";
                launch.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Launch cancelled successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling launch {LaunchId}", id);
                return StatusCode(500, "An error occurred while cancelling the launch");
            }
        }

        /// <summary>
        /// Get launch statistics
        /// </summary>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLaunchStatistics()
        {
            try
            {
                var total = await _context.Launches.CountAsync();
                var successful = await _context.Launches.CountAsync(l => l.MissionSuccess == true);
                var failed = await _context.Launches.CountAsync(l => l.MissionSuccess == false);
                var scheduled = await _context.Launches.CountAsync(l => l.Status == "Scheduled");
                var inProgress = await _context.Launches.CountAsync(l => l.Status == "InProgress");

                var launchesWithAltitude = await _context.Launches
                    .Where(l => l.MaxAltitude.HasValue)
                    .ToListAsync();
                var avgAltitude = launchesWithAltitude.Any() 
                    ? launchesWithAltitude.Average(l => l.MaxAltitude!.Value) 
                    : 0.0;

                var launchesWithVelocity = await _context.Launches
                    .Where(l => l.MaxVelocity.HasValue)
                    .ToListAsync();
                var avgVelocity = launchesWithVelocity.Any() 
                    ? launchesWithVelocity.Average(l => l.MaxVelocity!.Value) 
                    : 0.0;

                return Ok(new
                {
                    totalLaunches = total,
                    successful = successful,
                    failed = failed,
                    successRate = total > 0 ? (double)successful / total * 100 : 0,
                    scheduled = scheduled,
                    inProgress = inProgress,
                    averageMaxAltitude = avgAltitude,
                    averageMaxVelocity = avgVelocity
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving launch statistics");
                return StatusCode(500, "An error occurred while retrieving launch statistics");
            }
        }

        private async Task ExecuteLaunchAsync(int launchId, HelloblueGKDbContext context)
        {
            try
            {
                var launch = await context.Launches
                    .Include(l => l.Engine)
                    .FirstOrDefaultAsync(l => l.Id == launchId);

                if (launch == null) return;

                var startTime = DateTime.UtcNow;

                // Simulate launch using engine analysis
                var analysisResult = await _engine.AnalyzeEngineAsync(launch.Engine.Name);

                // Calculate launch results based on engine performance
                var totalThrust = launch.Engine.Thrust * launch.EngineCount; // Newtons
                var specificImpulse = launch.Engine.SpecificImpulse; // seconds
                var massFlowRate = launch.Engine.MassFlowRate * launch.EngineCount; // kg/s

                // Simulate mission parameters (simplified rocket physics)
                var burnTime = 180.0; // seconds (3 minutes)
                var deltaV = specificImpulse * 9.81 * Math.Log(2.0); // Simplified Tsiolkovsky
                var maxVelocity = deltaV; // m/s
                var maxAltitude = (maxVelocity * maxVelocity) / (2 * 9.81); // meters (simplified)

                // Mission success based on engine efficiency
                var missionSuccess = launch.Engine.Efficiency > 0.90 && analysisResult.ValidationReport?.OverallAccuracy > 95;

                var missionDuration = (DateTime.UtcNow - startTime).TotalSeconds;

                // Update launch with results
                launch.Status = missionSuccess ? "Success" : "Failed";
                launch.CompletedAt = DateTime.UtcNow;
                launch.MissionDurationSeconds = missionDuration;
                launch.MaxAltitude = maxAltitude;
                launch.MaxVelocity = maxVelocity;
                launch.MissionSuccess = missionSuccess;

                var results = new
                {
                    totalThrust = totalThrust,
                    specificImpulse = specificImpulse,
                    massFlowRate = massFlowRate,
                    burnTime = burnTime,
                    deltaV = deltaV,
                    maxVelocity = maxVelocity,
                    maxAltitude = maxAltitude,
                    missionDuration = missionDuration,
                    engineEfficiency = launch.Engine.Efficiency,
                    validationAccuracy = analysisResult.ValidationReport?.OverallAccuracy
                };

                launch.ResultsJson = JsonSerializer.Serialize(results);

                if (!missionSuccess)
                {
                    launch.ErrorMessage = "Mission failed due to engine performance below threshold";
                }

                await context.SaveChangesAsync();

                _logger.LogInformation("Launch {LaunchId} completed: {Status}", launchId, launch.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing launch {LaunchId}", launchId);

                var launch = await context.Launches.FindAsync(launchId);
                if (launch != null)
                {
                    launch.Status = "Failed";
                    launch.CompletedAt = DateTime.UtcNow;
                    launch.MissionSuccess = false;
                    launch.ErrorMessage = ex.Message;
                    await context.SaveChangesAsync();
                }
            }
        }
    }

    /// <summary>
    /// Request model for scheduling a launch
    /// </summary>
    public class ScheduleLaunchRequest
    {
        /// <summary>
        /// Mission name
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string MissionName { get; set; } = string.Empty;

        /// <summary>
        /// Mission description
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Engine ID to use for launch
        /// </summary>
        [Required]
        public int EngineId { get; set; }

        /// <summary>
        /// Number of engines (for multi-engine configurations)
        /// </summary>
        [Range(1, 100)]
        public int EngineCount { get; set; } = 1;

        /// <summary>
        /// Scheduled launch time (defaults to 1 hour from now)
        /// </summary>
        public DateTime? ScheduledAt { get; set; }

        /// <summary>
        /// Optional launch parameters
        /// </summary>
        public Dictionary<string, object>? LaunchParameters { get; set; }
    }
}

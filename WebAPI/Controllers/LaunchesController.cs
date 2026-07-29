using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.WebAPI.Models;
using HB_NLP_Research_Lab.WebAPI.Services;
using HB_NLP_Research_Lab.WebAPI.Validation;
using HB_NLP_Research_Lab.Core;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

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
        private readonly IBackgroundWorkQueue _backgroundWorkQueue;

        public LaunchesController(
            HelloblueGKDbContext context,
            HelloblueGKEngine engine,
            ILogger<LaunchesController> logger,
            IBackgroundWorkQueue backgroundWorkQueue)
        {
            _context = context;
            _engine = engine;
            _logger = logger;
            _backgroundWorkQueue = backgroundWorkQueue;
        }

        /// <summary>
        /// Get all launches
        /// </summary>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<LaunchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllLaunches(
            [FromQuery] string? status = null,
            [FromQuery] int skip = PaginationRequest.DefaultSkip,
            [FromQuery] int take = PaginationRequest.DefaultTake)
        {
            try
            {
                var pagination = PaginationRequest.Create(skip, take);
                if (!pagination.TryValidate(out var validationMessage))
                {
                    return BadRequest(new { message = validationMessage });
                }

                var query = _context.Launches
                    .Include(l => l.Engine)
                    .AsQueryable();

                if (!ApplyCurrentUserFilter(ref query))
                {
                    return Forbid();
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(l => l.Status == status);
                }

                var launches = await query
                    .OrderByDescending(l => l.CreatedAt)
                    .Skip(pagination.Skip)
                    .Take(pagination.Take)
                    .ToListAsync();

                return Ok(launches.Select(LaunchResponse.FromEntity));
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
        [Authorize]
        [ProducesResponseType(typeof(LaunchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLaunchById(int id)
        {
            try
            {
                var launch = await _context.Launches
                    .Include(l => l.Engine)
                    .FirstOrDefaultAsync(l => l.Id == id);

                // Same 404 for missing and inaccessible to avoid ownership oracles.
                if (launch == null || !CurrentUserCanAccessLaunch(launch))
                {
                    return NotFound(new { message = $"Launch with ID {id} not found" });
                }

                return Ok(LaunchResponse.FromEntity(launch));
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
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(LaunchResponse), StatusCodes.Status201Created)]
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

                if (request == null)
                {
                    return BadRequest(new { message = "Request body is required" });
                }

                if (!RequestPayloadLimits.TryValidateDictionary(
                    request.LaunchParameters,
                    nameof(request.LaunchParameters),
                    out var launchParametersValidationMessage))
                {
                    return BadRequest(new { message = launchParametersValidationMessage });
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

                var currentUsername = GetCurrentUsername();
                if (string.IsNullOrWhiteSpace(currentUsername))
                {
                    return Forbid();
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
                    CreatedBy = currentUsername,
                    Engine = engine
                };

                _context.Launches.Add(launch);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetLaunchById), new { id = launch.Id }, LaunchResponse.FromEntity(launch));
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
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(LaunchResponse), StatusCodes.Status200OK)]
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

                if (launch.Engine == null || !launch.Engine.IsActive)
                {
                    return BadRequest(new { message = "Cannot execute launch with an inactive or missing engine" });
                }

                if (!_backgroundWorkQueue.TryAcquire(out var backgroundWorkSlot) || backgroundWorkSlot == null)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                    {
                        message = "The server is currently running the maximum number of background workloads. Try again later."
                    });
                }

                using (backgroundWorkSlot)
                {
                    var launchedAt = DateTime.UtcNow;
                    // Claim only when still Scheduled and the engine remains active.
                    var transitioned = await _context.Launches
                        .Where(l => l.Id == id &&
                                    l.Status == "Scheduled" &&
                                    l.Engine != null &&
                                    l.Engine.IsActive)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(l => l.Status, "InProgress")
                            .SetProperty(l => l.LaunchedAt, launchedAt));

                    if (transitioned == 0)
                    {
                        await _context.Entry(launch).ReloadAsync();
                        if (launch.Engine != null)
                        {
                            await _context.Entry(launch.Engine).ReloadAsync();
                        }

                        if (launch.Engine == null || !launch.Engine.IsActive)
                        {
                            return BadRequest(new { message = "Cannot execute launch with an inactive or missing engine" });
                        }

                        return BadRequest(new
                        {
                            message = $"Launch is not in Scheduled status. Current status: {launch.Status}"
                        });
                    }

                    launch.Status = "InProgress";
                    launch.LaunchedAt = launchedAt;

                    // Execute launch asynchronously with a new scope to avoid DbContext disposal issues
                    var launchId = launch.Id;
                    try
                    {
                        backgroundWorkSlot.Queue(async (serviceProvider, _) =>
                        {
                            var scopedContext = serviceProvider.GetRequiredService<HelloblueGKDbContext>();
                            try
                            {
                                await ExecuteLaunchAsync(launchId, scopedContext);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Unhandled error in launch background work {LaunchId}", launchId);
                                await FailLaunchAsync(
                                    scopedContext,
                                    launchId,
                                    "Launch failed. See server logs for details.");
                            }
                        }, $"launch:{launchId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to queue launch {LaunchId}; reverting claim", launchId);
                        await FailLaunchAsync(
                            _context,
                            launchId,
                            "Launch failed because background work could not be queued.");
                        return StatusCode(500, "An error occurred while executing the launch");
                    }

                    return Ok(LaunchResponse.FromEntity(launch));
                }
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
        [Authorize(Roles = "Admin")]
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

                if (launch.Status != "Scheduled" && launch.Status != "InProgress")
                {
                    return BadRequest(new { message = $"Cannot cancel launch with status: {launch.Status}" });
                }

                var completedAt = DateTime.UtcNow;
                // Atomically claim Scheduled or InProgress so a concurrent worker
                // completion cannot be overwritten by a stale cancel write.
                var transitioned = await _context.Launches
                    .Where(l => l.Id == id && (l.Status == "Scheduled" || l.Status == "InProgress"))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(l => l.Status, "Cancelled")
                        .SetProperty(l => l.CompletedAt, completedAt));

                if (transitioned == 0)
                {
                    await _context.Entry(launch).ReloadAsync();
                    return BadRequest(new { message = $"Cannot cancel launch with status: {launch.Status}" });
                }

                launch.Status = "Cancelled";
                launch.CompletedAt = completedAt;

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
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLaunchStatistics()
        {
            try
            {
                var query = _context.Launches.AsQueryable();
                if (!ApplyCurrentUserFilter(ref query))
                {
                    return Forbid();
                }

                var total = await query.CountAsync();
                var successful = await query.CountAsync(l => l.MissionSuccess == true);
                var failed = await query.CountAsync(l => l.MissionSuccess == false);
                var scheduled = await query.CountAsync(l => l.Status == "Scheduled");
                var inProgress = await query.CountAsync(l => l.Status == "InProgress");

                var launchesWithAltitude = query
                    .Where(l => l.MaxAltitude.HasValue)
                    .Select(l => l.MaxAltitude!.Value);
                var avgAltitude = await launchesWithAltitude.AnyAsync()
                    ? await launchesWithAltitude.AverageAsync()
                    : 0.0;

                var launchesWithVelocity = query
                    .Where(l => l.MaxVelocity.HasValue)
                    .Select(l => l.MaxVelocity!.Value);
                var avgVelocity = await launchesWithVelocity.AnyAsync()
                    ? await launchesWithVelocity.AverageAsync()
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

                // Bail out early if cancel won the race before work started.
                if (launch.Status != "InProgress")
                {
                    _logger.LogInformation(
                        "Launch {LaunchId} is {Status}; skipping execution",
                        launchId,
                        launch.Status);
                    return;
                }

                if (launch.Engine == null || !launch.Engine.IsActive)
                {
                    await FailLaunchAsync(
                        context,
                        launchId,
                        "Launch failed because the engine is inactive or missing.");
                    return;
                }

                var startTime = DateTime.UtcNow;
                var launchParameters = DeserializeLaunchParameters(launch.LaunchParametersJson);
                var burnTime = TryReadLaunchDouble(launchParameters, "burnTimeSeconds", out var burnTimeSeconds) &&
                               burnTimeSeconds > 0
                    ? burnTimeSeconds
                    : TryReadLaunchDouble(launchParameters, "burnTime", out var burnTimeAlias) && burnTimeAlias > 0
                        ? burnTimeAlias
                        : 180.0;
                var massRatio = TryReadLaunchDouble(launchParameters, "massRatio", out var requestedMassRatio) &&
                                requestedMassRatio > 1.0
                    ? requestedMassRatio
                    : 2.0;
                var efficiencyThreshold = TryReadLaunchDouble(launchParameters, "successEfficiencyThreshold", out var effThreshold) &&
                                         effThreshold > 0
                    ? Math.Clamp(effThreshold, 0.0, 1.0)
                    : 0.90;
                var accuracyThreshold = TryReadLaunchDouble(launchParameters, "successAccuracyThreshold", out var accThreshold) &&
                                        accThreshold > 0
                    ? accThreshold
                    : 95.0;
                var simulationType = TryReadLaunchString(launchParameters, "simulationType") ?? "MultiPhysics";

                var baselineDesign = HelloblueGKEngine.CreateDesignParametersFromEngine(
                    launch.Engine.Thrust,
                    launch.Engine.SpecificImpulse,
                    launch.Engine.ChamberPressure,
                    launch.Engine.Efficiency);

                // Simulate launch using engine analysis with stored mission parameters.
                var analysisResult = await _engine.AnalyzeEngineAsync(
                    launch.Engine.Name,
                    simulationType,
                    launchParameters,
                    baselineDesign);

                // Calculate launch results based on engine performance + mission parameters.
                var totalThrust = launch.Engine.Thrust * launch.EngineCount; // Newtons
                var specificImpulse = launch.Engine.SpecificImpulse; // seconds
                var massFlowRate = launch.Engine.MassFlowRate * launch.EngineCount; // kg/s
                if (TryReadLaunchDouble(launchParameters, "thrust", out var thrustOverride) && thrustOverride > 0)
                {
                    totalThrust = thrustOverride * launch.EngineCount;
                }

                if (TryReadLaunchDouble(launchParameters, "specificImpulse", out var ispOverride) && ispOverride > 0)
                {
                    specificImpulse = ispOverride;
                }

                var deltaV = specificImpulse * 9.81 * Math.Log(massRatio); // Tsiolkovsky with mission mass ratio
                var maxVelocity = deltaV; // m/s
                var maxAltitude = (maxVelocity * maxVelocity) / (2 * 9.81); // meters (simplified)
                if (TryReadLaunchDouble(launchParameters, "gravity", out var gravity) && gravity > 0)
                {
                    maxAltitude = (maxVelocity * maxVelocity) / (2 * gravity);
                }

                // Mission success based on engine efficiency / validation thresholds (parameterizable).
                var missionSuccess = launch.Engine.Efficiency > efficiencyThreshold &&
                    (analysisResult.ValidationReport?.OverallAccuracy ?? 0) > accuracyThreshold;

                var missionDuration = (DateTime.UtcNow - startTime).TotalSeconds;
                var completedAt = DateTime.UtcNow;
                var status = missionSuccess ? "Success" : "Failed";
                var errorMessage = missionSuccess
                    ? null
                    : "Mission failed due to engine performance below threshold";

                var resultsJson = JsonSerializer.Serialize(new
                {
                    totalThrust = totalThrust,
                    specificImpulse = specificImpulse,
                    massFlowRate = massFlowRate,
                    burnTime = burnTime,
                    massRatio = massRatio,
                    deltaV = deltaV,
                    maxVelocity = maxVelocity,
                    maxAltitude = maxAltitude,
                    missionDuration = missionDuration,
                    engineEfficiency = launch.Engine.Efficiency,
                    validationAccuracy = analysisResult.ValidationReport?.OverallAccuracy,
                    simulationType = analysisResult.SimulationType,
                    appliedLaunchParameters = launchParameters
                });

                // Only complete if still InProgress so a concurrent cancel is preserved.
                var completed = await context.Launches
                    .Where(l => l.Id == launchId && l.Status == "InProgress")
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(l => l.Status, status)
                        .SetProperty(l => l.CompletedAt, completedAt)
                        .SetProperty(l => l.MissionDurationSeconds, missionDuration)
                        .SetProperty(l => l.MaxAltitude, maxAltitude)
                        .SetProperty(l => l.MaxVelocity, maxVelocity)
                        .SetProperty(l => l.MissionSuccess, missionSuccess)
                        .SetProperty(l => l.ResultsJson, resultsJson)
                        .SetProperty(l => l.ErrorMessage, errorMessage));

                if (completed == 0)
                {
                    var currentStatus = await context.Launches
                        .AsNoTracking()
                        .Where(l => l.Id == launchId)
                        .Select(l => l.Status)
                        .FirstOrDefaultAsync();

                    _logger.LogInformation(
                        "Launch {LaunchId} was {Status} before results were persisted; discarding completion",
                        launchId,
                        currentStatus ?? "missing");
                    return;
                }

                _logger.LogInformation("Launch {LaunchId} completed: {Status}", launchId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing launch {LaunchId}", launchId);
                await FailLaunchAsync(
                    context,
                    launchId,
                    "Launch failed. See server logs for details.");
            }
        }

        private static async Task FailLaunchAsync(
            HelloblueGKDbContext context,
            int launchId,
            string errorMessage)
        {
            await context.Launches
                .Where(l => l.Id == launchId && l.Status == "InProgress")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Status, "Failed")
                    .SetProperty(l => l.CompletedAt, DateTime.UtcNow)
                    .SetProperty(l => l.MissionSuccess, false)
                    .SetProperty(l => l.ErrorMessage, errorMessage));
        }

        private static Dictionary<string, object> DeserializeLaunchParameters(string? launchParametersJson)
        {
            if (string.IsNullOrWhiteSpace(launchParametersJson))
            {
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(launchParametersJson);
                if (parsed == null)
                {
                    return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                }

                return parsed.ToDictionary(
                    pair => pair.Key,
                    pair => (object)pair.Value,
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static bool TryReadLaunchDouble(
            IReadOnlyDictionary<string, object> parameters,
            string key,
            out double value)
        {
            value = 0;
            if (!parameters.TryGetValue(key, out var raw) || raw == null)
            {
                return false;
            }

            switch (raw)
            {
                case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                    value = d;
                    return true;
                case float f when !float.IsNaN(f) && !float.IsInfinity(f):
                    value = f;
                    return true;
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = l;
                    return true;
                case decimal m:
                    value = (double)m;
                    return true;
                case JsonElement element when element.ValueKind == JsonValueKind.Number &&
                                             element.TryGetDouble(out var jsonNumber):
                    value = jsonNumber;
                    return !double.IsNaN(jsonNumber) && !double.IsInfinity(jsonNumber);
                case JsonElement element when element.ValueKind == JsonValueKind.String &&
                                             double.TryParse(
                                                 element.GetString(),
                                                 System.Globalization.NumberStyles.Float,
                                                 System.Globalization.CultureInfo.InvariantCulture,
                                                 out var jsonStringNumber):
                    value = jsonStringNumber;
                    return !double.IsNaN(jsonStringNumber) && !double.IsInfinity(jsonStringNumber);
                case string text when double.TryParse(
                    text,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed):
                    value = parsed;
                    return !double.IsNaN(parsed) && !double.IsInfinity(parsed);
                default:
                    return false;
            }
        }

        private static string? TryReadLaunchString(
            IReadOnlyDictionary<string, object> parameters,
            string key)
        {
            if (!parameters.TryGetValue(key, out var raw) || raw == null)
            {
                return null;
            }

            return raw switch
            {
                string text when !string.IsNullOrWhiteSpace(text) => text.Trim(),
                JsonElement element when element.ValueKind == JsonValueKind.String =>
                    element.GetString()?.Trim(),
                _ => null
            };
        }

        private bool ApplyCurrentUserFilter(ref IQueryable<Launch> query)
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

            query = query.Where(l => l.CreatedBy == currentUsername);
            return true;
        }

        private bool CurrentUserCanAccessLaunch(Launch launch)
        {
            if (User.IsInRole("Admin"))
            {
                return true;
            }

            var currentUsername = GetCurrentUsername();
            return !string.IsNullOrWhiteSpace(currentUsername) &&
                string.Equals(launch.CreatedBy, currentUsername, StringComparison.OrdinalIgnoreCase);
        }

        private string? GetCurrentUsername()
        {
            return User.Identity?.Name
                ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst("username")?.Value;
        }
    }

    /// <summary>
    /// Safe launch response that excludes internal diagnostics from failed background work.
    /// </summary>
    public class LaunchResponse
    {
        public int Id { get; set; }
        public string MissionName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int EngineId { get; set; }
        public int EngineCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? LaunchParametersJson { get; set; }
        public string? ResultsJson { get; set; }
        public double? MissionDurationSeconds { get; set; }
        public double? MaxAltitude { get; set; }
        public double? MaxVelocity { get; set; }
        public bool? MissionSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime ScheduledAt { get; set; }
        public DateTime? LaunchedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public EngineSummaryResponse? Engine { get; set; }

        public static LaunchResponse FromEntity(Launch launch)
        {
            return new LaunchResponse
            {
                Id = launch.Id,
                MissionName = launch.MissionName,
                Description = launch.Description,
                EngineId = launch.EngineId,
                EngineCount = launch.EngineCount,
                Status = launch.Status,
                LaunchParametersJson = launch.LaunchParametersJson,
                ResultsJson = launch.ResultsJson,
                MissionDurationSeconds = launch.MissionDurationSeconds,
                MaxAltitude = launch.MaxAltitude,
                MaxVelocity = launch.MaxVelocity,
                MissionSuccess = launch.MissionSuccess,
                ErrorMessage = GetSafeErrorMessage(launch),
                ScheduledAt = launch.ScheduledAt,
                LaunchedAt = launch.LaunchedAt,
                CompletedAt = launch.CompletedAt,
                CreatedBy = launch.CreatedBy,
                CreatedAt = launch.CreatedAt,
                Engine = launch.Engine == null ? null : EngineSummaryResponse.FromEntity(launch.Engine)
            };
        }

        private static string? GetSafeErrorMessage(Launch launch)
        {
            if (string.IsNullOrWhiteSpace(launch.ErrorMessage))
            {
                return null;
            }

            if (!string.Equals(launch.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return launch.ErrorMessage == "Mission failed due to engine performance below threshold"
                ? launch.ErrorMessage
                : "Launch failed. See server logs for details.";
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

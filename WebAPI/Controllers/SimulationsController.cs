using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Authorization;
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
    /// Controller for running engine simulations (CFD, Thermal, Structural, MultiPhysics)
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Tags("Simulations")]
    public class SimulationsController : ControllerBase
    {
        private readonly HelloblueGKDbContext _context;
        private readonly HelloblueGKEngine _engine;
        private readonly ILogger<SimulationsController> _logger;
        private readonly IBackgroundWorkQueue _backgroundWorkQueue;

        public SimulationsController(
            HelloblueGKDbContext context,
            HelloblueGKEngine engine,
            ILogger<SimulationsController> logger,
            IBackgroundWorkQueue backgroundWorkQueue)
        {
            _context = context;
            _engine = engine;
            _logger = logger;
            _backgroundWorkQueue = backgroundWorkQueue;
        }

        /// <summary>
        /// Get all simulations
        /// </summary>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<EngineSimulationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllSimulations(
            [FromQuery] int? engineId = null,
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

                var query = _context.EngineSimulations
                    .Include(s => s.Engine)
                    .AsQueryable();

                if (!User.IsInRole("Admin"))
                {
                    var currentUsername = GetCurrentUsername();
                    if (string.IsNullOrWhiteSpace(currentUsername))
                    {
                        return Forbid();
                    }

                    query = query.Where(s => s.CreatedBy == currentUsername);
                }

                if (engineId.HasValue)
                {
                    query = query.Where(s => s.EngineId == engineId.Value);
                }

                var simulations = await query
                    .OrderByDescending(s => s.CreatedAt)
                    .Skip(pagination.Skip)
                    .Take(pagination.Take)
                    .ToListAsync();

                return Ok(simulations.Select(simulation => EngineSimulationResponse.FromEntity(simulation)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving simulations");
                return StatusCode(500, "An error occurred while retrieving simulations");
            }
        }

        /// <summary>
        /// Get simulation by ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(EngineSimulationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSimulationById(int id)
        {
            try
            {
                var simulation = await _context.EngineSimulations
                    .Include(s => s.Engine)
                    .Include(s => s.Telemetry)
                    .FirstOrDefaultAsync(s => s.Id == id);

                // Same 404 for missing and inaccessible to avoid ownership oracles.
                if (simulation == null || !CurrentUserCanAccessSimulation(simulation))
                {
                    return NotFound(new { message = $"Simulation with ID {id} not found" });
                }

                return Ok(EngineSimulationResponse.FromEntity(simulation, includeTelemetry: true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving simulation {SimulationId}", id);
                return StatusCode(500, "An error occurred while retrieving the simulation");
            }
        }

        /// <summary>
        /// Run a new simulation for an engine
        /// </summary>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(EngineSimulationResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RunSimulation([FromBody] RunSimulationRequest request)
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
                    request.Parameters,
                    nameof(request.Parameters),
                    out var parametersValidationMessage))
                {
                    return BadRequest(new { message = parametersValidationMessage });
                }

                // Validate engine exists
                var engine = await _context.Engines.FindAsync(request.EngineId);
                if (engine == null)
                {
                    return NotFound(new { message = $"Engine with ID {request.EngineId} not found" });
                }

                // Validate simulation type
                var validTypes = new[] { "CFD", "Thermal", "Structural", "MultiPhysics" };
                if (!validTypes.Contains(request.SimulationType, StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = $"Invalid simulation type. Valid types: {string.Join(", ", validTypes)}" });
                }

                var currentUsername = GetCurrentUsername();
                if (string.IsNullOrWhiteSpace(currentUsername))
                {
                    return Forbid();
                }

                // Same 404 as a missing engine so private-engine existence is not leaked.
                if (!EngineAccessPolicy.CanUseEngine(User, engine, currentUsername))
                {
                    return NotFound(new { message = $"Engine with ID {request.EngineId} not found" });
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
                    // Create simulation record
                    var simulation = new EngineSimulation
                    {
                        EngineId = request.EngineId,
                        SimulationType = request.SimulationType,
                        Status = "Pending",
                        ParametersJson = JsonSerializer.Serialize(request.Parameters ?? new Dictionary<string, object>()),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = currentUsername
                    };

                    _context.EngineSimulations.Add(simulation);
                    await _context.SaveChangesAsync();

                    // Run simulation asynchronously with a new scope to avoid DbContext disposal issues
                    var simulationId = simulation.Id;
                    var engineId = engine.Id;
                    try
                    {
                        backgroundWorkSlot.Queue(async (serviceProvider, cancellationToken) =>
                        {
                            var scopedContext = serviceProvider.GetRequiredService<HelloblueGKDbContext>();
                            try
                            {
                                var scopedEngine = await scopedContext.Engines.FindAsync(
                                    [engineId],
                                    cancellationToken);
                                if (scopedEngine == null)
                                {
                                    await FailSimulationAsync(
                                        scopedContext,
                                        simulationId,
                                        "Simulation failed because the target engine no longer exists.");
                                    return;
                                }

                                await ExecuteSimulationAsync(
                                    simulationId,
                                    scopedEngine,
                                    request,
                                    scopedContext,
                                    cancellationToken);
                            }
                            catch (OperationCanceledException ex)
                            {
                                // Cover cancellation during pre-execution engine lookup while still Pending.
                                _logger.LogWarning(ex, "Simulation background work cancelled {SimulationId}", simulationId);
                                await FailSimulationAsync(
                                    scopedContext,
                                    simulationId,
                                    "Simulation cancelled before completion.",
                                    markAsCancelled: true);
                            }
                            catch (ObjectDisposedException ex)
                            {
                                _logger.LogError(ex, "Unhandled error in simulation background work {SimulationId}", simulationId);
                                await FailSimulationAsync(
                                    scopedContext,
                                    simulationId,
                                    "Simulation failed. See server logs for details.");
                            }
                            catch (InvalidOperationException ex)
                            {
                                _logger.LogError(ex, "Unhandled error in simulation background work {SimulationId}", simulationId);
                                await FailSimulationAsync(
                                    scopedContext,
                                    simulationId,
                                    "Simulation failed. See server logs for details.");
                            }
                            catch (DbUpdateException ex)
                            {
                                _logger.LogError(ex, "Unhandled error in simulation background work {SimulationId}", simulationId);
                                await FailSimulationAsync(
                                    scopedContext,
                                    simulationId,
                                    "Simulation failed. See server logs for details.");
                            }
                            catch (Exception ex) when (
                                ex is not OperationCanceledException &&
                                ex is not OutOfMemoryException &&
                                ex is not StackOverflowException &&
                                ex is not AccessViolationException &&
                                ex is not AppDomainUnloadedException &&
                                ex is not BadImageFormatException &&
                                ex is not CannotUnloadAppDomainException &&
                                ex is not InvalidProgramException)
                            {
                                _logger.LogError(ex, "Unhandled error in simulation background work {SimulationId}", simulationId);
                                await FailSimulationAsync(
                                    scopedContext,
                                    simulationId,
                                    "Simulation failed. See server logs for details.");
                            }
                        }, $"simulation:{simulationId}");
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogError(ex, "Failed to queue simulation {SimulationId}", simulationId);
                        await FailSimulationAsync(
                            _context,
                            simulationId,
                            "Simulation failed because background work could not be queued.");
                        return StatusCode(500, "An error occurred while creating the simulation");
                    }

                    return CreatedAtAction(
                        nameof(GetSimulationById),
                        new { id = simulation.Id },
                        EngineSimulationResponse.FromEntity(simulation));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating simulation");
                return StatusCode(500, "An error occurred while creating the simulation");
            }
        }

        /// <summary>
        /// Get simulation status
        /// </summary>
        [HttpGet("{id}/status")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSimulationStatus(int id)
        {
            try
            {
                var simulation = await _context.EngineSimulations.FindAsync(id);
                // Same 404 for missing and inaccessible to avoid ownership oracles.
                if (simulation == null || !CurrentUserCanAccessSimulation(simulation))
                {
                    return NotFound(new { message = $"Simulation with ID {id} not found" });
                }

                return Ok(new
                {
                    id = simulation.Id,
                    status = simulation.Status,
                    startedAt = simulation.StartedAt,
                    completedAt = simulation.CompletedAt,
                    executionTimeSeconds = simulation.ExecutionTimeSeconds,
                    accuracy = simulation.Accuracy,
                    errorMessage = GetSafeErrorMessage(simulation.Status, simulation.ErrorMessage)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving simulation status {SimulationId}", id);
                return StatusCode(500, "An error occurred while retrieving simulation status");
            }
        }

        /// <summary>
        /// Cancel a running simulation
        /// </summary>
        [HttpPost("{id}/cancel")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CancelSimulation(int id)
        {
            try
            {
                var simulation = await _context.EngineSimulations.FindAsync(id);
                // Same 404 for missing and inaccessible to avoid ownership oracles.
                if (simulation == null || !CurrentUserCanAccessSimulation(simulation))
                {
                    return NotFound(new { message = $"Simulation with ID {id} not found" });
                }

                if (simulation.Status != "Running" && simulation.Status != "Pending")
                {
                    return BadRequest(new { message = $"Cannot cancel simulation with status: {simulation.Status}" });
                }

                var completedAt = DateTime.UtcNow;
                var cancelled = await _context.EngineSimulations
                    .Where(s => s.Id == id && (s.Status == "Running" || s.Status == "Pending"))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(s => s.Status, "Cancelled")
                        .SetProperty(s => s.CompletedAt, completedAt));

                if (cancelled == 0)
                {
                    await _context.Entry(simulation).ReloadAsync();
                    return BadRequest(new { message = $"Cannot cancel simulation with status: {simulation.Status}" });
                }

                simulation.Status = "Cancelled";
                simulation.CompletedAt = completedAt;

                return Ok(new { message = "Simulation cancelled successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling simulation {SimulationId}", id);
                return StatusCode(500, "An error occurred while cancelling the simulation");
            }
        }

        private async Task ExecuteSimulationAsync(
            int simulationId,
            Engine engine,
            RunSimulationRequest request,
            HelloblueGKDbContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                var startedAt = DateTime.UtcNow;
                var transitionedToRunning = await context.EngineSimulations
                    .Where(s => s.Id == simulationId && s.Status == "Pending")
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(s => s.Status, "Running")
                            .SetProperty(s => s.StartedAt, startedAt),
                        cancellationToken);

                if (transitionedToRunning == 0)
                {
                    var currentStatus = await context.EngineSimulations
                        .AsNoTracking()
                        .Where(s => s.Id == simulationId)
                        .Select(s => s.Status)
                        .FirstOrDefaultAsync(cancellationToken);

                    _logger.LogInformation(
                        "Skipping simulation {SimulationId} because it is already {Status}",
                        simulationId,
                        currentStatus ?? "missing");
                    return;
                }

                // Run the requested simulation type with engine baselines + optional overrides.
                var baselineDesign = HelloblueGKEngine.CreateDesignParametersFromEngine(
                    engine.Thrust,
                    engine.SpecificImpulse,
                    engine.ChamberPressure,
                    engine.Efficiency);
                var analysisResult = await _engine.AnalyzeEngineAsync(
                    engine.Name,
                    request.SimulationType,
                    request.Parameters,
                    baselineDesign);
                cancellationToken.ThrowIfCancellationRequested();

                var executionTime = (DateTime.UtcNow - startedAt).TotalSeconds;
                var accuracy = analysisResult.ValidationReport?.OverallAccuracy / 100.0
                    ?? analysisResult.ConvergenceRate;
                if (accuracy <= 0)
                {
                    accuracy = 0.95;
                }

                var completedAt = DateTime.UtcNow;
                var iterations = analysisResult.Iterations > 0 ? analysisResult.Iterations : 1000;
                var convergenceRate = analysisResult.ConvergenceRate > 0
                    ? analysisResult.ConvergenceRate
                    : 0.99;
                var resultsJson = JsonSerializer.Serialize(new
                {
                    simulationType = analysisResult.SimulationType,
                    parameters = request.Parameters ?? new Dictionary<string, object>(),
                    thrustAnalysis = new
                    {
                        maxThrust = analysisResult.ThrustAnalysis?.MaxThrust,
                        efficiency = analysisResult.ThrustAnalysis?.Efficiency
                    },
                    thermalAnalysis = new
                    {
                        maxTemperature = analysisResult.ThermalAnalysis?.MaxTemperature,
                        coolingEfficiency = analysisResult.ThermalAnalysis?.CoolingEfficiency
                    },
                    structuralAnalysis = new
                    {
                        maxStress = analysisResult.StructuralAnalysis?.MaxStress,
                        safetyFactor = analysisResult.StructuralAnalysis?.SafetyFactor
                    },
                    validationReport = new
                    {
                        overallAccuracy = analysisResult.ValidationReport?.OverallAccuracy,
                        confidenceLevel = analysisResult.ValidationReport?.ConfidenceLevel
                    }
                });

                // Only complete if still Running so a concurrent cancel is preserved.
                var completed = await context.EngineSimulations
                    .Where(s => s.Id == simulationId && s.Status == "Running")
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(s => s.Status, "Completed")
                            .SetProperty(s => s.CompletedAt, completedAt)
                            .SetProperty(s => s.ExecutionTimeSeconds, executionTime)
                            .SetProperty(s => s.Accuracy, accuracy)
                            .SetProperty(s => s.Iterations, iterations)
                            .SetProperty(s => s.ConvergenceRate, convergenceRate)
                            .SetProperty(s => s.ResultsJson, resultsJson),
                        cancellationToken);

                if (completed == 0)
                {
                    var currentStatus = await context.EngineSimulations
                        .AsNoTracking()
                        .Where(s => s.Id == simulationId)
                        .Select(s => s.Status)
                        .FirstOrDefaultAsync(cancellationToken);

                    _logger.LogInformation(
                        "Simulation {SimulationId} was {Status} before results were persisted; discarding completion",
                        simulationId,
                        currentStatus ?? "missing");
                    return;
                }

                _logger.LogInformation("Simulation {SimulationId} completed successfully in {ExecutionTime}s", 
                    simulationId, executionTime);
            }
            catch (OperationCanceledException)
            {
                await FailSimulationAsync(
                    context,
                    simulationId,
                    "Simulation cancelled during execution.",
                    markAsCancelled: true);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing simulation {SimulationId}", simulationId);
                await FailSimulationAsync(
                    context,
                    simulationId,
                    "Simulation failed. See server logs for details.");
            }
        }

        private static async Task FailSimulationAsync(
            HelloblueGKDbContext context,
            int simulationId,
            string errorMessage,
            bool markAsCancelled = false)
        {
            var completedAt = DateTime.UtcNow;
            var status = markAsCancelled ? "Cancelled" : "Failed";

            await context.EngineSimulations
                .Where(s => s.Id == simulationId &&
                            (s.Status == "Pending" || s.Status == "Running"))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.Status, status)
                    .SetProperty(s => s.CompletedAt, completedAt)
                    .SetProperty(s => s.ErrorMessage, errorMessage)
                    .SetProperty(s => s.StackTrace, (string?)null));
        }

        private string? GetCurrentUsername()
        {
            return User.Identity?.Name
                ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst("username")?.Value;
        }

        private bool CurrentUserCanAccessSimulation(EngineSimulation simulation)
        {
            if (User.IsInRole("Admin"))
            {
                return true;
            }

            var currentUsername = GetCurrentUsername();
            return !string.IsNullOrWhiteSpace(currentUsername) &&
                string.Equals(simulation.CreatedBy, currentUsername, StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetSafeErrorMessage(string status, string? errorMessage)
        {
            return string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(errorMessage)
                    ? "Simulation failed. See server logs for details."
                    : null;
        }
    }

    /// <summary>
    /// Request model for running a simulation
    /// </summary>
    public class RunSimulationRequest
    {
        /// <summary>
        /// Engine ID to run simulation for
        /// </summary>
        [Required]
        public int EngineId { get; set; }

        /// <summary>
        /// Type of simulation: CFD, Thermal, Structural, or MultiPhysics
        /// </summary>
        [Required]
        public string SimulationType { get; set; } = string.Empty;

        /// <summary>
        /// Optional simulation parameters
        /// </summary>
        public Dictionary<string, object>? Parameters { get; set; }
    }

    /// <summary>
    /// Safe simulation response that excludes internal diagnostics such as stack traces.
    /// </summary>
    public class EngineSimulationResponse
    {
        private const int MaxTelemetrySamples = 100;

        public int Id { get; set; }
        public int EngineId { get; set; }
        public string SimulationType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ParametersJson { get; set; }
        public string? ResultsJson { get; set; }
        public double? ExecutionTimeSeconds { get; set; }
        public int? Iterations { get; set; }
        public double? ConvergenceRate { get; set; }
        public double? Accuracy { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CreatedBy { get; set; }
        public EngineSummaryResponse? Engine { get; set; }
        public IEnumerable<EngineTelemetryResponse>? Telemetry { get; set; }

        public static EngineSimulationResponse FromEntity(EngineSimulation simulation, bool includeTelemetry = false)
        {
            return new EngineSimulationResponse
            {
                Id = simulation.Id,
                EngineId = simulation.EngineId,
                SimulationType = simulation.SimulationType,
                Status = simulation.Status,
                ParametersJson = simulation.ParametersJson,
                ResultsJson = simulation.ResultsJson,
                ExecutionTimeSeconds = simulation.ExecutionTimeSeconds,
                Iterations = simulation.Iterations,
                ConvergenceRate = simulation.ConvergenceRate,
                Accuracy = simulation.Accuracy,
                ErrorMessage = GetSafeErrorMessage(simulation.Status, simulation.ErrorMessage),
                CreatedAt = simulation.CreatedAt,
                StartedAt = simulation.StartedAt,
                CompletedAt = simulation.CompletedAt,
                CreatedBy = simulation.CreatedBy,
                Engine = simulation.Engine == null ? null : EngineSummaryResponse.FromEntity(simulation.Engine),
                Telemetry = includeTelemetry
                    ? simulation.Telemetry
                        .OrderByDescending(telemetry => telemetry.Timestamp)
                        .Take(MaxTelemetrySamples)
                        .Select(EngineTelemetryResponse.FromEntity)
                        .ToList()
                    : null
            };
        }

        private static string? GetSafeErrorMessage(string status, string? errorMessage)
        {
            return string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(errorMessage)
                    ? "Simulation failed. See server logs for details."
                    : null;
        }
    }

    public class EngineTelemetryResponse
    {
        public int Id { get; set; }
        public int SimulationId { get; set; }
        public DateTime Timestamp { get; set; }
        public double? Thrust { get; set; }
        public double? ChamberPressure { get; set; }
        public double? Temperature { get; set; }
        public double? MassFlowRate { get; set; }
        public double? Efficiency { get; set; }
        public double? SpecificImpulse { get; set; }
        public string? MetricsJson { get; set; }

        public static EngineTelemetryResponse FromEntity(HB_NLP_Research_Lab.WebAPI.Data.Models.EngineTelemetry telemetry)
        {
            return new EngineTelemetryResponse
            {
                Id = telemetry.Id,
                SimulationId = telemetry.SimulationId,
                Timestamp = telemetry.Timestamp,
                Thrust = telemetry.Thrust,
                ChamberPressure = telemetry.ChamberPressure,
                Temperature = telemetry.Temperature,
                MassFlowRate = telemetry.MassFlowRate,
                Efficiency = telemetry.Efficiency,
                SpecificImpulse = telemetry.SpecificImpulse,
                MetricsJson = telemetry.MetricsJson
            };
        }
    }

    public class EngineSummaryResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EngineType { get; set; } = string.Empty;

        public static EngineSummaryResponse FromEntity(Engine engine)
        {
            return new EngineSummaryResponse
            {
                Id = engine.Id,
                Name = engine.Name,
                EngineType = engine.EngineType
            };
        }
    }
}

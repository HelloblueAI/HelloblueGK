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

        public AIOptimizationController(
            HelloblueGKDbContext context,
            AdvancedAIOptimizationEngine optimizationEngine,
            ILogger<AIOptimizationController> logger)
        {
            _context = context;
            _optimizationEngine = optimizationEngine;
            _logger = logger;
        }

        /// <summary>
        /// Get all optimization runs
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<AIOptimizationRun>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllOptimizations([FromQuery] int? engineId = null)
        {
            try
            {
                var query = _context.AIOptimizationRuns
                    .Include(o => o.Engine)
                    .AsQueryable();

                if (engineId.HasValue)
                {
                    query = query.Where(o => o.EngineId == engineId.Value);
                }

                var optimizations = await query
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return Ok(optimizations);
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
        [ProducesResponseType(typeof(AIOptimizationRun), StatusCodes.Status200OK)]
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

                return Ok(optimization);
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

                // Create optimization run record
                var optimization = new AIOptimizationRun
                {
                    EngineId = request.EngineId,
                    AlgorithmType = request.AlgorithmType,
                    Status = "Pending",
                    ParametersJson = JsonSerializer.Serialize(request.Parameters ?? new Dictionary<string, object>()),
                    CreatedAt = DateTime.UtcNow
                };

                _context.AIOptimizationRuns.Add(optimization);
                await _context.SaveChangesAsync();

                // Run optimization asynchronously
                _ = Task.Run(async () => await ExecuteOptimizationAsync(optimization.Id, engine, request));

                return CreatedAtAction(nameof(GetOptimizationById), new { id = optimization.Id }, optimization);
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
                    errorMessage = optimization.ErrorMessage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving optimization status {OptimizationId}", id);
                return StatusCode(500, "An error occurred while retrieving optimization status");
            }
        }

        private async Task ExecuteOptimizationAsync(int optimizationId, Engine engine, StartOptimizationRequest request)
        {
            try
            {
                var optimization = await _context.AIOptimizationRuns.FindAsync(optimizationId);
                if (optimization == null) return;

                optimization.Status = "Running";
                optimization.StartedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

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
                var improvement = ((optimizedEfficiency - originalEfficiency) / originalEfficiency) * 100;

                // Update optimization with results
                optimization.Status = "Completed";
                optimization.CompletedAt = DateTime.UtcNow;
                optimization.ExecutionTimeSeconds = executionTime;
                optimization.ImprovementPercentage = improvement;
                optimization.Generations = result.GenerationsCompleted ?? 100;
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
                    generations = result.GenerationsCompleted
                };

                optimization.ResultsJson = JsonSerializer.Serialize(results);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Optimization {OptimizationId} completed: {Improvement}% improvement", 
                    optimizationId, improvement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing optimization {OptimizationId}", optimizationId);

                var optimization = await _context.AIOptimizationRuns.FindAsync(optimizationId);
                if (optimization != null)
                {
                    optimization.Status = "Failed";
                    optimization.CompletedAt = DateTime.UtcNow;
                    optimization.ErrorMessage = ex.Message;
                    await _context.SaveChangesAsync();
                }
            }
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

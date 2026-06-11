using Microsoft.AspNetCore.Mvc;
using HB_NLP_Research_Lab.WebAPI.Data.Repositories;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;

namespace HB_NLP_Research_Lab.WebAPI.Controllers
{
    /// <summary>
    /// Controller for managing aerospace engine configurations
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Tags("Engines")]
    public class EnginesController : ControllerBase
    {
        private readonly IEngineRepository _engineRepository;
        private readonly ILogger<EnginesController> _logger;

        public EnginesController(IEngineRepository engineRepository, ILogger<EnginesController> logger)
        {
            _engineRepository = engineRepository;
            _logger = logger;
        }

        /// <summary>
        /// Get all engines
        /// </summary>
        /// <returns>List of all engines</returns>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<Engine>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllEngines()
        {
            try
            {
                var engines = await _engineRepository.GetAllAsync();
                return Ok(engines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving engines");
                return StatusCode(500, "An error occurred while retrieving engines");
            }
        }

        /// <summary>
        /// Get active engines only
        /// </summary>
        /// <returns>List of active engines</returns>
        [HttpGet("active")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<Engine>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActiveEngines()
        {
            try
            {
                var engines = await _engineRepository.GetActiveEnginesAsync();
                return Ok(engines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active engines");
                return StatusCode(500, "An error occurred while retrieving active engines");
            }
        }

        /// <summary>
        /// Get engine by ID
        /// </summary>
        /// <param name="id">Engine ID</param>
        /// <returns>Engine details</returns>
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(Engine), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEngineById(int id)
        {
            try
            {
                var engine = await _engineRepository.GetByIdAsync(id);
                if (engine == null)
                {
                    return NotFound(new { message = $"Engine with ID {id} not found" });
                }
                return Ok(engine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving engine {EngineId}", id);
                return StatusCode(500, "An error occurred while retrieving the engine");
            }
        }

        /// <summary>
        /// Get engine by name
        /// </summary>
        /// <param name="name">Engine name</param>
        /// <returns>Engine details</returns>
        [HttpGet("name/{name}")]
        [Authorize]
        [ProducesResponseType(typeof(Engine), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEngineByName(string name)
        {
            try
            {
                var engine = await _engineRepository.GetByNameAsync(name);
                if (engine == null)
                {
                    return NotFound(new { message = $"Engine with name '{name}' not found" });
                }
                return Ok(engine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving engine {EngineName}", name);
                return StatusCode(500, "An error occurred while retrieving the engine");
            }
        }

        /// <summary>
        /// Create a new engine
        /// </summary>
        /// <param name="engine">Engine data</param>
        /// <returns>Created engine</returns>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(Engine), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateEngine([FromBody] Engine engine)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var createdEngine = await _engineRepository.CreateAsync(engine);
                return CreatedAtAction(nameof(GetEngineById), new { id = createdEngine.Id }, createdEngine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating engine");
                return StatusCode(500, "An error occurred while creating the engine");
            }
        }

        /// <summary>
        /// Update an existing engine
        /// </summary>
        /// <param name="id">Engine ID</param>
        /// <param name="request">Updated engine fields</param>
        /// <returns>Updated engine</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(Engine), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateEngine(int id, [FromBody] UpdateEngineRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "Engine update request is required" });
                }

                if (request.Id.HasValue && id != request.Id.Value)
                {
                    return BadRequest(new { message = "Engine ID mismatch" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var engine = await _engineRepository.GetByIdAsync(id);
                if (engine == null)
                {
                    return NotFound(new { message = $"Engine with ID {id} not found" });
                }

                request.ApplyTo(engine);
                var updatedEngine = await _engineRepository.UpdateAsync(engine);
                return Ok(updatedEngine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating engine {EngineId}", id);
                return StatusCode(500, "An error occurred while updating the engine");
            }
        }

        /// <summary>
        /// Delete an engine
        /// </summary>
        /// <param name="id">Engine ID</param>
        /// <returns>Deletion status</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEngine(int id)
        {
            try
            {
                var deleted = await _engineRepository.DeleteAsync(id);
                if (!deleted)
                {
                    return NotFound(new { message = $"Engine with ID {id} not found" });
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting engine {EngineId}", id);
                return StatusCode(500, "An error occurred while deleting the engine");
            }
        }
    }

    /// <summary>
    /// Request model for updating mutable engine fields without overwriting omitted values.
    /// </summary>
    public class UpdateEngineRequest
    {
        /// <summary>
        /// Optional body ID. When supplied, it must match the route ID.
        /// </summary>
        public int? Id { get; set; }

        [MinLength(1)]
        [MaxLength(200)]
        public string? Name { get; set; }

        [MinLength(1)]
        [MaxLength(100)]
        public string? EngineType { get; set; }

        public double? Thrust { get; set; }
        public double? SpecificImpulse { get; set; }
        public double? ChamberPressure { get; set; }
        public double? ExpansionRatio { get; set; }
        public double? Efficiency { get; set; }
        public string? Propellant { get; set; }
        public double? MixtureRatio { get; set; }
        public double? MassFlowRate { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }

        public void ApplyTo(Engine engine)
        {
            if (Name != null)
            {
                engine.Name = Name;
            }

            if (EngineType != null)
            {
                engine.EngineType = EngineType;
            }

            if (Thrust.HasValue)
            {
                engine.Thrust = Thrust.Value;
            }

            if (SpecificImpulse.HasValue)
            {
                engine.SpecificImpulse = SpecificImpulse.Value;
            }

            if (ChamberPressure.HasValue)
            {
                engine.ChamberPressure = ChamberPressure.Value;
            }

            if (ExpansionRatio.HasValue)
            {
                engine.ExpansionRatio = ExpansionRatio.Value;
            }

            if (Efficiency.HasValue)
            {
                engine.Efficiency = Efficiency.Value;
            }

            if (Propellant != null)
            {
                engine.Propellant = Propellant;
            }

            if (MixtureRatio.HasValue)
            {
                engine.MixtureRatio = MixtureRatio.Value;
            }

            if (MassFlowRate.HasValue)
            {
                engine.MassFlowRate = MassFlowRate.Value;
            }

            if (Description != null)
            {
                engine.Description = Description;
            }

            if (IsActive.HasValue)
            {
                engine.IsActive = IsActive.Value;
            }
        }
    }
}

using HB_NLP_Research_Lab.WebAPI.Data.Models;

namespace HB_NLP_Research_Lab.WebAPI.Data.Repositories;

/// <summary>
/// Repository interface for engine data operations
/// </summary>
public interface IEngineRepository
{
    Task<Engine?> GetByIdAsync(int id);
    Task<Engine?> GetByNameAsync(string name);
    Task<IEnumerable<Engine>> GetAllAsync();
    Task<IEnumerable<Engine>> GetActiveEnginesAsync();
    Task<Engine> CreateAsync(Engine engine);
    Task<Engine> UpdateAsync(Engine engine);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}


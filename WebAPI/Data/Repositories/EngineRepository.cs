using HB_NLP_Research_Lab.WebAPI.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace HB_NLP_Research_Lab.WebAPI.Data.Repositories;

/// <summary>
/// Repository implementation for engine data operations
/// </summary>
public class EngineRepository : IEngineRepository
{
    private readonly HelloblueGKDbContext _context;

    public EngineRepository(HelloblueGKDbContext context)
    {
        _context = context;
    }

    public async Task<Engine?> GetByIdAsync(int id)
    {
        return await _context.Engines
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Engine?> GetByNameAsync(string name)
    {
        return await _context.Engines
            .FirstOrDefaultAsync(e => e.Name == name);
    }

    public async Task<IEnumerable<Engine>> GetAllAsync(string? currentUsername, bool isAdmin, int skip, int take)
    {
        return await ApplyAccessFilter(_context.Engines.AsQueryable(), currentUsername, isAdmin)
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IEnumerable<Engine>> GetActiveEnginesAsync(string? currentUsername, bool isAdmin, int skip, int take)
    {
        return await ApplyAccessFilter(_context.Engines.AsQueryable(), currentUsername, isAdmin)
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<Engine> CreateAsync(Engine engine)
    {
        engine.CreatedAt = DateTime.UtcNow;
        _context.Engines.Add(engine);
        await _context.SaveChangesAsync();
        return engine;
    }

    public async Task<Engine> UpdateAsync(Engine engine)
    {
        engine.UpdatedAt = DateTime.UtcNow;
        _context.Engines.Update(engine);
        await _context.SaveChangesAsync();
        return engine;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var engine = await _context.Engines.FindAsync(id);
        if (engine == null)
            return false;

        _context.Engines.Remove(engine);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Engines.AnyAsync(e => e.Id == id);
    }

    private static IQueryable<Engine> ApplyAccessFilter(
        IQueryable<Engine> query,
        string? currentUsername,
        bool isAdmin)
    {
        if (isAdmin)
        {
            return query;
        }

        if (string.IsNullOrWhiteSpace(currentUsername))
        {
            return query.Where(_ => false);
        }

        return query.Where(e => e.CreatedBy == null || e.CreatedBy == string.Empty || e.CreatedBy == currentUsername);
    }
}


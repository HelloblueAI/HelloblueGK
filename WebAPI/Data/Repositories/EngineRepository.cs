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
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        var normalized = trimmed.ToLowerInvariant();
        var candidates = await _context.Engines
            .Where(e => e.Name.ToLower() == normalized)
            .ToListAsync();

        return candidates.FirstOrDefault(e =>
                   string.Equals(e.Name, trimmed, StringComparison.Ordinal))
               ?? candidates.FirstOrDefault();
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
        engine.Name = NormalizeUniqueName(engine.Name);
        await EnsureNameIsUniqueAsync(engine.Name);
        engine.CreatedAt = DateTime.UtcNow;
        _context.Engines.Add(engine);
        await SaveUniqueNameAsync(engine);
        return engine;
    }

    public async Task<Engine> UpdateAsync(Engine engine)
    {
        engine.Name = NormalizeUniqueName(engine.Name);
        await EnsureNameIsUniqueAsync(engine.Name, engine.Id);
        engine.UpdatedAt = DateTime.UtcNow;
        _context.Engines.Update(engine);
        await SaveUniqueNameAsync(engine);
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

    private static string NormalizeUniqueName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Engine name is required.", nameof(name));
        }

        return name.Trim();
    }

    private async Task EnsureNameIsUniqueAsync(string name, int? excludeId = null)
    {
        var normalized = name.ToLowerInvariant();
        var taken = await _context.Engines.AnyAsync(engine =>
            engine.Name.ToLower() == normalized &&
            (!excludeId.HasValue || engine.Id != excludeId.Value));

        if (taken)
        {
            throw new InvalidOperationException($"An engine named '{name}' already exists.");
        }
    }

    private async Task SaveUniqueNameAsync(Engine engine)
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _context.Entry(engine).State = EntityState.Detached;
            throw new InvalidOperationException($"An engine named '{engine.Name}' already exists.", ex);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner != null; inner = inner.InnerException)
        {
            var message = inner.Message;
            if (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("IX_Engines_Name", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}


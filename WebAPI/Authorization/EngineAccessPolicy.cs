using System.Security.Claims;
using HB_NLP_Research_Lab.WebAPI.Data.Models;

namespace HB_NLP_Research_Lab.WebAPI.Authorization;

public static class EngineAccessPolicy
{
    public static bool CanUseEngine(ClaimsPrincipal user, Engine engine, string? currentUsername)
    {
        if (user.IsInRole("Admin"))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(currentUsername))
        {
            return false;
        }

        // Seeded catalog engines are shared. User-created engines remain owner-scoped.
        return string.IsNullOrWhiteSpace(engine.CreatedBy) ||
            string.Equals(engine.CreatedBy, currentUsername, StringComparison.OrdinalIgnoreCase);
    }
}

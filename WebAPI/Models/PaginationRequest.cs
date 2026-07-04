namespace HB_NLP_Research_Lab.WebAPI.Models;

/// <summary>
/// Shared pagination guard for list endpoints that can otherwise load large tables.
/// </summary>
public sealed class PaginationRequest
{
    public const int DefaultSkip = 0;
    public const int DefaultTake = 100;
    public const int MaxTake = 100;

    public int Skip { get; init; } = DefaultSkip;
    public int Take { get; init; } = DefaultTake;

    public static PaginationRequest Create(int skip, int take)
    {
        return new PaginationRequest { Skip = skip, Take = take };
    }

    public bool TryValidate(out string message)
    {
        if (Skip < 0)
        {
            message = "skip must be greater than or equal to 0";
            return false;
        }

        if (Take < 1 || Take > MaxTake)
        {
            message = $"take must be between 1 and {MaxTake}";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

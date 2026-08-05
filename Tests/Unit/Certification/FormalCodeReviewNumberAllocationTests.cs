using HB_NLP_Research_Lab.Certification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.Certification;

public class FormalCodeReviewNumberAllocationTests
{
    [Fact]
    public async Task CreateReviewAsync_AllocatesMonotonicReviewNumbers()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var first = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/Engine.cs",
            FunctionName = "Ignite",
            LineStart = 1,
            LineEnd = 20,
            Author = "alice"
        });

        var second = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/Throttle.cs",
            FunctionName = "SetThrottle",
            LineStart = 10,
            LineEnd = 40,
            Author = "bob"
        });

        var year = DateTime.UtcNow.Year;
        first.ReviewNumber.Should().Be($"CR-{year}-0001");
        second.ReviewNumber.Should().Be($"CR-{year}-0002");
    }

    [Fact]
    public async Task CreateReviewAsync_ContinuesAfterGapInSequence()
    {
        await using var context = CreateContext();
        var year = DateTime.UtcNow.Year;

        context.CodeReviews.Add(new CodeReview
        {
            Id = Guid.NewGuid(),
            ReviewNumber = $"CR-{year}-0007",
            FilePath = "Core/Existing.cs",
            FunctionName = "Run",
            LineStart = 1,
            LineEnd = 2,
            Author = "seed",
            CreatedAt = DateTime.UtcNow,
            Status = CodeReviewStatus.Pending
        });
        await context.SaveChangesAsync();

        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/Next.cs",
            FunctionName = "Next",
            LineStart = 1,
            LineEnd = 5,
            Author = "alice"
        });

        created.ReviewNumber.Should().Be($"CR-{year}-0008");
    }

    private static CodeReviewDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CodeReviewDbContext>()
            .UseSqlite($"Data Source=file:code-review-alloc-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;

        var context = new CodeReviewDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }
}

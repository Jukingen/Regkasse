using KasseAPI_Final.Data;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseKnowledgeBaseServiceTests
{
    [Fact]
    public async Task EnsureSeed_ProvidesPopularAndFaq()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var popular = await svc.GetPopularArticlesAsync(10);
        var faq = await svc.GetFaqArticlesAsync();

        Assert.NotEmpty(popular);
        Assert.All(popular, a => Assert.False(a.IsFaq));
        Assert.NotEmpty(faq);
        Assert.All(faq, a => Assert.True(a.IsFaq));
        Assert.True(await db.TseKnowledgeArticles.CountAsync() >= popular.Count + faq.Count);
    }

    [Fact]
    public async Task SearchArticlesAsync_FiltersByQuery()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var hits = await svc.SearchArticlesAsync("failover");
        Assert.NotEmpty(hits);
        Assert.Contains(hits, a => a.Slug.Contains("failover", StringComparison.OrdinalIgnoreCase)
                                   || a.Title.Contains("failover", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetArticleAsync_IncrementsViewCount()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        var popular = await svc.GetPopularArticlesAsync(1);
        var id = popular[0].Id;
        var before = popular[0].ViewCount;

        var viewed = await svc.GetArticleAsync(id);
        Assert.Equal(before + 1, viewed.ViewCount);
        Assert.False(string.IsNullOrWhiteSpace(viewed.Body));
    }

    [Fact]
    public async Task SubmitFeedbackAsync_UpdatesAverage()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        var id = (await svc.GetPopularArticlesAsync(1))[0].Id;

        var feedback = await svc.SubmitFeedbackAsync(id, 5, "admin");
        Assert.Equal(5, feedback.Rating);
        Assert.Equal(5, feedback.ArticleAverageRating);
        Assert.Equal(1, feedback.ArticleRatingCount);

        feedback = await svc.SubmitFeedbackAsync(id, 3, "admin");
        Assert.Equal(4, feedback.ArticleAverageRating);
        Assert.Equal(2, feedback.ArticleRatingCount);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_RejectsOutOfRange()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        var id = (await svc.GetPopularArticlesAsync(1))[0].Id;

        await Assert.ThrowsAsync<ArgumentException>(() => svc.SubmitFeedbackAsync(id, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SubmitFeedbackAsync(id, 6));
    }

    private static TseKnowledgeBaseService CreateService(AppDbContext db) =>
        new(db, NullLogger<TseKnowledgeBaseService>.Instance);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_knowledge_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }
}

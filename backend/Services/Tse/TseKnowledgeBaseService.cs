using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Searches and rates seeded TSE operational knowledge articles. Never mutates fiscal data.
/// </summary>
public sealed class TseKnowledgeBaseService : ITseKnowledgeBaseService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TseKnowledgeBaseService> _logger;

    public TseKnowledgeBaseService(AppDbContext db, ILogger<TseKnowledgeBaseService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TseKnowledgeArticleDto>> SearchArticlesAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

        var q = (query ?? string.Empty).Trim();
        var articles = _db.TseKnowledgeArticles.AsNoTracking()
            .Where(a => a.IsPublished && !a.IsFaq);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = q.ToLowerInvariant();
            articles = articles.Where(a =>
                a.Title.ToLower().Contains(like)
                || a.Description.ToLower().Contains(like)
                || a.Body.ToLower().Contains(like)
                || a.Category.ToLower().Contains(like)
                || a.Slug.ToLower().Contains(like));
        }

        var list = await articles
            .OrderByDescending(a => a.ViewCount)
            .ThenBy(a => a.SortOrder)
            .ThenBy(a => a.Title)
            .Take(50)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return list.Select(Map).ToList();
    }

    public async Task<TseKnowledgeArticleDto> GetArticleAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        if (articleId == Guid.Empty)
            throw new ArgumentException("articleId is required.", nameof(articleId));

        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

        var article = await _db.TseKnowledgeArticles
            .FirstOrDefaultAsync(a => a.Id == articleId && a.IsPublished, cancellationToken)
            .ConfigureAwait(false);
        if (article is null)
            throw new KeyNotFoundException($"Knowledge article {articleId} was not found.");

        article.ViewCount += 1;
        article.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "TSE knowledge article viewed ArticleId={ArticleId} Slug={Slug} Views={Views}",
            article.Id,
            article.Slug,
            article.ViewCount);

        return Map(article);
    }

    public async Task<TseKnowledgeArticleFeedbackDto> SubmitFeedbackAsync(
        Guid articleId,
        int rating,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (articleId == Guid.Empty)
            throw new ArgumentException("articleId is required.", nameof(articleId));
        if (rating is < 1 or > 5)
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(rating));

        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

        var article = await _db.TseKnowledgeArticles
            .FirstOrDefaultAsync(a => a.Id == articleId && a.IsPublished, cancellationToken)
            .ConfigureAwait(false);
        if (article is null)
            throw new KeyNotFoundException($"Knowledge article {articleId} was not found.");

        var now = DateTime.UtcNow;
        var feedback = new TseKnowledgeFeedback
        {
            Id = Guid.NewGuid(),
            ArticleId = articleId,
            Rating = rating,
            ActorUserId = Truncate(actorUserId, 450),
            CreatedAt = now,
        };

        article.RatingSum += rating;
        article.RatingCount += 1;
        article.UpdatedAt = now;

        _db.TseKnowledgeFeedback.Add(feedback);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TseKnowledgeArticleFeedbackDto
        {
            ArticleId = articleId,
            FeedbackId = feedback.Id,
            Rating = rating,
            ArticleAverageRating = article.AverageRating,
            ArticleRatingCount = article.RatingCount,
            SubmittedAt = now,
        };
    }

    public async Task<IReadOnlyList<TseKnowledgeArticleDto>> GetPopularArticlesAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

        var take = Math.Clamp(limit, 1, 50);
        var list = await _db.TseKnowledgeArticles.AsNoTracking()
            .Where(a => a.IsPublished && !a.IsFaq)
            .OrderByDescending(a => a.ViewCount)
            .ThenByDescending(a => a.RatingCount > 0 ? (double)a.RatingSum / a.RatingCount : 0)
            .ThenBy(a => a.SortOrder)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return list.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<TseKnowledgeArticleDto>> GetFaqArticlesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

        var list = await _db.TseKnowledgeArticles.AsNoTracking()
            .Where(a => a.IsPublished && a.IsFaq)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return list.Select(Map).ToList();
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (await _db.TseKnowledgeArticles.AsNoTracking().AnyAsync(cancellationToken).ConfigureAwait(false))
            return;

        var now = DateTime.UtcNow;
        var seeds = BuildSeedArticles(now);
        _db.TseKnowledgeArticles.AddRange(seeds);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Seeded {Count} TSE knowledge articles", seeds.Count);
    }

    private static List<TseKnowledgeArticle> BuildSeedArticles(DateTime now) =>
    [
        Article(
            "tse-health-overview",
            "TSE device health overview",
            "How health scores, statuses, and probes relate to Super Admin monitoring.",
            "Health scores are operational indicators from periodic probes. They do not rewrite fiscal signatures. Use Health and Failover pages to inspect devices; degraded scores trigger diagnostic workflows only.",
            TseKnowledgeCategories.Health,
            isFaq: false,
            sort: 10,
            now,
            views: 42),
        Article(
            "tse-failover-basics",
            "Primary / backup failover basics",
            "When automatic or manual failover is appropriate for cloud TSE fleets.",
            "Failover switches signing traffic to a healthy backup device when the primary is unhealthy. Prefer manual review in production. Auto-healing may call failover only when AllowAutoFailover is enabled. Never use failover to alter DEP or Startbeleg data.",
            TseKnowledgeCategories.Failover,
            isFaq: false,
            sort: 20,
            now,
            views: 35),
        Article(
            "tse-auto-healing",
            "Auto-healing safe actions",
            "Which recoveries auto-healing may run without touching fiscal chains.",
            "Supported actions: re-probe health, clear transient ErrorMessage after a healthy probe, and optional failover. Auto-healing is disabled by default. Cooldown and max attempts limit thrashing.",
            TseKnowledgeCategories.Operations,
            isFaq: false,
            sort: 30,
            now,
            views: 28),
        Article(
            "tse-auto-scaling",
            "Auto-scaling recommendations",
            "How soft capacity recommendations differ from live cloud provisioning.",
            "Auto-scaling evaluates load vs policy and records recommendations. Soft provision stubs exist only in Development when AutoProvision is on. Production evaluations stay recommendation-only.",
            TseKnowledgeCategories.Operations,
            isFaq: false,
            sort: 40,
            now,
            views: 21),
        Article(
            "tse-getting-started",
            "Getting started with Super Admin TSE ops",
            "Entry points for fleet health, logs, incidents, and compliance reports.",
            "Start on TSE Management for the fleet overview, then Health / Failover for device state. Use Incidents for operational tickets and Compliance for readiness checks. Training modules offer Development-only failure drills.",
            TseKnowledgeCategories.GettingStarted,
            isFaq: false,
            sort: 5,
            now,
            views: 55),
        Article(
            "faq-what-is-health-score",
            "What does the TSE health score mean?",
            "FAQ: health score meaning",
            "The health score (0–100) summarizes recent probe success, connectivity, and certificate/memory signals. It is diagnostic only and is not a FinanzOnline or RKSV legal attestation.",
            TseKnowledgeCategories.Faq,
            isFaq: true,
            sort: 10,
            now),
        Article(
            "faq-does-healing-change-signatures",
            "Does auto-healing change fiscal signatures?",
            "FAQ: auto-healing and fiscal data",
            "No. Auto-healing never rewrites receipt chains, DEP exports, certificates, or Startbeleg material. It only runs operational recoveries (probe / clear error / optional failover).",
            TseKnowledgeCategories.Faq,
            isFaq: true,
            sort: 20,
            now),
        Article(
            "faq-offline-limit",
            "What is the offline transaction limit?",
            "FAQ: offline TSE capacity",
            "Per cash register, offline TSE intents are capped (default 50). POS should warn near 80% and block new offline fiscal transactions at the limit. This is separate from offline order snapshots.",
            TseKnowledgeCategories.Faq,
            isFaq: true,
            sort: 30,
            now),
        Article(
            "faq-who-can-access",
            "Who can access the TSE knowledge base?",
            "FAQ: access control",
            "Super Admin operators with system.critical. Mandanten-Admin and POS roles do not receive this platform knowledge surface by default.",
            TseKnowledgeCategories.Faq,
            isFaq: true,
            sort: 40,
            now),
    ];

    private static TseKnowledgeArticle Article(
        string slug,
        string title,
        string description,
        string body,
        string category,
        bool isFaq,
        int sort,
        DateTime now,
        int views = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Title = title,
            Description = description,
            Body = body,
            Category = category,
            IsFaq = isFaq,
            SortOrder = sort,
            IsPublished = true,
            ViewCount = views,
            CreatedAt = now,
        };

    private static TseKnowledgeArticleDto Map(TseKnowledgeArticle a) =>
        new()
        {
            Id = a.Id,
            Slug = a.Slug,
            Title = a.Title,
            Description = a.Description,
            Body = a.Body,
            Category = a.Category,
            IsFaq = a.IsFaq,
            ViewCount = a.ViewCount,
            Rating = a.AverageRating,
            RatingCount = a.RatingCount,
            SortOrder = a.SortOrder,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
            DiagnosticOnly = true,
        };

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}

using KasseAPI_Final.Services.AccountClosure;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.DataExport;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DataExportReadyEmailComposerTests
{
    [Fact]
    public void BuildSubject_IncludesTenantName()
    {
        var model = DataExportReadyEmailComposer.CreateModel(
            "Cafe Test",
            "https://api.regkasse.at/data/download/abc",
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        var subject = DataExportReadyEmailComposer.BuildSubject(model);

        Assert.Contains("Datenexport", subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cafe Test", subject, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtmlBody_ContainsDownloadLinkAndExpiry()
    {
        var model = DataExportReadyEmailComposer.CreateModel(
            "Cafe Test",
            "https://api.regkasse.at/data/download/tok123",
            new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc),
            validDays: 7,
            adminName: "Anna Admin");

        var html = DataExportReadyEmailComposer.BuildHtmlBody(model);

        Assert.Contains("Cafe Test", html, StringComparison.Ordinal);
        Assert.Contains("Anna Admin", html, StringComparison.Ordinal);
        Assert.Contains("https://api.regkasse.at/data/download/tok123", html, StringComparison.Ordinal);
        Assert.Contains("03.08.2026", html, StringComparison.Ordinal);
        Assert.Contains("#f6ffed", html, StringComparison.Ordinal);
        Assert.Contains("7 Tage", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPlainBody_ContainsDownloadLink()
    {
        var model = DataExportReadyEmailComposer.CreateModel(
            "Cafe Test",
            "https://example.test/dl",
            DateTime.UtcNow.Date.AddDays(7));
        var plain = DataExportReadyEmailComposer.BuildPlainBody(model);

        Assert.Contains("https://example.test/dl", plain, StringComparison.Ordinal);
        Assert.Contains("Cafe Test", plain, StringComparison.Ordinal);
    }
}

public sealed class AccountClosureConfirmationEmailComposerTests
{
    [Fact]
    public void BuildSubject_IncludesTenantName()
    {
        var model = AccountClosureConfirmationEmailComposer.CreateModel(
            "Cafe Test",
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            hasRksvData: true);
        var subject = AccountClosureConfirmationEmailComposer.BuildSubject(model);

        Assert.Contains("Kontoschließung", subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cafe Test", subject, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtmlBody_WithRksv_MentionsSevenYearRetention()
    {
        var model = AccountClosureConfirmationEmailComposer.CreateModel(
            "Cafe Test",
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            hasRksvData: true,
            adminName: "Max Manager");

        var html = AccountClosureConfirmationEmailComposer.BuildHtmlBody(model);

        Assert.Contains("Cafe Test", html, StringComparison.Ordinal);
        Assert.Contains("Max Manager", html, StringComparison.Ordinal);
        Assert.Contains("10.08.2026", html, StringComparison.Ordinal);
        Assert.Contains("7 Jahre", html, StringComparison.Ordinal);
        Assert.Contains("#fff1f0", html, StringComparison.Ordinal);
        Assert.Equal(DataDeletionService.ConfirmationWaitDays, model.ConfirmationWaitDays);
    }

    [Fact]
    public void BuildHtmlBody_WithoutRksv_MentionsFullDeletion()
    {
        var model = AccountClosureConfirmationEmailComposer.CreateModel(
            "Cafe Test",
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            hasRksvData: false);
        var html = AccountClosureConfirmationEmailComposer.BuildHtmlBody(model);

        Assert.Contains("keine RKSV", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bleiben mindestens 7 Jahre gespeichert", html, StringComparison.Ordinal);
    }
}

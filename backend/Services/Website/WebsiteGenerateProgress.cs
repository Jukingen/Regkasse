namespace KasseAPI_Final.Services.Website;

/// <summary>Stage tracking for website generation (FA progress UI).</summary>
public sealed class WebsiteGenerateProgress
{
    private readonly List<WebsiteGenerateProgressStep> _steps = [];

    public int Percent { get; private set; }
    public string Stage { get; private set; } = string.Empty;
    public IReadOnlyList<WebsiteGenerateProgressStep> Steps => _steps;

    public void Update(int percent, string stage)
    {
        Percent = Math.Clamp(percent, 0, 100);
        Stage = stage;
        _steps.Add(new WebsiteGenerateProgressStep(Percent, Stage));
    }
}

public sealed record WebsiteGenerateProgressStep(int Percent, string Stage);

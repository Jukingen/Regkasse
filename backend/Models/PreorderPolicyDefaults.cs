namespace KasseAPI_Final.Models;

public static class PreorderPolicyDefaults
{
    public const int PickupDeadlineWeeks = 4;
    public const int MinPickupDeadlineWeeks = 1;
    public const int MaxPickupDeadlineWeeks = 52;
    public const string CancellationPolicyText = "Keine Rücknahme von Artikeln!";
    public const decimal MoneyTolerance = 0.01m;

    public static int ClampWeeks(int weeks)
    {
        if (weeks < MinPickupDeadlineWeeks)
            return PickupDeadlineWeeks;
        return Math.Clamp(weeks, MinPickupDeadlineWeeks, MaxPickupDeadlineWeeks);
    }

    public static string NormalizePolicy(string? text) =>
        string.IsNullOrWhiteSpace(text) ? CancellationPolicyText : text.Trim();
}

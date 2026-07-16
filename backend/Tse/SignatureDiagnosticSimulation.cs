namespace KasseAPI_Final.Tse;

/// <summary>
/// In TSE simulation / demo mode, FakeTseProvider emits non-cryptographic pseudo-JWS.
/// Remap crypto FAIL steps to SIMULATED so admin UI does not treat expected test signatures as invalid.
/// </summary>
public static class SignatureDiagnosticSimulation
{
    public const string SimulatedStatus = "SIMULATED";

    public static IReadOnlyList<SignatureDiagnosticStep> ApplySimulationMode(
        IReadOnlyList<SignatureDiagnosticStep> steps,
        bool isTseSimulated,
        bool hasSignature)
    {
        if (!isTseSimulated || !hasSignature || steps.Count == 0)
            return steps;

        return steps
            .Select(s => s.Status == "FAIL"
                ? s with
                {
                    Status = SimulatedStatus,
                    Evidence = string.IsNullOrWhiteSpace(s.Evidence)
                        ? "TSE simulation mode — cryptographic verification not applicable (test only)."
                        : $"TSE simulation: {s.Evidence}"
                }
                : s)
            .ToList();
    }
}

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// İzole <c>pg_restore</c> denemesi sonucu (worker süreci; prod uygulama DB’sine yazmaz).
/// </summary>
public sealed class PgRestoreIsolatedRestoreOutcome
{
    public bool Success { get; init; }

    public int ExitCode { get; init; }

    public string? StdErrSnippet { get; init; }

    /// <summary>Oluşturulup silinen geçici DB adı (tanımlayıcı; host içermez).</summary>
    public string DatabaseName { get; init; } = string.Empty;
}

namespace KasseAPI_Final.Services.Activity;

/// <summary>SSE frame payload (<c>event</c> + JSON <c>data</c>).</summary>
public sealed record ActivityStreamMessage(string EventName, object? Data);

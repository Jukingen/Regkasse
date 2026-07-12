using KasseAPI_Final.DTOs;
using Npgsql;

namespace KasseAPI_Final.Services;

public sealed class ElmahErrorQueryService : IElmahErrorQueryService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ElmahErrorQueryService> _logger;

    public ElmahErrorQueryService(IConfiguration configuration, ILogger<ElmahErrorQueryService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ElmahErrorListResponseDto> ListAsync(
        string applicationName,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        var connectionString = RequireConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var items = new List<ElmahErrorListItemDto>();
        await using (var command = new NpgsqlCommand(
                         """
                         SELECT errorid, application, host, type, source, message, "user", statuscode, timeutc, allxml
                         FROM elmah_error
                         WHERE application = @application
                         ORDER BY sequence DESC
                         OFFSET @offset
                         LIMIT @limit
                         """,
                         connection))
        {
            command.Parameters.AddWithValue("application", applicationName);
            command.Parameters.AddWithValue("offset", offset);
            command.Parameters.AddWithValue("limit", pageSize);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(new ElmahErrorListItemDto
                {
                    ErrorId = reader.GetGuid(0),
                    Application = reader.GetString(1),
                    Host = reader.GetString(2),
                    Type = reader.GetString(3),
                    Source = reader.GetString(4),
                    Message = reader.GetString(5),
                    User = reader.IsDBNull(6) ? null : reader.GetString(6),
                    StatusCode = reader.GetInt32(7),
                    TimeUtc = reader.GetDateTime(8),
                    AllXml = reader.IsDBNull(9) ? null : reader.GetString(9),
                });
            }
        }

        int totalCount;
        await using (var countCommand = new NpgsqlCommand(
                         "SELECT COUNT(*) FROM elmah_error WHERE application = @application",
                         connection))
        {
            countCommand.Parameters.AddWithValue("application", applicationName);
            totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        }

        return new ElmahErrorListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
        };
    }

    public async Task<int> ClearAsync(string applicationName, CancellationToken cancellationToken = default)
    {
        var connectionString = RequireConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand(
            "DELETE FROM elmah_error WHERE application = @application",
            connection);
        command.Parameters.AddWithValue("application", applicationName);
        var deleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogWarning("Elmah errors cleared for application {ApplicationName}: {DeletedCount} rows.", applicationName, deleted);
        return deleted;
    }

    private string RequireConnectionString()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
        }

        return connectionString;
    }
}

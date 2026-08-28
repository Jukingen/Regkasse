using KasseAPI_Final.Migrations;
using Npgsql;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// PostgreSQL proofs for the ElmahCore <c>elmah_error."User"</c> repair (quoted PascalCase).
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class ElmahErrorTableEnsureTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public ElmahErrorTableEnsureTests(PostgreSqlReplayFixture fixture) =>
        _fixture = fixture;

    [SkippableFact]
    public async Task Migrate_CreatesElmahErrorUserColumn()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        Assert.True(await TableExistsAsync(connection));
        Assert.True(await HasQuotedUserColumnAsync(connection));
        Assert.False(await HasLowercaseUserColumnAsync(connection));
    }

    [SkippableFact]
    public async Task EnsureSql_IsIdempotent_WhenColumnAlreadyExists()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await ExecuteEnsureAsync(connection);
        await ExecuteEnsureAsync(connection);

        Assert.True(await HasQuotedUserColumnAsync(connection));
        Assert.False(await HasLowercaseUserColumnAsync(connection));
    }

    [SkippableFact]
    public async Task EnsureSql_AddsUserColumn_WhenTableExistsWithoutIt()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        await ExecuteAsync(connection, "DROP TABLE IF EXISTS elmah_error;", tx);
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE elmah_error
            (
                errorid UUID NOT NULL PRIMARY KEY,
                application VARCHAR(60) NOT NULL,
                host VARCHAR(50) NOT NULL,
                type VARCHAR(100) NOT NULL,
                source VARCHAR(60) NOT NULL,
                message VARCHAR(500) NOT NULL,
                statuscode INT NOT NULL,
                timeutc TIMESTAMP NOT NULL,
                sequence INT NOT NULL DEFAULT 1,
                allxml TEXT NOT NULL
            );
            """,
            tx);

        Assert.False(await HasQuotedUserColumnAsync(connection, tx));

        await ExecuteEnsureAsync(connection, tx);

        Assert.True(await HasQuotedUserColumnAsync(connection, tx));
        await InsertElmahCoreRowAsync(connection, tx, Guid.NewGuid(), user: "cashier1");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task EnsureSql_CreatesFullTable_WhenMissing()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        await ExecuteAsync(connection, "DROP TABLE IF EXISTS elmah_error;", tx);
        Assert.False(await TableExistsAsync(connection, tx));

        await ExecuteEnsureAsync(connection, tx);

        Assert.True(await TableExistsAsync(connection, tx));
        Assert.True(await HasQuotedUserColumnAsync(connection, tx));
        await InsertElmahCoreRowAsync(connection, tx, Guid.NewGuid(), user: string.Empty);

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task EnsureSql_RenamesLowercaseUserAndPreservesRows()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        await ExecuteAsync(connection, "DROP TABLE IF EXISTS elmah_error;", tx);
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE elmah_error
            (
                errorid UUID NOT NULL PRIMARY KEY,
                application VARCHAR(60) NOT NULL,
                host VARCHAR(50) NOT NULL,
                type VARCHAR(100) NOT NULL,
                source VARCHAR(60) NOT NULL,
                message VARCHAR(500) NOT NULL,
                "user" text NULL,
                statuscode INT NOT NULL,
                timeutc TIMESTAMP NOT NULL,
                sequence INT NOT NULL DEFAULT 1,
                allxml TEXT NOT NULL
            );
            """,
            tx);

        var errorId = Guid.NewGuid();
        await ExecuteAsync(
            connection,
            """
            INSERT INTO elmah_error (errorid, application, host, type, source, message, "user", statuscode, timeutc, allxml)
            VALUES (@id, 'Regkasse', 'host', 'System.Exception', 'test', 'boom', 'legacy-user', 500, NOW(), '<error/>');
            """,
            tx,
            cmd => cmd.Parameters.AddWithValue("id", errorId));

        await ExecuteEnsureAsync(connection, tx);

        Assert.True(await HasQuotedUserColumnAsync(connection, tx));
        Assert.False(await HasLowercaseUserColumnAsync(connection, tx));
        await using (var command = new NpgsqlCommand(
                         """SELECT "User" FROM elmah_error WHERE errorid = @id;""",
                         connection,
                         (NpgsqlTransaction)tx))
        {
            command.Parameters.AddWithValue("id", errorId);
            var value = await command.ExecuteScalarAsync();
            Assert.Equal("legacy-user", value);
        }

        await InsertElmahCoreRowAsync(connection, tx, Guid.NewGuid(), user: "new-user");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task EnsureSql_DropsLeftoverLowercaseUser_SoElmahCoreInsertSucceeds()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        await ExecuteAsync(connection, "DROP TABLE IF EXISTS elmah_error;", tx);
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE elmah_error
            (
                errorid UUID NOT NULL PRIMARY KEY,
                application VARCHAR(60) NOT NULL,
                host VARCHAR(50) NOT NULL,
                type VARCHAR(100) NOT NULL,
                source VARCHAR(60) NOT NULL,
                message VARCHAR(500) NOT NULL,
                "user" VARCHAR(50) NOT NULL,
                statuscode INT NOT NULL,
                timeutc TIMESTAMP NOT NULL,
                sequence INT NOT NULL DEFAULT 1,
                allxml TEXT NOT NULL
            );
            """,
            tx);
        await ExecuteAsync(
            connection,
            """ALTER TABLE elmah_error ADD COLUMN "User" text NULL;""",
            tx);

        await ExecuteEnsureAsync(connection, tx);

        Assert.True(await HasQuotedUserColumnAsync(connection, tx));
        Assert.False(await HasLowercaseUserColumnAsync(connection, tx));
        await InsertElmahCoreRowAsync(connection, tx, Guid.NewGuid(), user: "cashier1");

        await tx.RollbackAsync();
    }

    private static Task ExecuteEnsureAsync(NpgsqlConnection connection, NpgsqlTransaction? tx = null) =>
        ExecuteAsync(connection, AddElmahUserColumn.EnsureSql, tx);

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        NpgsqlTransaction? tx,
        Action<NpgsqlCommand>? configure = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, tx);
        configure?.Invoke(command);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertElmahCoreRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        Guid id,
        string user)
    {
        // Mirrors ElmahCore.Postgresql PgsqlErrorLog INSERT (quoted "User").
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO Elmah_Error (ErrorId, Application, Host, Type, Source, Message, "User", StatusCode, TimeUtc, AllXml)
            VALUES (@ErrorId, @Application, @Host, @Type, @Source, @Message, @User, @StatusCode, @TimeUtc, @AllXml);
            """,
            connection,
            tx);
        command.Parameters.AddWithValue("ErrorId", id);
        command.Parameters.AddWithValue("Application", "Regkasse");
        command.Parameters.AddWithValue("Host", "testhost");
        command.Parameters.AddWithValue("Type", "System.Exception");
        command.Parameters.AddWithValue("Source", "test");
        command.Parameters.AddWithValue("Message", "elmah insert");
        command.Parameters.AddWithValue("User", user);
        command.Parameters.AddWithValue("StatusCode", 500);
        command.Parameters.AddWithValue("TimeUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("AllXml", "<error />");
        await command.ExecuteNonQueryAsync();
    }

    private static Task<bool> TableExistsAsync(NpgsqlConnection connection, NpgsqlTransaction? tx = null) =>
        ExistsAsync(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name = 'elmah_error'
            );
            """,
            tx);

    private static Task<bool> HasQuotedUserColumnAsync(NpgsqlConnection connection, NpgsqlTransaction? tx = null) =>
        HasAttributeAsync(connection, "User", tx);

    private static Task<bool> HasLowercaseUserColumnAsync(NpgsqlConnection connection, NpgsqlTransaction? tx = null) =>
        HasAttributeAsync(connection, "user", tx);

    private static Task<bool> HasAttributeAsync(
        NpgsqlConnection connection,
        string attname,
        NpgsqlTransaction? tx) =>
        ExistsAsync(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_attribute a
                JOIN pg_class c ON c.oid = a.attrelid
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'public'
                  AND c.relname = 'elmah_error'
                  AND a.attname = @attname
                  AND a.attnum > 0
                  AND NOT a.attisdropped
            );
            """,
            tx,
            cmd => cmd.Parameters.AddWithValue("attname", attname));

    private static async Task<bool> ExistsAsync(
        NpgsqlConnection connection,
        string sql,
        NpgsqlTransaction? tx,
        Action<NpgsqlCommand>? configure = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, tx);
        configure?.Invoke(command);
        var result = await command.ExecuteScalarAsync();
        return result is true;
    }
}

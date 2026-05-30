using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PgRestoreListTableDataParserTests
{
    [Fact]
    public void ParseTableDataEntries_finds_public_table_data_lines()
    {
        const string stdout = """
            ;
            3875; 0 176717 TABLE DATA public products postgres
            3876; 0 176718 TABLE DATA public categories postgres
            100; 1259 176719 TABLE public audit_logs postgres
            """;

        var entries = PgRestoreListTableDataParser.ParseTableDataEntries(stdout);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.TableName == "products");
        Assert.Contains(entries, e => e.TableName == "categories");
    }

    [Fact]
    public void ParseTableDataEntries_deduplicates_same_table()
    {
        const string stdout = """
            1; 0 0 TABLE DATA public products postgres
            2; 0 0 TABLE DATA public products postgres
            """;

        var entries = PgRestoreListTableDataParser.ParseTableDataEntries(stdout);
        Assert.Single(entries);
    }
}

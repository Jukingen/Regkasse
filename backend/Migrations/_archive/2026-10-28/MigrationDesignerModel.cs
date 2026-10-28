using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Hand-written migrations were saved without a historical <c>BuildTargetModel</c>.
/// <see cref="MigrationDesignerModel"/> applies the current
/// <see cref="AppDbContextModelSnapshot"/> so data operations can resolve column types.
/// Per-migration historical models are not stored; see backend/docs/MIGRATION_SQUASH.md.
/// </summary>
internal static class MigrationDesignerModel
{
    public static void BuildCurrent(ModelBuilder modelBuilder)
    {
        new SnapshotForwarder().CopyTo(modelBuilder);
    }

    private sealed class SnapshotForwarder : AppDbContextModelSnapshot
    {
        public void CopyTo(ModelBuilder modelBuilder) => BuildModel(modelBuilder);
    }
}

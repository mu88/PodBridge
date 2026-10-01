using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PodBridge.Persistence;

// Used exclusively by EF Core design-time tooling (e.g. "dotnet ef migrations add"). Without this factory,
// the tooling falls back to building the whole host via PodBridge.Api's Program.cs top-level statements just
// to construct an AppDbContext for model inspection - which also runs MigrateDatabaseWithRetryAsync and the
// legacy podcast migration in the process (see Program.cs), wasting time (retry backoff) and risking real
// database writes for a purely design-time operation. The connection string content doesn't matter here:
// Npgsql is only used to pick the right provider/SQL dialect for the generated migration, no connection is
// ever opened.
// [ExcludeFromCodeCoverage]: only ever invoked by "dotnet ef" tooling, never by the running app or tests.
[ExcludeFromCodeCoverage]
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=design-time");

        return new AppDbContext(optionsBuilder.Options);
    }
}

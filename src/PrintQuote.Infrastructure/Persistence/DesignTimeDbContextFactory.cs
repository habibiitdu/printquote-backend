using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PrintQuote.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` build the model without the API's configuration.
/// `dotnet ef database update` should pass --connection or use the API as startup project.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PRINTQUOTE_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=printquote;Username=postgres";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}

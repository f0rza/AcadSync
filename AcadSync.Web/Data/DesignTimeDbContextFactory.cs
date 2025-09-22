using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace AcadSync.Web.Data
{
    // Design-time factory used by EF tooling so migrations can run without constructing the full application service provider
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AcadSyncWebDbContext>
    {
        public AcadSyncWebDbContext CreateDbContext(string[] args)
        {
            // Look for configuration in the AcadSync.Web folder
            var basePath = Directory.GetCurrentDirectory();
            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables();

            var config = builder.Build();

            var connectionString = config.GetConnectionString("AcadSyncAudit")
                                   ?? "Server=localhost;Database=AcadSyncAudit;Trusted_Connection=True;TrustServerCertificate=True;";

            var optionsBuilder = new DbContextOptionsBuilder<AcadSyncWebDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new AcadSyncWebDbContext(optionsBuilder.Options);
        }
    }
}

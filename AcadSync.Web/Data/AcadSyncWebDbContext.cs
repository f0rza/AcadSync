using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AcadSync.Web.Models;

namespace AcadSync.Web.Data
{
    public class AcadSyncWebDbContext : IdentityDbContext<IdentityUser>
    {
        public AcadSyncWebDbContext(DbContextOptions<AcadSyncWebDbContext> options)
            : base(options)
        {
        }

        public DbSet<RuleEntity> Rules { get; set; } = null!;
        public DbSet<RuleVersionEntity> RuleVersions { get; set; } = null!;
        public DbSet<WebRun> WebRuns { get; set; } = null!;
        public DbSet<RunLog> RunLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Put audit-related tables into acadsync schema to match existing naming
            builder.Entity<RuleEntity>(b =>
            {
                b.ToTable("Rules", schema: "acadsync");
                b.HasKey(x => x.Id);
                b.Property(x => x.Name).IsRequired().HasMaxLength(200);
                b.HasIndex(x => x.Slug).IsUnique();
                b.Property(x => x.RowVersion).IsRowVersion();
            });

            builder.Entity<RuleVersionEntity>(b =>
            {
                b.ToTable("RuleVersions", schema: "acadsync");
                b.HasKey(x => x.Id);
                b.Property(x => x.Yaml).HasColumnType("nvarchar(max)");
                b.HasOne(x => x.Rule)
                    .WithMany(r => r.Versions)
                    .HasForeignKey(x => x.RuleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<WebRun>(b =>
            {
                b.ToTable("WebRuns", schema: "acadsync");
                b.HasKey(x => x.Id);
                b.Property(x => x.Mode).HasMaxLength(50);
                b.Property(x => x.Status).HasMaxLength(50);
            });

            builder.Entity<RunLog>(b =>
            {
                b.ToTable("RunLogs", schema: "acadsync");
                b.HasKey(x => x.Id);
                b.Property(x => x.Level).HasMaxLength(20);
                b.HasOne(x => x.Run)
                    .WithMany(r => r.Logs)
                    .HasForeignKey(x => x.RunId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}

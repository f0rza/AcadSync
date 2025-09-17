using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using AcadSync.Audit.Extensions;
using AcadSync.Audit.Services;

namespace AcadSync.Audit.Tests
{
    [TestClass]
    public class ViolationExtensionsExtraTests
    {
        [TestMethod]
        public void ToAuditEntry_MissingOptionalProperties_UsesDefaults()
        {
            var violation = new
            {
                RuleId = "R1",
                EntityType = "Student",
                EntityId = 1L,
                PropertyCode = "P1"
            };

            var entry = violation.ToAuditEntry();

            entry.RuleId.Should().Be("R1");
            entry.EntityType.Should().Be("Student");
            entry.EntityId.Should().Be(1L);
            entry.PropertyCode.Should().Be("P1");
            entry.CurrentValue.Should().BeNull();
            entry.ProposedValue.Should().BeNull();
            entry.Action.Should().Be("");     // ViolationExtensions uses empty string default for Action
            entry.Severity.Should().Be("");   // and for Severity
        }

        [TestMethod]
        public void ToAuditEntry_NullValues_AreMappedToNull()
        {
            var violation = new
            {
                RuleId = "R2",
                EntityType = "Document",
                EntityId = 42L,
                PropertyCode = "C",
                CurrentValue = (string?)null,
                ProposedValue = (string?)null,
                Action = (string?)null,
                Severity = (string?)null
            };

            var entry = violation.ToAuditEntry();

            entry.RuleId.Should().Be("R2");
            entry.EntityType.Should().Be("Document");
            entry.EntityId.Should().Be(42L);
            entry.PropertyCode.Should().Be("C");
            entry.CurrentValue.Should().BeNull();
            entry.ProposedValue.Should().BeNull();
            entry.Action.Should().Be("");   // Action property in extension uses ?? "" when reading
            entry.Severity.Should().Be(""); // Severity likewise
        }

        [TestMethod]
        public void ToAuditEntry_NonLongEntityId_ThrowsInvalidCast_ForIncompatibleType()
        {
            var violation = new
            {
                RuleId = "R3",
                EntityType = "Student",
                // Int32 boxed; the extension casts (long)(GetValue(...) ?? 0L)
                EntityId = 7, 
                PropertyCode = "P2"
            };

            Action act = () => violation.ToAuditEntry();

            // Unboxing an int as a long will throw InvalidCastException
            act.Should().Throw<InvalidCastException>();
        }
    }

    [TestClass]
    public class DatabaseInitializationServicePrivateHelpersTests
    {
        [TestMethod]
        public void SplitSqlScript_SplitsOnStandaloneGo_CaseInsensitive()
        {
            var script = "SELECT 1\r\nGO\r\nSELECT 2\nGo\nSELECT 3\r\n  go  \r\nSELECT 4";
            var mi = typeof(DatabaseInitializationService).GetMethod("SplitSqlScript", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            var result = (List<string>)mi!.Invoke(null, new object[] { script });

            result.Should().HaveCount(4);
            result[0].Should().Contain("SELECT 1");
            result[1].Should().Contain("SELECT 2");
            result[2].Should().Contain("SELECT 3");
            result[3].Should().Contain("SELECT 4");
        }

        [TestMethod]
        public async Task LoadSqlScriptAsync_FileFallback_ReturnsFileContents()
        {
            // Arrange - place file under AppDomain.CurrentDomain.BaseDirectory/SqlScripts/CreateAuditDatabase.sql
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
            var scriptsDir = Path.Combine(baseDir, "SqlScripts");
            Directory.CreateDirectory(scriptsDir);
            var scriptPath = Path.Combine(scriptsDir, "CreateAuditDatabase.sql");
            var expectedContent = $"-- test script {Guid.NewGuid()}";
            await File.WriteAllTextAsync(scriptPath, expectedContent);

            try
            {
                var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
                var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<DatabaseInitializationService>>();
                var service = new DatabaseInitializationService(configMock.Object, loggerMock.Object);

                var mi = typeof(DatabaseInitializationService).GetMethod("LoadSqlScriptAsync", BindingFlags.NonPublic | BindingFlags.Instance);
                mi.Should().NotBeNull();

                var task = (Task<string>)mi!.Invoke(service, Array.Empty<object>())!;
                var content = await task;

                content.Should().Be(expectedContent);
            }
            finally
            {
                // Cleanup
                try
                {
                    if (File.Exists(scriptPath))
                        File.Delete(scriptPath);
                    if (Directory.Exists(scriptsDir) && Directory.GetFiles(scriptsDir).Length == 0)
                        Directory.Delete(scriptsDir);
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }

        [TestMethod]
        public void GetMasterConnectionString_SetsInitialCatalogToMaster()
        {
            var original = "Server=.;Database=MyDb;Integrated Security=true;";
            var mi = typeof(DatabaseInitializationService).GetMethod("GetMasterConnectionString", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            var result = (string)mi!.Invoke(null, new object[] { original });

            var lowered = result.ToLowerInvariant();
            (lowered.Contains("initial catalog=master") || lowered.Contains("database=master"))
                .Should().BeTrue();
        }

        [TestMethod]
        public void GetDatabaseNameFromConnectionString_ReturnsInitialCatalogOrFallback()
        {
            var withDb = "Server=.;Database=MyDb;Integrated Security=true;";
            var withoutDb = "Server=.;Integrated Security=true;";

            var mi = typeof(DatabaseInitializationService).GetMethod("GetDatabaseNameFromConnectionString", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            var name1 = (string)mi!.Invoke(null, new object[] { withDb });
            var name2 = (string)mi!.Invoke(null, new object[] { withoutDb });

            name1.Should().Be("MyDb");
            name2.Should().Be("AcadSyncAudit"); // fallback default as implemented
        }
    }
}

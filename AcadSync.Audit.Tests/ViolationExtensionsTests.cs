using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AcadSync.Audit.Extensions;
using AcadSync.Audit.Models;
using AcadSync.Audit.Repositories;
using AcadSync.Audit.Services;

namespace AcadSync.Audit.Tests;

[TestClass]
public class ViolationExtensionsTests
{
    [TestMethod]
    public void ToAuditEntry_MapsAllProperties()
    {
        var violation = new
        {
            RuleId = "RULE-1",
            EntityType = "Student",
            EntityId = 123L,
            PropertyCode = "PROP_CODE",
            CurrentValue = "old",
            ProposedValue = "new",
            Action = "flag:missing",
            Severity = "High"
        };

        var entry = violation.ToAuditEntry();

        entry.RuleId.Should().Be("RULE-1");
        entry.EntityType.Should().Be("Student");
        entry.EntityId.Should().Be(123L);
        entry.PropertyCode.Should().Be("PROP_CODE");
        entry.CurrentValue.Should().Be("old");
        entry.ProposedValue.Should().Be("new");
        entry.Action.Should().Be("flag:missing");
        entry.Severity.Should().Be("High");
    }
}

[TestClass]
public class AuditEntryTests
{
    [TestMethod]
    public void AuditEntry_ConstructedValues_AreAccessible()
    {
        var entry = new AuditEntry(
            "RULE-2",
            "Document",
            999,
            "CODE",
            "cv",
            "pv",
            "repair:apply",
            "Medium"
        );

        entry.RuleId.Should().Be("RULE-2");
        entry.EntityType.Should().Be("Document");
        entry.EntityId.Should().Be(999);
        entry.PropertyCode.Should().Be("CODE");
        entry.CurrentValue.Should().Be("cv");
        entry.ProposedValue.Should().Be("pv");
        entry.Action.Should().Be("repair:apply");
        entry.Severity.Should().Be("Medium");
    }
}

[TestClass]
public class DatabaseInitializationServiceTests
{
    [TestMethod]
    public async Task InitializeAsync_NoConnectionString_CompletesWithoutThrowing()
    {
        // Build an empty configuration (no connection strings)
        var config = new Mock<IConfiguration>().Object;
        var logger = new Mock<ILogger<DatabaseInitializationService>>().Object;

        var service = new DatabaseInitializationService(config, logger);

        Func<Task> act = async () => await service.InitializeAsync();

        await act.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task InitializeAsync_AutoInitDisabled_CompletesWithoutThrowing()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.SetupGet(c => c["ConnectionStrings:AcadSyncAudit"])
            .Returns("Server=(local);Database=AcadSyncAudit;Trusted_Connection=True;");
        configMock.SetupGet(c => c["AcadSync:AutoInitializeDatabase"])
            .Returns("false");
        var config = configMock.Object;
        var logger = new Mock<ILogger<DatabaseInitializationService>>().Object;

        var service = new DatabaseInitializationService(config, logger);

        Func<Task> act = async () => await service.InitializeAsync();

        await act.Should().NotThrowAsync();
    }
}

[TestClass]
public class AuditRepositoryTests
{
    [TestMethod]
    public void Constructor_NullConnectionString_ThrowsArgumentNullException()
    {
        Action act = () => _ = new AuditRepository(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

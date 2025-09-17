using Microsoft.Extensions.Logging;
using Moq;
using AcadSync.Processor.Services;
using AcadSync.Processor.Models.Domain;
using AcadSync.Processor.Models.Projections;
using FluentAssertions;

namespace AcadSync.Processor.Tests;

[TestClass]
public class RuleEngineTests
{
    private Mock<ILogger<RuleEngine>> _loggerMock;
    private RuleEngine _ruleEngine;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<RuleEngine>>();
        _ruleEngine = new RuleEngine(_loggerMock.Object);
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new RuleEngine(null!));
    }

    [TestMethod]
    public async Task EvaluateAsync_WithEmptyEntities_ReturnsEmptyViolations()
    {
        // Arrange
        var doc = CreateTestEprlDoc();
        var entities = new List<IEntityProjection>();

        // Act
        var result = await _ruleEngine.EvaluateAsync(doc, entities);

        // Assert
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task EvaluateAsync_WithNullDoc_ThrowsNullReferenceException()
    {
        // Arrange
        var entities = new List<IEntityProjection> { CreateTestEntity() };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<NullReferenceException>(
            () => _ruleEngine.EvaluateAsync(null!, entities));
    }

    [TestMethod]
    public async Task EvaluateAsync_WithNullEntities_ThrowsArgumentNullException()
    {
        // Arrange
        var doc = CreateTestEprlDoc();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            () => _ruleEngine.EvaluateAsync(doc, null!));
    }

    [TestMethod]
    public async Task EvaluateEntityAsync_WithMatchingRuleAndCondition_ReturnsViolations()
    {
        // Arrange
        var doc = CreateTestEprlDoc();
        var entity = CreateTestEntity();

        // Act
        var result = await _ruleEngine.EvaluateEntityAsync(doc, entity);

        // Assert
        result.Should().NotBeEmpty();
        result.First().RuleId.Should().Be("test-rule");
    }

    [TestMethod]
    public async Task EvaluateEntityAsync_WithNonMatchingEntityType_ReturnsEmptyViolations()
    {
        // Arrange
        var doc = CreateTestEprlDoc();
        var entity = CreateTestEntity("NonMatchingType", 1);

        // Act
        var result = await _ruleEngine.EvaluateEntityAsync(doc, entity);

        // Assert
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task EvaluateEntityAsync_WithNullDoc_ThrowsNullReferenceException()
    {
        // Arrange
        var entity = CreateTestEntity();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<NullReferenceException>(
            () => _ruleEngine.EvaluateEntityAsync(null!, entity));
    }

    [TestMethod]
    public async Task EvaluateEntityAsync_WithNullEntity_ThrowsNullReferenceException()
    {
        // Arrange
        var doc = CreateTestEprlDoc();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<NullReferenceException>(
            () => _ruleEngine.EvaluateEntityAsync(doc, null!));
    }

    private static EprlDoc CreateTestEprlDoc()
    {
        return new EprlDoc
        {
            Rules = new List<Rule>
            {
                new Rule
                {
                    Id = "test-rule",
                    Name = "Test Rule",
                    Scope = new Scope { Entity = "TestEntity" },
                    Requirements = new List<Requirement>
                    {
                        new Requirement
                        {
                            property = "TestProperty",
                            required = true,
                            constraints = new RequirementConstraints()
                        }
                    }
                }
            },
            Defaults = new Defaults()
        };
    }

    private static IEntityProjection CreateTestEntity(string entityType = "TestEntity", long entityId = 1)
    {
        var mock = new Mock<IEntityProjection>();
        return mock.SetupEntity(entityType, entityId, new Dictionary<string, string?>());
    }
}

public static class EntityProjectionExtensions
{
    public static IEntityProjection SetupEntity(
        this Mock<IEntityProjection> mock,
        string entityType,
        long entityId,
        Dictionary<string, string?> extProperties)
    {
        mock.Setup(x => x.EntityType).Returns(entityType);
        mock.Setup(x => x.EntityId).Returns(entityId);
        mock.Setup(x => x.Ext).Returns(extProperties);
        mock.Setup(x => x.ResolvePath(It.IsAny<string>())).Returns((string path) =>
        {
            // Simple path resolution for testing
            if (path.StartsWith("ext."))
            {
                var propCode = path.Substring(4);
                return extProperties.TryGetValue(propCode, out var value) ? value : null;
            }
            return null;
        });

        return mock.Object;
    }
}

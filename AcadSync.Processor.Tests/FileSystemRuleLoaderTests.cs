using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AcadSync.Processor.Services;
using AcadSync.Processor.Interfaces;
using AcadSync.Processor.Configuration;
using FluentAssertions;
using System.IO;

namespace AcadSync.Processor.Tests;

[TestClass]
public class FileSystemRuleLoaderTests
{
    private Mock<IOptions<ProcessorOptions>> _optionsMock;
    private Mock<ILogger<FileSystemRuleLoader>> _loggerMock;
    private ProcessorOptions _processorOptions;
    private FileSystemRuleLoader _ruleLoader;

    [TestInitialize]
    public void Setup()
    {
        _optionsMock = new Mock<IOptions<ProcessorOptions>>();
        _loggerMock = new Mock<ILogger<FileSystemRuleLoader>>();
        _processorOptions = new ProcessorOptions
        {
            RulesFilePath = "test-rules.yaml",
            Cache = new CacheOptions
            {
                EnableRuleCache = true,
                RuleCacheExpirationMinutes = 30
            }
        };

        _optionsMock.Setup(x => x.Value).Returns(_processorOptions);
        _ruleLoader = new FileSystemRuleLoader(_optionsMock.Object, _loggerMock.Object);
    }

    [TestMethod]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            new FileSystemRuleLoader(null!, _loggerMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            new FileSystemRuleLoader(_optionsMock.Object, null!));
    }

    [TestMethod]
    public async Task LoadRulesAsync_WithExistingFile_ReturnsRules()
    {
        // Arrange
        var testYaml = CreateTestYamlContent();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, testYaml);

        _processorOptions.RulesFilePath = tempFile;

        // Act
        var result = await _ruleLoader.LoadRulesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Rules.Should().NotBeEmpty();
        result.Rules.First().Id.Should().Be("test-rule");

        // Cleanup
        File.Delete(tempFile);
    }

    [TestMethod]
    public async Task LoadRulesAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        _processorOptions.RulesFilePath = "non-existent-file.yaml";

        // Act & Assert
        await Assert.ThrowsExceptionAsync<FileNotFoundException>(
            () => _ruleLoader.LoadRulesAsync());
    }

    [TestMethod]
    public async Task LoadRulesFromFileAsync_WithValidYaml_ReturnsParsedDocument()
    {
        // Arrange
        var testYaml = CreateTestYamlContent();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, testYaml);

        // Act
        var result = await _ruleLoader.LoadRulesFromFileAsync(tempFile);

        // Assert
        result.Should().NotBeNull();
        result.Ruleset.Id.Should().Be("test-ruleset");
        result.Rules.Should().HaveCount(1);

        // Cleanup
        File.Delete(tempFile);
    }

    [TestMethod]
    public async Task LoadRulesFromFileAsync_WithInvalidYaml_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidYaml = "invalid: yaml: content: [unclosed";
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, invalidYaml);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _ruleLoader.LoadRulesFromFileAsync(tempFile));

        // Cleanup
        File.Delete(tempFile);
    }

    [TestMethod]
    public async Task LoadRulesFromYamlAsync_WithValidContent_ReturnsParsedDocument()
    {
        // Arrange
        var testYaml = CreateTestYamlContent();

        // Act
        var result = await _ruleLoader.LoadRulesFromYamlAsync(testYaml);

        // Assert
        result.Should().NotBeNull();
        result.Ruleset.Name.Should().Be("Test Ruleset");
        result.Rules.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task LoadRulesFromYamlAsync_WithNullContent_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => _ruleLoader.LoadRulesFromYamlAsync(null!));
    }

    [TestMethod]
    public async Task LoadRulesFromYamlAsync_WithEmptyContent_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => _ruleLoader.LoadRulesFromYamlAsync(""));
    }

    [TestMethod]
    public async Task ClearCacheAsync_ClearsCache()
    {
        // Arrange
        var testYaml = CreateTestYamlContent();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, testYaml);

        // Load rules to populate cache
        await _ruleLoader.LoadRulesFromFileAsync(tempFile);

        // Act
        await _ruleLoader.ClearCacheAsync();

        // Assert
        _ruleLoader.HasValidCache().Should().BeFalse();

        // Cleanup
        File.Delete(tempFile);
    }

    [TestMethod]
    public void HasValidCache_WithCacheDisabled_ReturnsFalse()
    {
        // Arrange
        _processorOptions.Cache.EnableRuleCache = false;

        // Act
        var result = _ruleLoader.HasValidCache();

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task LoadRulesFromYamlAsync_WithInvalidRuleset_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidYaml = @"
ruleset:
  name: ""Test Ruleset""
  # Missing required 'id' field
rules: []
";

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _ruleLoader.LoadRulesFromYamlAsync(invalidYaml));
    }

    [TestMethod]
    public async Task LoadRulesFromYamlAsync_WithInvalidRule_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidYaml = @"
ruleset:
  id: test-ruleset
  name: ""Test Ruleset""
rules:
  - name: ""Test Rule""
    # Missing required 'id' field
    scope:
      entity: TestEntity
    requirements: []
";

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _ruleLoader.LoadRulesFromYamlAsync(invalidYaml));
    }

    private static string CreateTestYamlContent()
    {
        return @"
ruleset:
  id: test-ruleset
  name: ""Test Ruleset""
  version: 1
defaults:
  mode: validate
  severity: error
rules:
  - id: test-rule
    name: ""Test Rule""
    scope:
      entity: TestEntity
    requirements:
      - property: TestProperty
        required: true
        type: string
";
    }
}

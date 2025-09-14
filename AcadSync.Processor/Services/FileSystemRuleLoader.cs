using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using AcadSync.Processor.Configuration;
using AcadSync.Processor.Interfaces;

namespace AcadSync.Processor.Services;

/// <summary>
/// File system-based rule loader with caching support
/// </summary>
public class FileSystemRuleLoader : IRuleLoader
{
    private readonly ProcessorOptions _options;
    private readonly ILogger<FileSystemRuleLoader> _logger;
    private readonly ConcurrentDictionary<string, CachedRule> _cache = new();

    public FileSystemRuleLoader(IOptions<ProcessorOptions> options, ILogger<FileSystemRuleLoader> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EprlDoc> LoadRulesAsync()
    {
        var filePath = Path.IsPathRooted(_options.RulesFilePath) 
            ? _options.RulesFilePath 
            : Path.Combine(Directory.GetCurrentDirectory(), _options.RulesFilePath);

        return await LoadRulesFromFileAsync(filePath);
    }

    public async Task<EprlDoc> LoadRulesFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Rules file not found: {filePath}");
        }

        // Check cache first
        if (_options.Cache.EnableRuleCache && TryGetFromCache(filePath, out var cachedDoc))
        {
            _logger.LogDebug("Loaded rules from cache: {FilePath}", filePath);
            return cachedDoc;
        }

        _logger.LogInformation("Loading rules from file: {FilePath}", filePath);

        try
        {
            var yamlContent = await File.ReadAllTextAsync(filePath);
            var doc = await LoadRulesFromYamlAsync(yamlContent);

            // Cache the result
            if (_options.Cache.EnableRuleCache)
            {
                CacheRule(filePath, doc);
            }

            _logger.LogInformation("Successfully loaded ruleset: {Name} v{Version} with {RuleCount} rules from {FilePath}", 
                doc.Ruleset.Name, doc.Ruleset.Version, doc.Rules.Count, filePath);

            return doc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rules from file: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<EprlDoc> LoadRulesFromYamlAsync(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            throw new ArgumentException("YAML content cannot be null or empty", nameof(yamlContent));
        }

        try
        {
            _logger.LogDebug("Parsing YAML content ({Length} characters)", yamlContent.Length);
            
            var doc = EprlLoader.LoadFromYaml(yamlContent);
            
            // Validate the loaded document
            ValidateRuleDocument(doc);
            
            _logger.LogDebug("Successfully parsed YAML content into ruleset: {Name} v{Version}", 
                doc.Ruleset.Name, doc.Ruleset.Version);
            
            return await Task.FromResult(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse YAML content");
            throw new InvalidOperationException("Failed to parse YAML rule content", ex);
        }
    }

    public async Task ClearCacheAsync()
    {
        _cache.Clear();
        _logger.LogInformation("Rule cache cleared");
        await Task.CompletedTask;
    }

    public bool HasValidCache()
    {
        if (!_options.Cache.EnableRuleCache)
            return false;

        var filePath = Path.IsPathRooted(_options.RulesFilePath) 
            ? _options.RulesFilePath 
            : Path.Combine(Directory.GetCurrentDirectory(), _options.RulesFilePath);

        return TryGetFromCache(filePath, out _);
    }

    private bool TryGetFromCache(string filePath, out EprlDoc doc)
    {
        doc = null!;

        if (!_cache.TryGetValue(filePath, out var cached))
            return false;

        // Check if cache is expired
        var expirationTime = cached.CachedAt.AddMinutes(_options.Cache.RuleCacheExpirationMinutes);
        if (DateTimeOffset.UtcNow > expirationTime)
        {
            _cache.TryRemove(filePath, out _);
            _logger.LogDebug("Cache expired for rules file: {FilePath}", filePath);
            return false;
        }

        // Check if file has been modified since caching
        if (File.Exists(filePath))
        {
            var lastWriteTime = File.GetLastWriteTimeUtc(filePath);
            if (lastWriteTime > cached.FileLastModified)
            {
                _cache.TryRemove(filePath, out _);
                _logger.LogDebug("File modified since cache, invalidating cache for: {FilePath}", filePath);
                return false;
            }
        }

        doc = cached.Document;
        return true;
    }

    private void CacheRule(string filePath, EprlDoc doc)
    {
        var lastModified = File.Exists(filePath) ? File.GetLastWriteTimeUtc(filePath) : DateTimeOffset.UtcNow;
        
        var cached = new CachedRule
        {
            Document = doc,
            CachedAt = DateTimeOffset.UtcNow,
            FileLastModified = lastModified
        };

        _cache.AddOrUpdate(filePath, cached, (key, existing) => cached);
        _logger.LogDebug("Cached rules for file: {FilePath}", filePath);
    }

    private void ValidateRuleDocument(EprlDoc doc)
    {
        if (doc == null)
            throw new InvalidOperationException("Rule document is null");

        if (string.IsNullOrWhiteSpace(doc.Ruleset.Id))
            throw new InvalidOperationException("Ruleset ID is required");

        if (string.IsNullOrWhiteSpace(doc.Ruleset.Name))
            throw new InvalidOperationException("Ruleset name is required");

        if (doc.Rules == null || !doc.Rules.Any())
            throw new InvalidOperationException("At least one rule is required");

        // Validate individual rules
        foreach (var rule in doc.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
                throw new InvalidOperationException($"Rule ID is required for rule: {rule.Name}");

            if (string.IsNullOrWhiteSpace(rule.Scope.Entity))
                throw new InvalidOperationException($"Rule scope entity is required for rule: {rule.Id}");

            if (rule.Requirements == null || !rule.Requirements.Any())
                throw new InvalidOperationException($"At least one requirement is required for rule: {rule.Id}");

            // Validate requirements
            foreach (var req in rule.Requirements)
            {
                if (string.IsNullOrWhiteSpace(req.property))
                    throw new InvalidOperationException($"Property name is required for requirement in rule: {rule.Id}");
            }
        }

        _logger.LogDebug("Rule document validation passed for ruleset: {RulesetId}", doc.Ruleset.Id);
    }

    private class CachedRule
    {
        public EprlDoc Document { get; set; } = null!;
        public DateTimeOffset CachedAt { get; set; }
        public DateTimeOffset FileLastModified { get; set; }
    }
}

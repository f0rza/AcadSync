namespace AcadSync.Processor.Interfaces;

/// <summary>
/// Interface for loading and caching rule documents
/// </summary>
public interface IRuleLoader
{
    /// <summary>
    /// Load rules from the configured source
    /// </summary>
    /// <returns>The loaded rule document</returns>
    Task<EprlDoc> LoadRulesAsync();

    /// <summary>
    /// Load rules from a specific file path
    /// </summary>
    /// <param name="filePath">Path to the rules file</param>
    /// <returns>The loaded rule document</returns>
    Task<EprlDoc> LoadRulesFromFileAsync(string filePath);

    /// <summary>
    /// Load rules from YAML content
    /// </summary>
    /// <param name="yamlContent">YAML content as string</param>
    /// <returns>The loaded rule document</returns>
    Task<EprlDoc> LoadRulesFromYamlAsync(string yamlContent);

    /// <summary>
    /// Clear the rule cache
    /// </summary>
    Task ClearCacheAsync();

    /// <summary>
    /// Check if rules are cached and valid
    /// </summary>
    /// <returns>True if cached rules are available and valid</returns>
    bool HasValidCache();
}

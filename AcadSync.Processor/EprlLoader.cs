using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AcadSync.Processor;

// ------------------ Loader ------------------
public static class EprlLoader
{
    private static readonly IDeserializer _yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static EprlDoc LoadFromYaml(string yaml) => _yaml.Deserialize<EprlDoc>(yaml);
}

using YamlDotNet.RepresentationModel;

namespace TelemetryBridge.ContractTests;

public sealed class OpenApiContractTests
{
    [Fact]
    public void EveryPublicPathHasAnImplementationContract()
    {
        var publicPaths = LoadPaths("public-api.yaml");
        var implementationPaths = LoadPaths("legacy-api.yaml")
            .Concat(LoadPaths("modern-api.yaml"))
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(publicPaths, path => Assert.Contains(path, implementationPaths));
    }

    [Fact]
    public void ContractsUseOpenApi31AndStableOperationIds()
    {
        foreach (var file in new[] { "public-api.yaml", "legacy-api.yaml", "modern-api.yaml" })
        {
            var root = Load(file);
            Assert.Equal("3.1.0", ((YamlScalarNode)root.Children["openapi"]).Value);
            var paths = (YamlMappingNode)root.Children["paths"];
            foreach (var path in paths.Children.Values.Cast<YamlMappingNode>())
            {
                foreach (var operation in path.Children.Values.Cast<YamlMappingNode>())
                {
                    Assert.True(operation.Children.ContainsKey("operationId"));
                }
            }
        }
    }

    private static IEnumerable<string> LoadPaths(string name) =>
        ((YamlMappingNode)Load(name).Children["paths"]).Children.Keys
            .Cast<YamlScalarNode>()
            .Select(node => node.Value!);

    private static YamlMappingNode Load(string name)
    {
        var path = Path.Combine(FindRepositoryRoot(), "openapi", name);
        using var reader = File.OpenText(path);
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TelemetryBridge.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

/// <summary>A flat key/value <see cref="IConfiguration"/> (keys with <c>:</c> paths) so tests need no configuration package.</summary>
public sealed class TestConfiguration : IConfiguration
{
    private readonly Dictionary<string, string?> _values;
    private readonly string _path;

    public TestConfiguration(IDictionary<string, string?> values, string path = "")
    {
        _values = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
        _path = path;
    }

    public string? this[string key]
    {
        get => _values.GetValueOrDefault(Full(key));
        set => _values[Full(key)] = value;
    }

    public IConfigurationSection GetSection(string key) => new Section(_values, Full(key));

    public IEnumerable<IConfigurationSection> GetChildren() => Children(_values, _path);

    public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);

    private string Full(string key) => _path.Length == 0 ? key : $"{_path}:{key}";

    private static IEnumerable<IConfigurationSection> Children(Dictionary<string, string?> values, string path)
    {
        var prefix = path.Length == 0 ? string.Empty : path + ":";

        return values.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => k[prefix.Length..].Split(':')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => (IConfigurationSection)new Section(values, prefix + name));
    }

    private sealed class Section : IConfigurationSection
    {
        private readonly Dictionary<string, string?> _values;

        public Section(Dictionary<string, string?> values, string path)
        {
            _values = values;
            Path = path;
        }

        public string Path { get; }
        public string Key => Path[(Path.LastIndexOf(':') + 1)..];

        public string? Value
        {
            get => _values.GetValueOrDefault(Path);
            set => _values[Path] = value;
        }

        public string? this[string key]
        {
            get => _values.GetValueOrDefault($"{Path}:{key}");
            set => _values[$"{Path}:{key}"] = value;
        }

        public IConfigurationSection GetSection(string key) => new Section(_values, $"{Path}:{key}");

        public IEnumerable<IConfigurationSection> GetChildren() => Children(_values, Path);

        public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);
    }
}

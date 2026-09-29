using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace ContextSync.Scenarios;

public static class YamlNormalizer
{
    public static (Dictionary<string, object?> Root, List<string> Errors) ParseDocument(string yaml)
    {
        var stream = new YamlStream();

        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (Exception ex)
        {
            return (new Dictionary<string, object?>(), new List<string> { "YAML parse error: " + ex.Message });
        }

        if (stream.Documents.Count == 0)
            return (new Dictionary<string, object?>(), new List<string> { "Empty YAML document" });

        if (stream.Documents[0].RootNode is not YamlMappingNode map)
            return (new Dictionary<string, object?>(), new List<string> { "Root node must be a mapping" });

        return (ConvertMap(map), new List<string>());
    }

    private static Dictionary<string, object?> ConvertMap(YamlMappingNode map)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in map.Children)
        {
            var key = entry.Key is YamlScalarNode k ? k.Value ?? "" : "";
            result[key] = ConvertValue(entry.Value);
        }
        return result;
    }

    public static object? ConvertValue(YamlNode node)
    {
        switch (node)
        {
            case YamlScalarNode scalar:
                return ParseScalar(scalar.Value);
            case YamlMappingNode mapping:
                return ConvertMap(mapping);
            case YamlSequenceNode sequence:
                var list = new List<object?>();
                foreach (var child in sequence.Children)
                    list.Add(ConvertValue(child));
                return list;
            default:
                return null;
        }
    }

    private static object? ParseScalar(string? value)
    {
        if (value == null) return null;
        if (value == "true") return true;
        if (value == "false") return false;
        if (value == "null" || value == "~") return null;
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
        return value;
    }
}

public static class YamlDictionaryExtensions
{
    public static T? Get<T>(this Dictionary<string, object?> dict, string key) where T : struct =>
        dict.TryGetValue(key, out var value) && value != null ? (T)ConvertValue(typeof(T), value) : null;

    public static string GetString(this Dictionary<string, object?> dict, string key, string? fallback = null)
    {
        if (!dict.TryGetValue(key, out var value) || value == null)
            return fallback ?? "";
        if (value is string s) return s;
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback ?? "";
    }

    public static Dictionary<string, object?>? GetDictionary(this Dictionary<string, object?> dict, string key) =>
        dict.TryGetValue(key, out var value) && value is Dictionary<string, object?> nested ? nested : null;

    public static List<object?>? GetList(this Dictionary<string, object?> dict, string key) =>
        dict.TryGetValue(key, out var value) && value is List<object?> list ? list : null;

    private static object ConvertValue(Type target, object value) => value switch
    {
        long l when target == typeof(int) => (int)l,
        long l when target == typeof(long) => l,
        bool b when target == typeof(bool) => b,
        double d when target == typeof(double) => d,
        string s when target == typeof(bool) => string.Equals(s, "true", StringComparison.OrdinalIgnoreCase),
        string s when target == typeof(int) => int.Parse(s, CultureInfo.InvariantCulture),
        string s when target == typeof(double) => double.Parse(s, CultureInfo.InvariantCulture),
        _ => Convert.ChangeType(value, target, CultureInfo.InvariantCulture)
    };
}

public static class YamlValueExtensions
{
    public static List<object?>? AsList(this object? value) => value is List<object?> list ? list : null;

    public static List<string> AsStringList(this object? value) => value switch
    {
        List<object?> list => list.Select(i => Convert.ToString(i, CultureInfo.InvariantCulture) ?? "").ToList(),
        string s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        _ => new List<string>()
    };

    public static string ToDisplay(this object? value)
    {
        if (value == null) return "";
        if (value is double d) return d.ToString("0.##", CultureInfo.InvariantCulture);
        if (value is long l) return l.ToString(CultureInfo.InvariantCulture);
        if (value is bool b) return b ? "true" : "false";
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }
}

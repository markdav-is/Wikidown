using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Wikidown.Html;

// Jekyll's site.data: every JSON and YAML file under _data/, keyed by file
// name without extension, folders as nested maps — so
// _data/hops/2026-11.json is site.data.hops["2026-11"]. Values become the
// plain dictionaries, lists, and scalars Fluid reads natively.
public static partial class SiteData
{
    public static Dictionary<string, object?> Load(string wikiRoot)
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);
        var dir = Path.Combine(wikiRoot, "_data");
        if (!Directory.Exists(dir)) return data;

        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension is not (".json" or ".yml" or ".yaml")) continue;
            var relative = Path.GetRelativePath(dir, file).Replace('\\', '/');
            var parts = relative.Split('/');
            if (parts.Any(p => p.StartsWith('.'))) continue;

            var target = data;
            foreach (var folder in parts[..^1])
            {
                if (!(target.TryGetValue(folder, out var existing) && existing is Dictionary<string, object?> sub))
                    target[folder] = sub = new Dictionary<string, object?>(StringComparer.Ordinal);
                target = sub;
            }

            var text = File.ReadAllText(file);
            try
            {
                target[Path.GetFileNameWithoutExtension(file)] = extension == ".json" ? FromJson(text) : FromYaml(text);
            }
            catch (Exception e) when (e is JsonException or YamlException)
            {
                throw new InvalidOperationException($"_data/{relative}: {e.Message}", e);
            }
        }
        return data;
    }

    public static object? FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        return Convert(doc.RootElement);
    }

    private static object? Convert(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().Aggregate(
            new Dictionary<string, object?>(StringComparer.Ordinal),
            (d, p) => { d[p.Name] = Convert(p.Value); return d; }),
        JsonValueKind.Array => e.EnumerateArray().Select(Convert).ToList(),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    public static object? FromYaml(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return stream.Documents.Count == 0 ? null : Convert(stream.Documents[0].RootNode);
    }

    private static object? Convert(YamlNode node) => node switch
    {
        YamlMappingNode map => map.Children.Aggregate(
            new Dictionary<string, object?>(StringComparer.Ordinal),
            (d, kv) => { d[((YamlScalarNode)kv.Key).Value ?? ""] = Convert(kv.Value); return d; }),
        YamlSequenceNode list => list.Children.Select(Convert).ToList(),
        YamlScalarNode scalar => Scalar(scalar),
        _ => null,
    };

    // Plain scalars typed the way Jekyll's YAML loader (Psych, YAML 1.1)
    // types them, so `price: 5` sorts as a number and `draft: no` is false.
    // Quoted scalars stay strings.
    private static object? Scalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? "";
        if (scalar.Style != ScalarStyle.Plain) return value;
        switch (value.ToLowerInvariant())
        {
            case "" or "~" or "null": return null;
            case "true" or "yes" or "on": return true;
            case "false" or "no" or "off": return false;
        }
        if (YamlInt().IsMatch(value) && long.TryParse(value.Replace("_", ""), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l))
            return l;
        if (YamlFloat().IsMatch(value) && decimal.TryParse(value.Replace("_", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d;
        return value;
    }

    [GeneratedRegex(@"^[-+]?[0-9][0-9_]*$")]
    private static partial Regex YamlInt();

    [GeneratedRegex(@"^[-+]?([0-9][0-9_]*)?\.[0-9_]+([eE][-+]?[0-9]+)?$|^[-+]?[0-9][0-9_]*[eE][-+]?[0-9]+$")]
    private static partial Regex YamlFloat();
}

namespace Wikidown.Core;

// Subtrees the published site should omit, declared in _config.yml:
//
//   wikidown:
//     exclude_from_site:
//       - /Meta
//       - /Testing
//
// This is a publishing concern only: excluded pages stay first-class for
// the CLI, MCP server, editor, and check-links. Parsed here (not in
// Wikidown.Html) because JekyllNavigation needs it too.
public static class PublishExclusions
{
    public static IReadOnlyList<string> Load(string wikiRoot)
    {
        var configPath = Path.Combine(wikiRoot, "_config.yml");
        return File.Exists(configPath) ? Parse(File.ReadAllText(configPath)) : Array.Empty<string>();
    }

    public static IReadOnlyList<string> Parse(string configYaml)
    {
        var result = new List<string>();
        var inWikidown = false;
        var inList = false;

        foreach (var raw in configYaml.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.TrimStart().StartsWith('#')) continue;

            var indent = line.Length - line.TrimStart().Length;
            if (indent == 0)
            {
                inWikidown = line.TrimEnd() == "wikidown:";
                inList = false;
                continue;
            }
            if (!inWikidown) continue;

            var trimmed = line.Trim();
            if (trimmed == "exclude_from_site:") { inList = true; continue; }
            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (!inList) continue;
                var entry = trimmed[2..].Trim().Trim('"', '\'').TrimEnd('/');
                if (entry.StartsWith('/')) result.Add(entry);
            }
            else
            {
                inList = false;
            }
        }
        return result;
    }

    public static bool IsExcluded(PagePath page, IReadOnlyList<string> exclusions)
    {
        if (exclusions.Count == 0) return false;
        var link = page.ToLinkPath();
        foreach (var excluded in exclusions)
        {
            if (link.Equals(excluded, StringComparison.OrdinalIgnoreCase)
                || link.StartsWith(excluded + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

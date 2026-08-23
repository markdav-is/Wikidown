using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Wikidown.Html;

// The theme as HtmlExporter sees it: files from the wiki root (scaffolded
// by `wikidown pages`, possibly edited) layered over the embedded
// defaults, so export works on an unscaffolded wiki and a customized one
// alike. Also serves `_includes/` to Fluid.
public sealed partial class ThemeFiles : IFileProvider
{
    private readonly Dictionary<string, string> _text = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _diskFiles = new(StringComparer.Ordinal);

    public static ThemeFiles Load(string wikiRoot)
    {
        var theme = new ThemeFiles();
        foreach (var file in ThemeResources.Files)
            theme._text[file] = ThemeResources.Read(file);

        foreach (var folder in new[] { "_layouts", "_includes", "assets" })
        {
            var dir = Path.Combine(wikiRoot, folder);
            if (!Directory.Exists(dir)) continue;
            foreach (var path in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var relative = folder + "/" + Path.GetRelativePath(dir, path).Replace('\\', '/');
                theme._diskFiles[relative] = path;
                if (folder != "assets") theme._text[relative] = File.ReadAllText(path);
            }
        }

        foreach (var file in new[] { "_config.yml", "index.html" })
        {
            var path = Path.Combine(wikiRoot, file);
            if (File.Exists(path)) theme._text[file] = File.ReadAllText(path);
        }

        return theme;
    }

    public string Text(string relativePath) =>
        _text.TryGetValue(relativePath, out var text)
            ? text
            : throw new FileNotFoundException($"theme file not found: {relativePath}");

    public bool Has(string relativePath) => _text.ContainsKey(relativePath) || _diskFiles.ContainsKey(relativePath);

    // Every asset the site needs copied verbatim: the embedded defaults
    // plus anything under the wiki's own assets/ folder.
    public IEnumerable<(string RelativePath, Action<string> CopyTo)> Assets()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (relative, disk) in _diskFiles)
        {
            if (!relative.StartsWith("assets/", StringComparison.Ordinal)) continue;
            seen.Add(relative);
            var source = disk;
            yield return (relative, dest => File.Copy(source, dest, overwrite: true));
        }
        foreach (var relative in ThemeResources.Files)
        {
            if (!relative.StartsWith("assets/", StringComparison.Ordinal) || seen.Contains(relative)) continue;
            var content = _text[relative];
            yield return (relative, dest => File.WriteAllText(dest, content));
        }
    }

    // Liquid includes in Jekyll's dialect (`{% include file.html a=b %}`,
    // read back as `include.a`) rewritten to Fluid's
    // (`{% include 'file.html', a: b %}`, read back as `a`), so one theme
    // serves both GitHub's Jekyll and this exporter.
    public static string ToFluidSyntax(string liquid)
    {
        var rewritten = IncludeTag().Replace(liquid, m =>
        {
            var args = IncludeArg().Matches(m.Groups[2].Value)
                .Select(a => $", {a.Groups[1].Value}: {a.Groups[2].Value}");
            return $"{{% include '{m.Groups[1].Value}'{string.Concat(args)} %}}";
        });
        return LiquidBlock().Replace(rewritten, b => IncludeMember().Replace(b.Value, "$1"));
    }

    [GeneratedRegex(@"\{%-?\s*include\s+([^\s'""%]+)((?:\s+[A-Za-z_]\w*=(?:""[^""]*""|'[^']*'|[^\s%]+))*)\s*-?%\}")]
    private static partial Regex IncludeTag();

    [GeneratedRegex(@"([A-Za-z_]\w*)=(""[^""]*""|'[^']*'|[^\s%]+)")]
    private static partial Regex IncludeArg();

    [GeneratedRegex(@"\{\{.*?\}\}|\{%.*?%\}", RegexOptions.Singleline)]
    private static partial Regex LiquidBlock();

    [GeneratedRegex(@"\binclude\.([A-Za-z_]\w*)")]
    private static partial Regex IncludeMember();

    // ── IFileProvider (Fluid include lookup, rooted at _includes/) ───────

    IFileInfo IFileProvider.GetFileInfo(string subpath)
    {
        var name = "_includes/" + subpath.TrimStart('/', '\\').Replace('\\', '/');
        return _text.TryGetValue(name, out var text)
            ? new TextFileInfo(Path.GetFileName(name), ToFluidSyntax(text))
            : new NotFoundFileInfo(subpath);
    }

    IDirectoryContents IFileProvider.GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

    IChangeToken IFileProvider.Watch(string filter) => NullChangeToken.Singleton;

    private sealed class TextFileInfo(string name, string text) : IFileInfo
    {
        private readonly byte[] _bytes = System.Text.Encoding.UTF8.GetBytes(text);
        public bool Exists => true;
        public long Length => _bytes.Length;
        public string? PhysicalPath => null;
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.MinValue;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => new MemoryStream(_bytes, writable: false);
    }
}

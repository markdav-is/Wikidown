namespace Wikidown.Core;

// Wiki-relative path to a page, stored as a list of PageName segments.
// "/" => empty segments. Provides filename and link-form rendering.
public sealed record PagePath(IReadOnlyList<PageName> Segments)
{
    public static PagePath Root { get; } = new(Array.Empty<PageName>());

    public bool IsRoot => Segments.Count == 0;

    public PageName Name =>
        IsRoot ? throw new InvalidOperationException("Root has no name.") : Segments[^1];

    public PagePath Parent =>
        IsRoot ? this : new PagePath(Segments.Take(Segments.Count - 1).ToList());

    public PagePath Append(PageName child) =>
        new(Segments.Append(child).ToList());

    // e.g. "/Getting-Started/Install"
    public string ToLinkPath()
    {
        if (IsRoot) return "/";
        return "/" + string.Join("/", Segments.Select(s => s.FileBase));
    }

    // Disk path, relative to the docs root.
    public string ToFilePath()
    {
        if (IsRoot)
            throw new InvalidOperationException("Root has no page file.");
        var dirSegments = Segments.Take(Segments.Count - 1).Select(s => s.FileBase);
        var joined = string.Join(Path.DirectorySeparatorChar, dirSegments);
        var file = Name.FileName;
        return string.IsNullOrEmpty(joined) ? file : Path.Combine(joined, file);
    }

    // Disk path of the folder that would hold this page's subpages.
    public string ToFolderPath()
    {
        if (IsRoot) return string.Empty;
        return string.Join(Path.DirectorySeparatorChar, Segments.Select(s => s.FileBase));
    }

    public static PagePath Parse(string linkPath)
    {
        ArgumentNullException.ThrowIfNull(linkPath);
        var trimmed = linkPath.Trim().TrimStart('/');
        if (trimmed.Length == 0) return Root;

        var parts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var names = parts.Select(PageName.FromFileBase).ToList();
        return new PagePath(names);
    }

    public override string ToString() => ToLinkPath();
}

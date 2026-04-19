namespace Wikidown.Core;

// Directory-backed wiki. All operations are relative to RootPath, which is
// the folder containing top-level pages (typically "<repo>/docs").
public sealed class WikiRepository
{
    public string RootPath { get; }

    public WikiRepository(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = Path.GetFullPath(rootPath);
    }

    public bool Exists(PagePath path) =>
        !path.IsRoot && File.Exists(ResolveFile(path));

    public WikiPage Read(PagePath path)
    {
        if (path.IsRoot)
            throw new InvalidOperationException("Cannot read the root as a page.");
        var file = ResolveFile(path);
        if (!File.Exists(file))
            throw new FileNotFoundException($"Page not found: {path.ToLinkPath()}", file);
        var text = File.ReadAllText(file);
        return new WikiPage(path, text);
    }

    public void Write(WikiPage page)
    {
        if (page.Path.IsRoot)
            throw new InvalidOperationException("Cannot write the root as a page.");

        var file = ResolveFile(page.Path);
        var dir = System.IO.Path.GetDirectoryName(file)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(file, NormalizeNewlines(page.Markdown));
        EnsureOrderIncludes(page.Path);
    }

    public void Delete(PagePath path, bool deleteSubpages = false)
    {
        if (path.IsRoot) throw new InvalidOperationException("Cannot delete root.");
        var file = ResolveFile(path);
        if (File.Exists(file)) File.Delete(file);

        var subDir = ResolveFolder(path);
        if (Directory.Exists(subDir))
        {
            if (!deleteSubpages)
                throw new InvalidOperationException(
                    $"Subpages exist at {path.ToLinkPath()}; pass deleteSubpages=true.");
            Directory.Delete(subDir, recursive: true);
        }

        RemoveFromOrder(path);
    }

    public void Move(PagePath from, PagePath to)
    {
        if (from.IsRoot || to.IsRoot)
            throw new InvalidOperationException("Cannot move to/from root.");
        if (!Exists(from))
            throw new FileNotFoundException($"Page not found: {from.ToLinkPath()}");
        if (Exists(to))
            throw new InvalidOperationException($"Destination exists: {to.ToLinkPath()}");

        var fromFile = ResolveFile(from);
        var toFile = ResolveFile(to);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(toFile)!);
        File.Move(fromFile, toFile);

        var fromDir = ResolveFolder(from);
        var toDir = ResolveFolder(to);
        if (Directory.Exists(fromDir))
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(toDir)!);
            Directory.Move(fromDir, toDir);
        }

        RemoveFromOrder(from);
        EnsureOrderIncludes(to);
    }

    public IReadOnlyList<PagePath> ListChildren(PagePath parent)
    {
        var folder = parent.IsRoot ? RootPath : ResolveFolder(parent);
        if (!Directory.Exists(folder)) return Array.Empty<PagePath>();

        var ordered = ReadOrder(parent);
        var present = Directory.EnumerateFiles(folder, "*.md")
            .Select(System.IO.Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new List<PagePath>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ordered)
        {
            if (!present.Contains(entry)) continue;
            if (!seen.Add(entry)) continue;
            result.Add(parent.Append(PageName.FromFileBase(entry)));
        }
        foreach (var entry in present.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            if (seen.Contains(entry)) continue;
            result.Add(parent.Append(PageName.FromFileBase(entry)));
        }
        return result;
    }

    public IEnumerable<PagePath> Walk(PagePath? from = null)
    {
        var start = from ?? PagePath.Root;
        var stack = new Stack<PagePath>();
        foreach (var child in ListChildren(start).Reverse()) stack.Push(child);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            foreach (var child in ListChildren(current).Reverse()) stack.Push(child);
        }
    }

    public IReadOnlyList<string> ReadOrder(PagePath folder)
    {
        var path = OrderPath(folder);
        if (!File.Exists(path)) return Array.Empty<string>();
        return OrderFile.Parse(File.ReadAllText(path));
    }

    public void WriteOrder(PagePath folder, IEnumerable<string> entries)
    {
        var path = OrderPath(folder);
        var content = OrderFile.Render(entries);
        var dir = System.IO.Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        if (content.Length == 0 && File.Exists(path))
        {
            File.Delete(path);
            return;
        }
        File.WriteAllText(path, content);
    }

    private void EnsureOrderIncludes(PagePath page)
    {
        var parent = page.Parent;
        var entries = ReadOrder(parent).ToList();
        if (!entries.Contains(page.Name.FileBase, StringComparer.OrdinalIgnoreCase))
        {
            entries.Add(page.Name.FileBase);
            WriteOrder(parent, entries);
        }
    }

    private void RemoveFromOrder(PagePath page)
    {
        var parent = page.Parent;
        var entries = ReadOrder(parent)
            .Where(e => !string.Equals(e, page.Name.FileBase, StringComparison.OrdinalIgnoreCase))
            .ToList();
        WriteOrder(parent, entries);
    }

    private string ResolveFile(PagePath path) =>
        System.IO.Path.Combine(RootPath, path.ToFilePath());

    private string ResolveFolder(PagePath path) =>
        System.IO.Path.Combine(RootPath, path.ToFolderPath());

    private string OrderPath(PagePath folder) =>
        folder.IsRoot
            ? System.IO.Path.Combine(RootPath, OrderFile.FileName)
            : System.IO.Path.Combine(ResolveFolder(folder), OrderFile.FileName);

    private static string NormalizeNewlines(string text)
    {
        var lf = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return lf.EndsWith('\n') ? lf : lf + "\n";
    }
}

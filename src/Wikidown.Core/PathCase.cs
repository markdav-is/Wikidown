namespace Wikidown.Core;

// Windows and macOS filesystems ignore case; the Linux hosts a wiki is
// published to (GitHub Pages and friends) do not. These checks answer "does
// this exist *as spelled*?" the same way on every platform.
public static class PathCase
{
    public static bool ExistsExact(string fullPath)
    {
        var dir = Path.GetDirectoryName(fullPath);
        var name = Path.GetFileName(fullPath);
        if (string.IsNullOrEmpty(dir) || name.Length == 0 || !Directory.Exists(dir)) return false;
        return Directory.EnumerateFileSystemEntries(dir)
            .Any(entry => string.Equals(Path.GetFileName(entry), name, StringComparison.Ordinal));
    }

    // Every segment of fullPath below root exists with exactly this casing.
    public static bool ExistsExactBelow(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            return File.Exists(fullPath) || Directory.Exists(fullPath);

        if (relative == ".") return true;

        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!ExistsExact(current)) return false;
        }
        return true;
    }
}

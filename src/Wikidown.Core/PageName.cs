namespace Wikidown.Core;

// ADO wiki maps page titles to filenames by replacing space with '-'.
// This type owns that conversion plus path-safe validation.
public readonly record struct PageName
{
    private static readonly char[] ForbiddenChars =
        { '/', '\\', ':', '*', '?', '"', '<', '>', '|', '#' };

    public string Title { get; }
    public string FileBase { get; }

    private PageName(string title, string fileBase)
    {
        Title = title;
        FileBase = fileBase;
    }

    public string FileName => FileBase + ".md";

    public static PageName FromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (title.IndexOfAny(ForbiddenChars) >= 0)
            throw new ArgumentException(
                $"Title contains a forbidden character ({new string(ForbiddenChars)}).",
                nameof(title));

        var trimmed = title.Trim();
        var fileBase = trimmed.Replace(' ', '-');
        return new PageName(trimmed, fileBase);
    }

    public static PageName FromFileBase(string fileBase)
    {
        if (string.IsNullOrWhiteSpace(fileBase))
            throw new ArgumentException("File base cannot be empty.", nameof(fileBase));

        var normalized = fileBase.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? fileBase[..^3]
            : fileBase;

        var title = normalized.Replace('-', ' ');
        return new PageName(title, normalized);
    }

    // Checked when a page is created, on every platform: a wiki authored on
    // Linux still has to check out on Windows, where these names either fail
    // or silently go somewhere else ("a:b.md" is an alternate data stream on
    // "a"; "CON.md" is the console device).
    public void EnsurePortable()
    {
        if (FileBase.IndexOfAny(ForbiddenChars) >= 0 || FileBase.Any(char.IsControl))
            throw new ArgumentException(
                $"Page name '{FileBase}' contains a character that is not allowed in a page name ({string.Join(' ', ForbiddenChars)}).");
        if (FileBase.EndsWith('.') || FileBase.EndsWith(' '))
            throw new ArgumentException($"Page name '{FileBase}' cannot end with a dot or a space.");

        var device = FileBase.Split('.')[0].TrimEnd(' ');
        if (ReservedDeviceNames.Contains(device))
            throw new ArgumentException(
                $"Page name '{FileBase}' is a reserved device name on Windows ({device}); pick another name.");
    }

    private static readonly HashSet<string> ReservedDeviceNames = new(
        new[] { "CON", "PRN", "AUX", "NUL" }
            .Concat(Enumerable.Range(1, 9).SelectMany(i => new[] { $"COM{i}", $"LPT{i}" })),
        StringComparer.OrdinalIgnoreCase);

    public override string ToString() => Title;
}

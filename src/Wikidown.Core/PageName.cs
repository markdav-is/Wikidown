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

    public override string ToString() => Title;
}

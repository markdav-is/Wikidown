namespace Wikidown.Core;

public sealed record WikiPage(PagePath Path, string Markdown)
{
    public PageName Name => Path.Name;
}

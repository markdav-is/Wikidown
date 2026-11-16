using Wikidown.Core;

namespace Wikidown.Web.Services;

public enum TreeActionKind
{
    NewSubpage,
    Rename,
    Move,
    MoveUp,
    MoveDown,
    Delete,
}

public sealed record TreeAction(TreeActionKind Kind, NavNode Node);

public enum EditView
{
    Editor,
    Split,
    Preview,
}

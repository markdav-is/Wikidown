namespace Wikidown.Core;

// Patch tools work on LF text internally and write back with whatever the
// file used, so an agent's CRLF/LF choice never makes an exact match fail.
public static class LineEndings
{
    public const string Lf = "\n";
    public const string CrLf = "\r\n";

    public static string Detect(string text) =>
        text.Contains(CrLf, StringComparison.Ordinal) ? CrLf : Lf;

    public static string ToLf(string text) =>
        text.Replace(CrLf, Lf, StringComparison.Ordinal).Replace('\r', '\n');

    public static string Apply(string lfText, string ending) =>
        ending == Lf ? lfText : lfText.Replace(Lf, ending, StringComparison.Ordinal);
}

namespace Wikidown.Core;

// Everything works on LF text internally and writes back with whatever the
// file used, so an agent's CRLF/LF choice never makes an exact match fail
// and a Windows checkout never ends up with mixed endings.
public static class LineEndings
{
    public const string Lf = "\n";
    public const string CrLf = "\r\n";

    public static string Detect(string text) =>
        text.Contains(CrLf, StringComparison.Ordinal) ? CrLf : Lf;

    // Null when there is no file, or no line break in it to learn from.
    public static string? OfFile(string path)
    {
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path);
        return text.Contains('\n') ? Detect(text) : null;
    }

    public static string ToLf(string text) =>
        text.Replace(CrLf, Lf, StringComparison.Ordinal).Replace('\r', '\n');

    public static string Apply(string lfText, string ending) =>
        ending == Lf ? lfText : lfText.Replace(Lf, ending, StringComparison.Ordinal);

    // Whole-file write: a rewrite keeps the file's own endings, a new file
    // takes `newFileEnding`.
    public static void WriteFile(string path, string text, string newFileEnding) =>
        WriteKeepingBom(path, Apply(ToLf(text), OfFile(path) ?? newFileEnding));

    // Visual Studio and Notepad save UTF-8 with a BOM; dropping it on the
    // first CLI edit shows up as a change the user never made.
    public static void WriteKeepingBom(string path, string text) =>
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(HasUtf8Bom(path)));

    private static bool HasUtf8Bom(string path)
    {
        if (!File.Exists(path)) return false;
        using var stream = File.OpenRead(path);
        Span<byte> head = stackalloc byte[3];
        return stream.ReadAtLeast(head, 3, throwOnEndOfStream: false) == 3
            && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
    }
}

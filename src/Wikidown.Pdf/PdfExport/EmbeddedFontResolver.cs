using System.Reflection;
using PdfSharp.Fonts;

namespace Wikidown.Pdf.PdfExport;

// Resolves fonts from TTF files embedded as resources in this assembly
// (src/Wikidown.Pdf/Fonts/, DejaVu Sans + DejaVu Sans Mono — Bitstream
// Vera License, redistribution explicitly permitted, see Fonts/LICENSE-DejaVu.txt)
// instead of relying on fonts installed on the host OS. This is what makes
// PDF rendering actually cross-platform: the earlier approach
// (GlobalFontSettings.UseWindowsFontsUnderWindows) only worked on Windows,
// which broke both CI (ubuntu-latest) and any future non-Windows host.
internal sealed class EmbeddedFontResolver : IFontResolver
{
    public const string BodyFamily = "DejaVu Sans";
    public const string MonospaceFamily = "DejaVu Sans Mono";

    private static readonly Assembly ResourceAssembly = typeof(EmbeddedFontResolver).Assembly;
    private static readonly string[] ResourceNames = ResourceAssembly.GetManifestResourceNames();

    private static readonly HashSet<string> KnownFaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "DejaVuSans", "DejaVuSans-Bold", "DejaVuSans-Oblique", "DejaVuSans-BoldOblique",
        "DejaVuSansMono", "DejaVuSansMono-Bold", "DejaVuSansMono-Oblique", "DejaVuSansMono-BoldOblique",
    };

    // Never returns null: MigraDoc's own internals request a handful of
    // fixed, hardcoded family names ("Courier New" among them) during setup
    // — for an internal error/measurement font — completely independent of
    // what the document itself uses. A resolver that only recognizes our
    // two families and returns null for anything else makes that internal
    // setup step throw before a single page renders. Any unrecognized name
    // (including MigraDoc's own) falls back to one of our two embedded
    // families instead, picked by whether the name looks monospace.
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var baseName = LooksMonospace(familyName) ? "DejaVuSansMono" : "DejaVuSans";
        var faceName = (isBold, isItalic) switch
        {
            (true, true) => baseName + "-BoldOblique",
            (true, false) => baseName + "-Bold",
            (false, true) => baseName + "-Oblique",
            (false, false) => baseName,
        };
        return new FontResolverInfo(faceName);
    }

    private static bool LooksMonospace(string familyName) =>
        familyName.Equals(MonospaceFamily, StringComparison.OrdinalIgnoreCase) ||
        familyName.Contains("Mono", StringComparison.OrdinalIgnoreCase) ||
        familyName.Contains("Courier", StringComparison.OrdinalIgnoreCase) ||
        familyName.Contains("Consolas", StringComparison.OrdinalIgnoreCase);

    public byte[] GetFont(string faceName)
    {
        if (!KnownFaces.Contains(faceName))
            throw new InvalidOperationException($"Unknown embedded font face '{faceName}'.");

        var fileName = faceName + ".ttf";
        var resourceName = ResourceNames.FirstOrDefault(
            n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded font resource for '{fileName}' not found.");

        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not open embedded font resource '{resourceName}'.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

using System.Reflection;
using PdfSharp.Fonts;

namespace Wikidown.Pdf.PdfExport;

// Resolves fonts from TTF files embedded as resources in this assembly
// (src/Wikidown.Pdf/Fonts/) instead of relying on fonts installed on the
// host OS. This is what makes PDF rendering actually cross-platform: the
// earlier approach (GlobalFontSettings.UseWindowsFontsUnderWindows) only
// worked on Windows, which broke both CI (ubuntu-latest) and any future
// non-Windows host.
//
// Text is set in Atkinson Hyperlegible Next / Mono (SIL Open Font License
// 1.1, see Fonts/OFL-*.txt). Those cover only ~360 characters — no
// arrows, box drawing, Greek or Cyrillic — so DejaVu Sans / Sans
// Mono (Bitstream Vera License, Fonts/LICENSE-DejaVu.txt) stay embedded as
// a per-character fallback; MigraDocRenderer switches to them for any
// character IsCovered reports missing.
internal sealed class EmbeddedFontResolver : IFontResolver
{
    public const string BodyFamily = "Atkinson Hyperlegible Next";
    public const string MonospaceFamily = "Atkinson Hyperlegible Mono";
    public const string FallbackBodyFamily = "DejaVu Sans";
    public const string FallbackMonospaceFamily = "DejaVu Sans Mono";

    private static readonly Assembly ResourceAssembly = typeof(EmbeddedFontResolver).Assembly;
    private static readonly string[] ResourceNames = ResourceAssembly.GetManifestResourceNames();

    private static readonly HashSet<string> KnownFaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "AtkinsonHyperlegibleNext-Regular", "AtkinsonHyperlegibleNext-Bold",
        "AtkinsonHyperlegibleNext-Italic", "AtkinsonHyperlegibleNext-BoldItalic",
        "AtkinsonHyperlegibleMono-Regular", "AtkinsonHyperlegibleMono-Bold",
        "AtkinsonHyperlegibleMono-Italic", "AtkinsonHyperlegibleMono-BoldItalic",
        "DejaVuSans", "DejaVuSans-Bold", "DejaVuSans-Oblique", "DejaVuSans-BoldOblique",
        "DejaVuSansMono", "DejaVuSansMono-Bold", "DejaVuSansMono-Oblique", "DejaVuSansMono-BoldOblique",
    };

    private static readonly Lazy<CmapCoverage> BodyCoverage =
        new(() => CmapCoverage.Load(LoadFace("AtkinsonHyperlegibleNext-Regular")));
    private static readonly Lazy<CmapCoverage> MonospaceCoverage =
        new(() => CmapCoverage.Load(LoadFace("AtkinsonHyperlegibleMono-Regular")));

    public static bool IsCovered(char c, bool monospace) =>
        (monospace ? MonospaceCoverage : BodyCoverage).Value.Contains(c);

    // Never returns null: MigraDoc's own internals request a handful of
    // fixed, hardcoded family names ("Courier New" among them) during setup
    // — for an internal error/measurement font — completely independent of
    // what the document itself uses. A resolver that only recognizes our
    // families and returns null for anything else makes that internal
    // setup step throw before a single page renders. Any unrecognized name
    // (including MigraDoc's own) falls back to Atkinson Next or Mono, picked
    // by whether the name looks monospace.
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var monospace = LooksMonospace(familyName);
        var faceName = familyName.StartsWith("DejaVu", StringComparison.OrdinalIgnoreCase)
            ? DejaVuFace(monospace ? "DejaVuSansMono" : "DejaVuSans", isBold, isItalic)
            : AtkinsonFace(monospace ? "AtkinsonHyperlegibleMono" : "AtkinsonHyperlegibleNext", isBold, isItalic);
        return new FontResolverInfo(faceName);
    }

    private static string AtkinsonFace(string baseName, bool isBold, bool isItalic) => (isBold, isItalic) switch
    {
        (true, true) => baseName + "-BoldItalic",
        (true, false) => baseName + "-Bold",
        (false, true) => baseName + "-Italic",
        (false, false) => baseName + "-Regular",
    };

    private static string DejaVuFace(string baseName, bool isBold, bool isItalic) => (isBold, isItalic) switch
    {
        (true, true) => baseName + "-BoldOblique",
        (true, false) => baseName + "-Bold",
        (false, true) => baseName + "-Oblique",
        (false, false) => baseName,
    };

    private static bool LooksMonospace(string familyName) =>
        familyName.Contains("Mono", StringComparison.OrdinalIgnoreCase) ||
        familyName.Contains("Courier", StringComparison.OrdinalIgnoreCase) ||
        familyName.Contains("Consolas", StringComparison.OrdinalIgnoreCase);

    public byte[] GetFont(string faceName)
    {
        if (!KnownFaces.Contains(faceName))
            throw new InvalidOperationException($"Unknown embedded font face '{faceName}'.");
        return LoadFace(faceName);
    }

    private static byte[] LoadFace(string faceName)
    {
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

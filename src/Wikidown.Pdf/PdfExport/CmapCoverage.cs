using System.Buffers.Binary;

namespace Wikidown.Pdf.PdfExport;

// Which BMP characters a TrueType font has glyphs for, read from its
// format-4 cmap subtable. PDFsharp doesn't expose this publicly, and
// without it a character the font lacks silently renders as a blank.
internal sealed class CmapCoverage
{
    private readonly HashSet<char> _chars;

    private CmapCoverage(HashSet<char> chars) => _chars = chars;

    public bool Contains(char c) => _chars.Contains(c);

    public static CmapCoverage Load(byte[] font)
    {
        var data = font.AsSpan();
        var cmap = FindTable(data, "cmap");
        var subtable = FindUnicodeBmpSubtable(data, cmap);

        var segCount = U16(data, subtable + 6) / 2;
        var endCodes = subtable + 14;
        var startCodes = endCodes + segCount * 2 + 2;
        var idDeltas = startCodes + segCount * 2;
        var idRangeOffsets = idDeltas + segCount * 2;

        var chars = new HashSet<char>();
        for (var i = 0; i < segCount; i++)
        {
            int start = U16(data, startCodes + i * 2), end = U16(data, endCodes + i * 2);
            int delta = U16(data, idDeltas + i * 2), rangeOffsetPos = idRangeOffsets + i * 2;
            int rangeOffset = U16(data, rangeOffsetPos);
            for (var c = start; c <= end && c != 0xFFFF; c++)
            {
                int glyph;
                if (rangeOffset == 0)
                {
                    glyph = (c + delta) & 0xFFFF;
                }
                else
                {
                    var raw = U16(data, rangeOffsetPos + rangeOffset + (c - start) * 2);
                    glyph = raw == 0 ? 0 : (raw + delta) & 0xFFFF;
                }
                if (glyph != 0) chars.Add((char)c);
            }
        }
        return new CmapCoverage(chars);
    }

    private static int FindTable(ReadOnlySpan<byte> data, string tag)
    {
        var numTables = U16(data, 4);
        for (var i = 0; i < numTables; i++)
        {
            var record = 12 + i * 16;
            if (System.Text.Encoding.ASCII.GetString(data.Slice(record, 4)) == tag)
                return (int)BinaryPrimitives.ReadUInt32BigEndian(data[(record + 8)..]);
        }
        throw new InvalidOperationException($"Font has no '{tag}' table.");
    }

    private static int FindUnicodeBmpSubtable(ReadOnlySpan<byte> data, int cmap)
    {
        var numSubtables = U16(data, cmap + 2);
        for (var i = 0; i < numSubtables; i++)
        {
            var record = cmap + 4 + i * 8;
            int platform = U16(data, record), encoding = U16(data, record + 2);
            var offset = cmap + (int)BinaryPrimitives.ReadUInt32BigEndian(data[(record + 4)..]);
            if ((platform == 3 && encoding == 1 || platform == 0) && U16(data, offset) == 4)
                return offset;
        }
        throw new InvalidOperationException("Font has no format-4 Unicode cmap subtable.");
    }

    private static int U16(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
}

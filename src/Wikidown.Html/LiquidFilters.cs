using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fluid;
using Fluid.Values;

namespace Wikidown.Html;

// How templates reach wiki pages by path: Resolve turns what a template
// wrote ("Venues/Pinebox.md", "/Venues/Pinebox", a page object's path)
// into a page link path, or null when no such page is published.
public sealed record WikiLookup(
    Func<string, string?> Resolve,
    Func<string, bool, string> Body,
    Func<string, string> Title);

// The Jekyll filters Fluid lacks that site themes commonly use, plus
// Wikidown's own:
//
//   {{ "Venues/Pinebox.md" | wiki_body }}                 rendered body, no breadcrumb
//   {{ "Venues/Pinebox.md" | wiki_body: "strip_title" }}  ...and no first H1
//   {{ "Venues/Pinebox.md" | wiki_title }}                the page's title
//
// Either also takes a page object (from site.pages). Links inside a
// wiki_body are site-absolute, so it can be shown on any page.
public static partial class LiquidFilters
{
    public static void Register(FilterCollection filters, string baseUrl, string siteUrl, WikiLookup wiki)
    {
        filters.AddFilter("relative_url", (input, _, _) => new StringValue(RelativeUrl(input.ToStringValue(), baseUrl)));
        filters.AddFilter("absolute_url", (input, _, _) =>
        {
            var url = input.ToStringValue();
            return new StringValue(UrlScheme().IsMatch(url) ? url : siteUrl + RelativeUrl(url, baseUrl));
        });
        filters.AddFilter("markdownify", (input, _, _) => new StringValue(MarkdownPageRenderer.ToHtml(input.ToStringValue()), false));
        filters.AddFilter("jsonify", (input, _, context) => new StringValue(JsonSerializer.Serialize(ToPlain(input, context)), false));
        filters.AddFilter("slugify", (input, _, _) =>
            new StringValue(NonSlug().Replace(input.ToStringValue().ToLowerInvariant(), "-").Trim('-')));
        filters.AddFilter("xml_escape", (input, _, _) => new StringValue(WebUtility.HtmlEncode(input.ToStringValue()), false));
        filters.AddFilter("cgi_escape", (input, _, _) => new StringValue(WebUtility.UrlEncode(input.ToStringValue())));
        filters.AddFilter("normalize_whitespace", (input, _, _) =>
            new StringValue(Whitespace().Replace(input.ToStringValue(), " ").Trim()));
        filters.AddFilter("number_of_words",(input, _, _) =>
            NumberValue.Create(input.ToStringValue().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length));

        // Fluid's sort compares as strings, so prices 3, 5, 10 came out 10, 3, 5.
        filters.AddFilter("sort", async (input, arguments, context) =>
        {
            if (input.Type != FluidValues.Array) return input;
            var property = arguments.At(0);
            var keyed = new List<(FluidValue Item, FluidValue Key)>();
            foreach (var item in input.Enumerate(context))
                keyed.Add((item, property.IsNil() ? item : await item.GetValueAsync(property.ToStringValue(), context)));
            return new ArrayValue(keyed.OrderBy(k => k.Key, LiquidOrder.Instance).Select(k => k.Item).ToArray());
        });

        filters.AddFilter("wiki_body", async (input, arguments, context) =>
        {
            var link = wiki.Resolve(await PagePathOf(input, context));
            if (link is null) return StringValue.Empty;
            var option = arguments.At(0);
            var stripTitle = option.ToStringValue() == "strip_title" || option.Type == FluidValues.Boolean && option.ToBooleanValue()
                || arguments["strip_title"].ToBooleanValue();
            return new StringValue(wiki.Body(link, stripTitle), false);
        });
        filters.AddFilter("wiki_title", async (input, _, context) =>
        {
            var link = wiki.Resolve(await PagePathOf(input, context));
            return link is null ? StringValue.Empty : new StringValue(wiki.Title(link));
        });
    }

    // Jekyll's relative_url: prefix the baseurl, keeping exactly one slash
    // between them; full URLs pass through.
    private static string RelativeUrl(string url, string baseUrl)
    {
        if (UrlScheme().IsMatch(url) || url.StartsWith("//", StringComparison.Ordinal)) return url;
        return baseUrl + (url.StartsWith('/') ? url : "/" + url);
    }

    private static async ValueTask<string> PagePathOf(FluidValue input, TemplateContext context) =>
        input.Type is FluidValues.Dictionary or FluidValues.Object
            ? (await input.GetValueAsync("path", context)).ToStringValue()
            : input.ToStringValue();

    private static object? ToPlain(FluidValue value, TemplateContext context)
    {
        switch (value.Type)
        {
            case FluidValues.Nil: return null;
            case FluidValues.Boolean: return value.ToBooleanValue();
            case FluidValues.Number: return value.ToNumberValue();
            case FluidValues.String: return value.ToStringValue();
            case FluidValues.Array: return value.Enumerate(context).Select(v => ToPlain(v, context)).ToList();
        }
        return ToPlain(value.ToObjectValue(), context);
    }

    private static object? ToPlain(object? value, TemplateContext context) => value switch
    {
        null => null,
        FluidValue fluid => ToPlain(fluid, context),
        IFluidIndexable indexable => indexable.Keys.ToDictionary(k => k, k => indexable.TryGetValue(k, out var v) ? ToPlain(v, context) : null),
        string or bool or long or int or decimal or double or DateTime or DateTimeOffset => value,
        IDictionary<string, object?> dictionary => dictionary.ToDictionary(kv => kv.Key, kv => ToPlain(kv.Value, context)),
        System.Collections.IEnumerable list => list.Cast<object?>().Select(v => ToPlain(v, context)).ToList(),
        _ => value.ToString(),
    };

    // Numbers numerically, everything else as ordinal strings, nils last.
    private sealed class LiquidOrder : IComparer<FluidValue>
    {
        public static readonly LiquidOrder Instance = new();

        public int Compare(FluidValue? x, FluidValue? y)
        {
            var xNil = x is null || x.IsNil();
            var yNil = y is null || y.IsNil();
            if (xNil || yNil) return xNil.CompareTo(yNil);
            return x!.Type == FluidValues.Number && y!.Type == FluidValues.Number
                ? x.ToNumberValue().CompareTo(y.ToNumberValue())
                : string.CompareOrdinal(x.ToStringValue(), y!.ToStringValue());
        }
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.-]*:")]
    private static partial Regex UrlScheme();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlug();
}

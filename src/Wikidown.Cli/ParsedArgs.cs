namespace Wikidown.Cli;

public sealed class ParsedArgs
{
    private static readonly HashSet<string> BoolFlags = new(StringComparer.Ordinal)
    {
        "stdin", "recursive", "case-sensitive"
    };

    public string Command { get; init; } = "";
    public string Root { get; set; } = "docs";
    public Dictionary<string, string> Options { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Flags { get; } = new(StringComparer.Ordinal);

    public string Require(string name) =>
        Options.TryGetValue(name, out var v)
            ? v
            : throw new CliUsageException($"missing --{name}");

    public string? Optional(string name) =>
        Options.TryGetValue(name, out var v) ? v : null;

    public bool Flag(string name) => Flags.Contains(name);

    public static ParsedArgs Parse(string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("no command given");
        var result = new ParsedArgs { Command = args[0] };

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                throw new CliUsageException($"unexpected argument '{token}'");
            var key = token[2..];

            if (BoolFlags.Contains(key))
            {
                result.Flags.Add(key);
                continue;
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new CliUsageException($"option --{key} requires a value");

            var value = args[++i];
            if (key == "root") result.Root = value;
            else result.Options[key] = value;
        }

        return result;
    }
}

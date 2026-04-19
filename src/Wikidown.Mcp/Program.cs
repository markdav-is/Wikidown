using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wikidown.Core;
using Wikidown.Mcp;

// stdio MCP servers MUST NOT log to stdout — that channel is the protocol.
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var rootPath = ResolveRootPath(args);
builder.Services.AddSingleton(_ => new WikiRepository(rootPath));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

static string ResolveRootPath(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--root") return Path.GetFullPath(args[i + 1]);
    }
    var fromEnv = Environment.GetEnvironmentVariable("WIKIDOWN_ROOT");
    if (!string.IsNullOrWhiteSpace(fromEnv)) return Path.GetFullPath(fromEnv);
    return Path.GetFullPath("docs");
}

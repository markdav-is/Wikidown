using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Wikidown.Api;

public class Ping
{
    [Function("Ping")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ping")] HttpRequest _)
        => new OkObjectResult(new { ok = true, ts = DateTimeOffset.UtcNow });

    [Function("Version")]
    public IActionResult Version(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "version")] HttpRequest _)
        => new OkObjectResult(new { build = "d680857-canary-1", ts = DateTimeOffset.UtcNow });
}

public class GitHubProbe(IHttpClientFactory httpFactory)
{
    [Function("GitHubProbe")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "github-test")] HttpRequest _)
    {
        var started = DateTimeOffset.UtcNow;
        var http = httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(8);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/");
            req.Headers.UserAgent.ParseAdd("Wikidown/1.0");
            using var resp = await http.SendAsync(req);
            return new OkObjectResult(new
            {
                ok = true,
                status = (int)resp.StatusCode,
                elapsedMs = (DateTimeOffset.UtcNow - started).TotalMilliseconds,
            });
        }
        catch (Exception ex)
        {
            return new OkObjectResult(new
            {
                ok = false,
                type = ex.GetType().Name,
                message = ex.Message,
                elapsedMs = (DateTimeOffset.UtcNow - started).TotalMilliseconds,
            });
        }
    }
}

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

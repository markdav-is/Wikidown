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
}

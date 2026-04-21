using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Wikidown.Api.Auth;

public class ConfigEndpoint
{
    [Function("GitHubConfig")]
    public IActionResult GitHub(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "config/github")] HttpRequest _)
    {
        var clientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID") ?? "";
        return new OkObjectResult(new { clientId });
    }

    [Function("AdoConfig")]
    public IActionResult Ado(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "config/ado")] HttpRequest _)
    {
        var clientId = Environment.GetEnvironmentVariable("ADO_CLIENT_ID") ?? "";
        return new OkObjectResult(new { clientId });
    }
}

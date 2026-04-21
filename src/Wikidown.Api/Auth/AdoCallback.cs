using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Wikidown.Api.Auth;

public class AdoCallback(ILogger<AdoCallback> logger)
{
    [Function("AdoCallback")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/ado/callback")] HttpRequest req)
    {
        var code = req.Query["code"].ToString();
        var state = req.Query["state"].ToString();

        if (string.IsNullOrEmpty(code))
        {
            logger.LogWarning("ADO callback invoked without a code parameter");
            return new BadRequestObjectResult(new { error = "missing_code" });
        }

        logger.LogInformation("ADO callback received code (len={Length}) state={State}", code.Length, state);

        return new OkObjectResult(new
        {
            provider = "ado",
            status = "stub",
            receivedCode = true,
            state,
        });
    }
}

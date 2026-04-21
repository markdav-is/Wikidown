using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Wikidown.Api.Auth;

public class GitHubExchange(IHttpClientFactory httpFactory, ILogger<GitHubExchange> logger)
{
    private const string TokenEndpoint = "https://github.com/login/oauth/access_token";

    [Function("GitHubExchange")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/github/exchange")] HttpRequest req)
    {
        ExchangeRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ExchangeRequest>(req.Body);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Exchange body is not valid JSON");
            return new BadRequestObjectResult(new { error = "bad_request" });
        }

        if (body is null || string.IsNullOrEmpty(body.Code))
        {
            return new BadRequestObjectResult(new { error = "missing_code" });
        }

        var clientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET");
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            logger.LogError("GITHUB_CLIENT_ID or GITHUB_CLIENT_SECRET is not configured");
            return new ObjectResult(new { error = "server_not_configured" }) { StatusCode = 500 };
        }

        var http = httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(8);
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("code", body.Code),
            }),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.UserAgent.ParseAdd("Wikidown/1.0");

        TokenResponse? token;
        try
        {
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GitHub token exchange failed with status {Status}", (int)response.StatusCode);
                return new ObjectResult(new { error = "token_exchange_failed" }) { StatusCode = 502 };
            }
            token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GitHub token exchange threw");
            return new ObjectResult(new { error = "token_exchange_threw" }) { StatusCode = 502 };
        }

        if (token is null || string.IsNullOrEmpty(token.AccessToken))
        {
            logger.LogWarning("GitHub token response had no access_token (error={Error})", token?.Error);
            return new OkObjectResult(new { error = token?.Error ?? "no_access_token" });
        }

        return new OkObjectResult(new { token = token.AccessToken });
    }

    private sealed record ExchangeRequest(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("state")] string? State);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("scope")] string? Scope,
        [property: JsonPropertyName("error")] string? Error);
}

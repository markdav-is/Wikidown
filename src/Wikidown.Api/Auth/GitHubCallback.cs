using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Wikidown.Api.Auth;

public class GitHubCallback(IHttpClientFactory httpFactory, ILogger<GitHubCallback> logger)
{
    private const string TokenEndpoint = "https://github.com/login/oauth/access_token";

    [Function("GitHubCallback")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/github/callback")] HttpRequest req)
    {
        var state = req.Query["state"].ToString();
        var code = req.Query["code"].ToString();

        if (string.IsNullOrEmpty(code))
        {
            var providedError = req.Query["error"].ToString();
            return RedirectToConnect(state, error: string.IsNullOrEmpty(providedError) ? "missing_code" : providedError);
        }

        var clientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET");
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            logger.LogError("GITHUB_CLIENT_ID or GITHUB_CLIENT_SECRET is not configured");
            return RedirectToConnect(state, error: "server_not_configured");
        }

        var http = httpFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("code", code),
            }),
        };
        request.Headers.Accept.ParseAdd("application/json");

        TokenResponse? token;
        try
        {
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GitHub token exchange failed with status {Status}", (int)response.StatusCode);
                return RedirectToConnect(state, error: "token_exchange_failed");
            }

            token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GitHub token exchange threw");
            return RedirectToConnect(state, error: "token_exchange_threw");
        }

        if (token is null || string.IsNullOrEmpty(token.AccessToken))
        {
            logger.LogWarning("GitHub token response had no access_token (error={Error})", token?.Error);
            return RedirectToConnect(state, error: token?.Error ?? "no_access_token");
        }

        return RedirectToConnect(state, token: token.AccessToken);
    }

    private static IActionResult RedirectToConnect(string state, string? token = null, string? error = null)
    {
        var fragment = new List<string>();
        if (!string.IsNullOrEmpty(token)) fragment.Add("gh_token=" + Uri.EscapeDataString(token));
        if (!string.IsNullOrEmpty(error)) fragment.Add("gh_error=" + Uri.EscapeDataString(error));
        if (!string.IsNullOrEmpty(state)) fragment.Add("gh_state=" + Uri.EscapeDataString(state));
        var hash = fragment.Count == 0 ? "" : "#" + string.Join("&", fragment);
        return new RedirectResult("/connect" + hash);
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string? AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string? TokenType,
        [property: System.Text.Json.Serialization.JsonPropertyName("scope")] string? Scope,
        [property: System.Text.Json.Serialization.JsonPropertyName("error")] string? Error);
}

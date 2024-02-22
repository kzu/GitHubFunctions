using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GitHubFunctions;

public class Function(ILogger<Function> logger, IConfiguration configuration, IHttpClientFactory httpFactory)
{
    [Function("me")]
    public async Task<IActionResult> EchoAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, FunctionContext context)
    {
        var feature = context.Features.Get<ClaimsFeature>();
        var principal = feature?.Principal ?? req.HttpContext.User;

        if (principal.Identity?.IsAuthenticated != true)
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            // Never redirect requests for JWT, as they are likely from a CLI or other non-browser client.
            if (!req.Headers.Accept.Contains("application/json") &&
                configuration["WEBSITE_AUTH_GITHUB_CLIENT_ID"] is { Length: > 0 } clientId)
            {
                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&scope=read:user%20read:org&redirect_uri=https://{req.Headers["Host"]}/.auth/login/github/callback&state=redir=/sync");
            }

            logger.LogWarning("Ensure WEBSITE_AUTH_GITHUB_CLIENT_ID configuration is present.");

            // Otherwise, just 401
            return new UnauthorizedResult();
        }

        using var http = httpFactory.CreateClient("user");
        var response = await http.GetAsync("https://api.github.com/user");

        return new JsonResult(new
        {
            body = await response.Content.ReadFromJsonAsync<JsonElement>(),
            request = req.Headers.ToDictionary(x => x.Key, x => x.Value.ToString().Trim('"')),
            response = response.Headers.ToDictionary(x => x.Key, x => x.Value?.ToString()?.Trim('"')),
        })
        {
            StatusCode = (int)response.StatusCode
        };
    }
}

using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace GitHubFunctions;

public class Function(ILogger<Function> logger, IConfiguration configuration, IHttpClientFactory httpFactory)
{
    [Function("me")]
    public async Task<IActionResult> EchoAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var clientId = configuration["WEBSITE_AUTH_GITHUB_CLIENT_ID"];
        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogError("Ensure GitHub identity provider is configured for the functions app.");
            return new StatusCodeResult(500);
        }

        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true } principal)
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            // Never redirect requests for JWT, as they are likely from a CLI or other non-browser client.
            if (!req.Headers.Accept.Contains("application/json"))
                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&scope=read:user%20read:org%20user:email&redirect_uri=https://{req.Headers["Host"]}/.auth/login/github/callback&state=redir=/me");

            // Otherwise, just 401
            return new UnauthorizedResult();
        }

        // NOTE: the 'user' HTTP client is configured to automatically retrieve the authenticated user 
        // access token that was populated in the context feature.
        using var http = httpFactory.CreateClient("user");
        var response = await http.GetAsync("https://api.github.com/user");

        var emails = await http.GetFromJsonAsync<JsonArray>("https://api.github.com/user/emails");  
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        body?.Add("emails", emails);

        var claims = principal.Claims.GroupBy(x => x.Type)
                .Select(g => new { g.Key, Value = (object)(g.Count() == 1 ? g.First().Value : g.Select(x => x.Value).ToArray()) })
                .ToDictionary(x => x.Key, x => x.Value);

        // Allows the client to authenticate directly with the OAuth app if needed too.
        claims["client_id"] = clientId; 

        return new JsonResult(new
        {
            body,
            claims,
            request = req.Headers.ToDictionary(x => x.Key, x => x.Value.ToString().Trim('"')),
            response = response.Headers.ToDictionary(x => x.Key, x => string.Join(',', x.Value)),
        })
        {
            StatusCode = (int)response.StatusCode
        };
    }
}

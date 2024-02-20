using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Auth0Functions;

public class Function(ILogger<Function> logger, IConfiguration configuration)
{
    [Function("sync")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, FunctionContext context)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");

        foreach (var entry in configuration.AsEnumerable().OrderBy(x => x.Key))
        {
            logger.LogInformation("{Key} = {Value}", entry.Key, entry.Value);
        }

        var feature = context.Features.Get<PrincipalFeature>();
        var principal = feature?.Principal ?? req.HttpContext.User;

        if (principal.Identity?.IsAuthenticated != true)
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            if (configuration["WEBSITE_AUTH_GITHUB_CLIENT_ID"] is { Length: > 0 } clientId)
                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&redirect_uri=https://{req.Headers["Host"]}/.auth/login/github/callback&state=redir=/sync");

            // Otherwise, just return a 401 with the headers for debugging.
            return new UnauthorizedObjectResult($"Not authenticated :(" +
                "\r\n\r\n-- Headers --\r\n" +
                string.Join(Environment.NewLine, req.Headers.Select(x => $"{x.Key} = {x.Value}")));
        }

        return new OkObjectResult($"Welcome to Azure Functions!" +
            "\r\n\r\n-- Claims --\r\n" +
            string.Join(Environment.NewLine, principal.Claims.Select(x => $"{x.Type} = {x.Value}")) +
            "\r\n\r\n-- Headers --\r\n" +
            string.Join(Environment.NewLine, req.Headers.Select(x => $"{x.Key} = {x.Value}")));
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
            logger.LogTrace("{Key} = {Value}", entry.Key, entry.Value);
        }

        var feature = context.Features.Get<ClaimsFeature>();
        var principal = feature?.Principal ?? req.HttpContext.User;

        if (principal.Identity?.IsAuthenticated != true)
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            // Never redirect requests for JWT, as they are likely from a CLI or other non-browser client.
            if (!req.Headers.Accept.Contains("application/jwt") &&
                configuration["WEBSITE_AUTH_GITHUB_CLIENT_ID"] is { Length: > 0 } clientId)
            {
                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&redirect_uri=https://{req.Headers["Host"]}/.auth/login/github/callback&state=redir=/sync");
            }

            // Otherwise, just return a 401 with the headers for debugging.
            return new UnauthorizedObjectResult(new
            {
                status = "401: Unauthorized",
                headers = req.Headers.ToDictionary(x => x.Key, x => x.Value.ToString().Trim('"'))
            });
        }

        if (req.Headers.Accept.Contains("application/jwt"))
        {
            var token = new JwtSecurityToken(
                // audience: audience,
                // issuer: issuer,
                // signingCredentials: private key...
                claims: principal.Claims
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return new ContentResult 
            {
                Content = jwt, 
                ContentType = "application/jwt",
                StatusCode = 200
            };
        }
        else
        {
            return new JsonResult(new
            {
                claims = principal.Claims.ToDictionary(x => x.Type, x => x.Value),
                headers = req.Headers.ToDictionary(x => x.Key, x => x.Value.ToString().Trim('"'))
            })
            { 
                StatusCode = 200 
            };               
        }
    }
}

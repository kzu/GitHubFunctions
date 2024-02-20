using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Auth0Functions;

public class Function(ILogger<Function> logger)
{
    [Authorize]
    [Function("echo")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req, FunctionContext context)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");

        var feature = context.Features.Get<PrincipalFeature>();
        var principal = feature?.Principal ?? req.HttpContext.User;

        if (principal.Identity?.IsAuthenticated != true)
        {
            return new OkObjectResult($"Not authenticated :(" +
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

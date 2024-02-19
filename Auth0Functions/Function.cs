using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using System.Security.Claims;

namespace Auth0Functions;

public class Function(ILogger<Function> logger)
{
    [Function("echo")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req, FunctionContext context)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");

        var feature = context.Features.Get<PrincipalFeature>();
        if (feature?.Principal.Identity?.IsAuthenticated != true)
        {
            return new OkObjectResult($"Not authenticated :(" +
            Environment.NewLine + "-- Headers --" + Environment.NewLine +
            string.Join(Environment.NewLine, req.Headers.Select(x => $"{x.Key} = {x.Value}")));
        }

        var user = feature.Principal;

        return new OkObjectResult($"Welcome to Azure Functions!" + 
            Environment.NewLine + "-- Claims --" + Environment.NewLine + 
            string.Join(Environment.NewLine, user.Claims.Select(x => $"{x.Type} = {x.Value}")) +
            Environment.NewLine + "-- Headers --" + Environment.NewLine +
            string.Join(Environment.NewLine, req.Headers.Select(x => $"{x.Key} = {x.Value}")));
    }
}

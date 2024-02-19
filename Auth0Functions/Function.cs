using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using System.Security.Claims;

namespace Auth0Functions;

public class Function(ILogger<Function> logger)
{
    [Function("echo")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");

        return new OkObjectResult($"Welcome to Azure Functions!" + string.Join(Environment.NewLine,
            req.Headers.Select(x => $"{x.Key} = {x.Value}")));
    }
}

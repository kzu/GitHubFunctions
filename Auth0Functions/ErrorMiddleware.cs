using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

public class ErrorMiddleware(ILogger<ErrorMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            return next(context);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while processing the request.");
            throw;
        }
    }
}

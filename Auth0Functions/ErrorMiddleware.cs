using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

public class ErrorMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            var logger = context.GetLogger<ErrorMiddleware>();
#if DEBUG
            logger.LogError(e, "Exception: {Exception}", e.ToString());
#else
            logger.LogError(e, "Exception: {Exception}", e.Message);
#endif
            throw;
        }
    }
}

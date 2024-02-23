using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Microsoft.Extensions.Hosting;

public static partial class AppServiceAuthenticationExtensions
{
    public static IFunctionsWorkerApplicationBuilder UseClaimsPrincipal(this IFunctionsWorkerApplicationBuilder builder)
        => builder.UseMiddleware<ClaimsPrincipalMiddleware>();

    class ClaimsPrincipalMiddleware : IFunctionsWorkerMiddleware
    {
        static readonly ClaimsPrincipal empty = new(new ClaimsIdentity());

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            if (context.Features.Get<ClaimsPrincipal>() is { } principal)
                ClaimsPrincipal.ClaimsPrincipalSelector = () => principal;

            await next(context);
        }
    }
}
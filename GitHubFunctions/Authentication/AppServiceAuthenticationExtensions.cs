using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static partial class AppServiceAuthenticationExtensions
{
    public static IFunctionsWorkerApplicationBuilder UseAppServiceAuthentication(this IFunctionsWorkerApplicationBuilder builder)
        => builder.UseMiddleware<ClientPrincipalMiddleware>();

    class ClientPrincipalMiddleware : IFunctionsWorkerMiddleware
    {
        static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            if (context.Features.Get<ClaimsPrincipal>() is not null)
            {
                await next(context);
                return;
            }

            var req = await context.GetHttpRequestDataAsync();
            if (req is not null &&
                req.Headers.ToDictionary(x => x.Key, x => string.Join(',', x.Value), StringComparer.OrdinalIgnoreCase) is var headers &&
                headers.TryGetValue("x-ms-client-principal", out var msclient) &&
                Convert.FromBase64String(msclient) is var decoded &&
                Encoding.UTF8.GetString(decoded) is var json &&
                JsonSerializer.Deserialize<ClientPrincipal>(json, options) is { } cp)
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    cp.claims.Select(c => new Claim(c.typ, c.val)),
                    cp.auth_typ));

                context.Features.Set(principal);

                if (headers.TryGetValue($"x-ms-token-{cp.auth_typ}-access-token", out var token))
                    context.Features.Set(new AccessToken(token, DateTimeOffset.MinValue));
            }

            await next(context);
        }

        record ClientClaim(string typ, string val);
        record ClientPrincipal(string auth_typ, ClientClaim[] claims);
    }
}
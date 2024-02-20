using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace System.Security.Claims;

public class ClientPrincipalMiddleware : IFunctionsWorkerMiddleware
{
    static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (context.Features.Get<ClaimsFeature>() is not null)
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
            var access_token = headers.TryGetValue($"x-ms-token-{cp.auth_typ}-access-token", out var token) ? token : default;
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                cp.claims.Select(c => new Claim(c.typ, c.val)),
                cp.auth_typ));

            context.Features.Set(new ClaimsFeature(principal, access_token));
            await next(context);
            return;
        }

        context.Features.Set(ClaimsFeature.Default);
        await next(context);
        return;
    }

    record ClientClaim(string typ, string val);
    record ClientPrincipal(string auth_typ, ClientClaim[] claims);
}
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace System.Security.Claims;

/// <summary>
/// Holds the authenticated user principal
/// for the request along with the
/// access token they used.
/// </summary>
public class PrincipalFeature(ClaimsPrincipal principal, string? accessToken = default)
{
    public ClaimsPrincipal Principal => principal;

    /// <summary>
    /// The access token that was used for this
    /// request. Can be used to acquire further
    /// access tokens with the on-behalf-of flow.
    /// </summary>
    public string? AccessToken => accessToken;
}

public class PrincipalMiddleware(ILogger<PrincipalMiddleware> logger) : IFunctionsWorkerMiddleware
{
    static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
    static readonly ClaimsPrincipal empty = new(new ClaimsIdentity());

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is not null && 
            req.Headers.ToDictionary(x => x.Key, x => string.Join(',', x.Value), StringComparer.OrdinalIgnoreCase) is var headers && 
            headers.TryGetValue("host", out var host) && 
            headers.TryGetValue("x-ms-client-principal", out var msclient) &&
            Convert.FromBase64String(msclient) is var decoded && 
            Encoding.UTF8.GetString(decoded) is var json &&
            JsonSerializer.Deserialize<ClientPrincipal>(json, options) is { } cp)
        {
            var access_token = headers.TryGetValue($"x-ms-token-{cp.auth_typ}-access-token", out var token) ? token : default;
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                cp.claims.Select(c => new Claim(c.typ, c.val)),
                cp.auth_typ));

            context.Features.Set(new PrincipalFeature(principal, access_token));
            await next(context);
            return;
        }
        else if (req is not null)
        {
            foreach (var header in req.Headers)
            {
                logger.LogDebug("{Header} = {Value}", header.Key, string.Join(',', header.Value));
            }
        }

        context.Features.Set(new PrincipalFeature(empty));
        await next(context);
        return;
    }

    record ClientClaim(string typ, string val);
    record ClientPrincipal(string auth_typ, ClientClaim[] claims);
}
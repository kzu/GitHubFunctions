using System.Net;
using System.Net.Http.Headers;
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

public class PrincipalMiddleware(IHttpClientFactory httpFactory, ILogger<PrincipalMiddleware> logger) : IFunctionsWorkerMiddleware
{
    static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
    static readonly ClaimsPrincipal empty = new(new ClaimsIdentity());

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is not null &&
            req.Headers.ToDictionary(x => x.Key, x => string.Join(',', x.Value), StringComparer.OrdinalIgnoreCase) is var headers)
        {
            if (headers.TryGetValue("x-ms-client-principal", out var msclient) &&
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
            else if (headers.TryGetValue("authorization", out var auth))
            {
                // CLI auth using device flow
                using var http = httpFactory.CreateClient();
                http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SponsorLink", "0.1"));
                http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth);
                var resp = await http.GetAsync("https://api.github.com/user");
                var body = await resp.Content.ReadAsStringAsync();
                if (await http.GetAsync("https://api.github.com/user") is { StatusCode: HttpStatusCode.OK, Content: { } content })
                {
                    var gh = await content.ReadAsStringAsync();
                    var claims = new List<Claim>();
                    var doc = JsonDocument.Parse(gh);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind != JsonValueKind.Object && 
                            prop.Value.ValueKind != JsonValueKind.Array &&
                            prop.Value.ToString() is { Length: > 0 } value)
                        {
                            claims.Add(new Claim(prop.Name, value));
                        }
                    }

                    context.Features.Set(new PrincipalFeature(new ClaimsPrincipal(
                        new ClaimsIdentity(claims, "github"))));

                    await next(context);
                    return;
                }
            }
            else
            {
                foreach (var header in headers)
                {
                    logger.LogDebug("{Header} = {Value}", header.Key, header.Value);
                }
            }
        }

        context.Features.Set(new PrincipalFeature(empty));
        await next(context);
        return;
    }

    record ClientClaim(string typ, string val);
    record ClientPrincipal(string auth_typ, ClientClaim[] claims);
}
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

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

public class PrincipalMiddleware(IHttpClientFactory httpFactory) : IFunctionsWorkerMiddleware
{
    static readonly ClaimsPrincipal empty = new ClaimsPrincipal(new ClaimsIdentity());

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (TryGetSessionFromHeaders(context, out var headers, out var session) && 
            headers.TryGetValue("Host", out var host))
        {
            using var http = httpFactory.CreateClient();
            http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", session);

            var response = await http.GetAsync($"https://{host}/.auth/me");
            if (response.IsSuccessStatusCode)
            {
                var token = await response.Content.ReadAsStringAsync();
                var sessions = JsonSerializer.Deserialize<Session[]>(token, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (sessions is { Length: > 0 })
                {
                    var principal = new ClaimsPrincipal(new ClaimsIdentity(
                        sessions[0].user_claims.Select(c => new Claim(c.typ, c.val)),
                        sessions[0].provider_name));

                    context.Features.Set(new PrincipalFeature(principal, sessions[0].access_token));
                    await next(context);
                    return;
                }
            }
        }

        context.Features.Set(new PrincipalFeature(empty));
        await next(context);
        return;
    }

    static bool TryGetSessionFromHeaders(FunctionContext context, out Dictionary<string, string> headers, out string? token)
    {
        token = default;

        // HTTP headers are in the binding context as a JSON object
        // The first checks ensure that we have the JSON string
        if (!context.BindingContext.BindingData.TryGetValue("Headers", out var headersObj) ||
            headersObj is not string headersStr ||
            JsonSerializer.Deserialize<Dictionary<string, string>>(headersStr) is not { } headersDict)
        {
            headers = [];
            return false;
        }

        headers = new(headersDict, StringComparer.OrdinalIgnoreCase);

        if (!headers.TryGetValue("AppServiceAuthSession", out token))
            return false;

        return true;
    }

    record SessionClaim(string typ, string val);
    record Session(string access_token, string provider_name, SessionClaim[] user_claims);
}
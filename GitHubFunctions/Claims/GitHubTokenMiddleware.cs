using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Azure.Functions.Worker;
using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace System.Security.Claims;

public class GitHubTokenMiddleware(IHttpClientFactory httpFactory) : IFunctionsWorkerMiddleware
{
    const string Scheme = "Bearer ";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (context.Features.Get<ClaimsFeature>() is not null)
        {
            await next(context);
            return;
        }

        var req = await context.GetHttpRequestDataAsync();
        if (req is not null &&
            req.Headers.TryGetValues("Authorization", out var values) && 
            values is { } auths && 
            auths.FirstOrDefault() is { Length: > 0 } auth &&
            auth.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            // CLI auth using device flow
            using var http = httpFactory.CreateClient();
            // TODO: use ThisAssembly for product info
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DSL", "0.2"));
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth);
            var resp = await http.GetAsync("https://api.github.com/user");

            if (resp is { StatusCode: HttpStatusCode.OK, Content: { } content })
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

                context.Features.Set(new ClaimsFeature(new ClaimsPrincipal(
                    new ClaimsIdentity(claims, "github")),
                    auth[Scheme.Length..]));

                await next(context);
                return;
            } 
            else 
            {
                var error = await resp.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(error))
                    error = resp.ReasonPhrase;

                context.InstanceServices.GetRequiredService<ILogger<GitHubTokenMiddleware>>()
                    .LogWarning("Failed to authenticate with GitHub: " + error);
            }
        }

        await next(context);
        return;
    }

    record ClientClaim(string typ, string val);
    record ClientPrincipal(string auth_typ, ClientClaim[] claims);
}
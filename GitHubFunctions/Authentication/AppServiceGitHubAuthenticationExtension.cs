using System.Net.Http.Headers;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Azure.Core;

namespace Microsoft.Extensions.Hosting;

public static partial class AppServiceAuthenticationExtensions
{
    public static IFunctionsWorkerApplicationBuilder UseGitHubAuthentication(this IFunctionsWorkerApplicationBuilder builder)
    {
        builder.UseMiddleware<GitHubTokenMiddleware>();
        builder.Services.AddHttpClient();
        return builder;
    }

    class GitHubTokenMiddleware(IHttpClientFactory httpFactory) : IFunctionsWorkerMiddleware
    {
        static readonly AssemblyName name = typeof(GitHubTokenMiddleware).Assembly.GetName();
        const string Scheme = "Bearer ";

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            if (context.Features.Get<ClaimsPrincipal>() is not null)
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

                http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(name.FullName, name.Version?.ToString()));
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
                            // For compatiblity with the app service principal populated claims.
                            claims.Add(new Claim("urn:github:" + prop.Name, value));
                        }
                    }

                    context.Features.Set(new ClaimsPrincipal(
                        new ClaimsIdentity(claims, "github")));

                    var token = auth[Scheme.Length..];
                    context.Features.Set(new AccessToken(token, DateTimeOffset.MinValue));
                }
            }

            await next(context);
        }
    }
}
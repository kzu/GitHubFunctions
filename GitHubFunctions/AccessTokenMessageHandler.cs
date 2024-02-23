using System.Net.Http.Headers;
using Azure.Core;
using Microsoft.Azure.Functions.Worker;

namespace GitHubFunctions;

/// <summary>
/// Automatically injects the current function invocation's access token into the outgoing http requests.
/// Requires <see cref="IFunctionContextAccessor"/>.
/// </summary>
public class AccessTokenMessageHandler(IFunctionContextAccessor context) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // NOTE: we don't use or check the token's expiration, if any, since we don't do refreshing either. 
        // We keep it simple here.
        if (context.FunctionContext?.Features.Get<AccessToken>() is { } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        return base.SendAsync(request, cancellationToken);
    }
}
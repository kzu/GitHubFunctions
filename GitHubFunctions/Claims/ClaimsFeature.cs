namespace System.Security.Claims;

/// <summary>
/// Holds the authenticated user principal
/// for the request along with the
/// access token they used.
/// </summary>
public class ClaimsFeature(ClaimsPrincipal principal, string? accessToken = default)
{
    public ClaimsPrincipal Principal => principal;

    /// <summary>
    /// The access token that was used for this
    /// request. Can be used to acquire further
    /// access tokens with the on-behalf-of flow.
    /// </summary>
    public string? AccessToken => accessToken;
}
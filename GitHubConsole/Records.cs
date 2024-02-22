record Auth(string device_code, string user_code, string verification_uri, int interval, int expires_in);
record AuthCode(string? access_token, string? token_type, string? scope, AuthError? error, string? error_description, int? interval);
enum AuthError
{
    authorization_pending,
    slow_down,
    expired_token,
    unsupported_grant_type,
    incorrect_client_credentials,
    incorrect_device_code,
    access_denied,
    device_flow_disabled
}
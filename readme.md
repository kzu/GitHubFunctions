# GitHub authentication for Azure Functions

This sample showcases a functions app that can use GitHub authentication 
configured via the Azure Portal, with support for both API-based and 
web-based authentication flows.

## Getting started

1. Fork this repo
2. Create a [new function app](https://portal.azure.com/#create/Microsoft.FunctionApp) in the Azure Portal. 
   > For simplicity, use the consumption plan and ensure .NET and Windows are used for runtime/plan,
   > .NET 8, isolated worker model.

   Note the function app's URL
3. Create a [new GitHub OAuth app](https://github.com/settings/applications/new)
   a. Set the "Authorization callback URL" to the function app's URL with the path `/.auth/login/github/callback`

   Note the app's Client ID and generate a new Client Secret

4. Back in the Azure Portal, configure GitHub login as explained in the 
   [Azure docs](https://learn.microsoft.com/en-us/azure/app-service/configure-authentication-provider-github)
   > Make sure to select `Allow unauthenticated users`, otherwise the CLI/API access will not work.
   > Leave `Token store` checked.
 
5. Finally, deploy from your fork. For simplicity, I choose the App Service Build Service to deploy the function app.

Navigate to the function app's URL plus `/me` to invoke the function. You should be redirected to GitHub to sign in,
and then see your GitHub user info in the browser rendered as JSON.

You can try it out at https://ghauth.azurewebsites.net/me

## Local development

You can run the function app locally, but you won't be able to replicate the web-based authentication flow, 
since the callback URL in the GitHub app will not match your production deployment. This a limitation on the 
GH app side (Auth0 does allow multiple callback URLs, for example). In addition, the function app running 
locally, does not have the plumbing to handle the web-based auth flow that is provided by the Azure App Service.

In order to work around this, the functions app is configured to perform an API-based device flow and store 
the access token as a cookie in the browser. The way to make this work is as follows:

1. Add a secret containing the Client ID from the GitHub app by running this command in the function app's 
   directory:

   ```shell
   dotnet user-secrets set "GitHub:ClientId" "your-client-id"
   ```

2. Run the functions app and watch the console for the device code and URL to authenticate with. 
   The console will render the device code and URL to authenticate with. While you do this manually,     
   the current function invocation will pause execution. If you take too long, it's possible for the 
   function execution to timeout.

Once you authenticate, the token will be saved in a cookie and subsequent calls will use it to authenticate.
The rest of the flow will work just as in the case of the API/CLI-based device flow.


You can test the API/CLI-based device flow, as follows:

1. Set both the console and the functions app as startup projects.
2. Run with F5 (with debugger). This is what causes the console app to use localhost rather than the 
   production URL.
3. The console will prompt for the client ID and perform the device flow auth. Copy the device code that 
   will be rendered in the console and paste it in the browser window that will open.
4. Once autenticated, the credentials will be saved using the GCM-powered 
   [credential store](https://github.com/devlooped/CredentialManager/) and subsequent calls will verify 
   the saved token and use it to authenticate with the function app.


## Notes

The function app highlights how to perform a web-based authentication flow by detecting anonymous requests 
and redirecting just like the `Require authenticated user` setting does. A pair of function middleware 
classes take care of populating the claims in both scenarios so that the function app can depend generically 
on just the `ClaimsFeature` that's added to the `FunctionContext.Features`.

The sample also showcases leveraging the `ClaimsPrincipal.ClaimsPrincipalSelector` to set the 
authenticated user automatically so that functions can basically just depend on `ClaimsPrincipal.Current` 
throughout.
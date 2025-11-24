using RaumbuchService.Services;
using System;
using System.Web;
using System.Web.Http;
using System.Threading.Tasks;

namespace RaumbuchService.Controllers
{
    /// <summary>
    /// Controller for handling Trimble OAuth authentication flow.
    /// </summary>
    [RoutePrefix("api/auth")]
    public class AuthController : ApiController
    {
        private readonly TrimbleAuthService _authService = new TrimbleAuthService();

        /// <summary>
        /// Generates OAuth authorization URL and returns it to the client.
        /// The client will open this URL in a popup or redirect to it.
        /// </summary>
        [HttpGet]
        [Route("login")]
        public IHttpActionResult GetLoginUrl()
        {
            try
            {
                // Generate random state for CSRF protection
                string state = Guid.NewGuid().ToString("N");

                // Get the base URL from the request to construct redirect URI
                var request = HttpContext.Current.Request;
                string baseUrl = $"{request.Url.Scheme}://{request.Url.Authority}";
                string redirectUri = $"{baseUrl}/api/auth/callback";

                // Generate authorization URL
                string authUrl = _authService.GetAuthorizationUrl(state, redirectUri);

                // Store state in session for validation in callback
                HttpContext.Current.Session["oauth_state"] = state;
                HttpContext.Current.Session["oauth_redirect_uri"] = redirectUri;

                return Ok(new
                {
                    success = true,
                    authUrl = authUrl,
                    state = state
                });
            }
            catch (Exception ex)
            {
                return Content(
                    System.Net.HttpStatusCode.InternalServerError,
                    new
                    {
                        success = false,
                        message = $"Error generating login URL: {ex.Message}"
                    }
                );
            }
        }

        /// <summary>
        /// OAuth callback endpoint. Receives authorization code and exchanges it for tokens.
        /// This endpoint is called by Trimble after user authorizes the application.
        /// </summary>
        [HttpGet]
        [Route("callback")]
        public async Task<IHttpActionResult> HandleCallback()
        {
            try
            {
                var request = HttpContext.Current.Request;
                string code = request.QueryString["code"];
                string state = request.QueryString["state"];
                string error = request.QueryString["error"];
                string errorDescription = request.QueryString["error_description"];

                // Check for errors from OAuth provider
                if (!string.IsNullOrWhiteSpace(error))
                {
                    System.Diagnostics.Debug.WriteLine($"OAuth error: {error} - {errorDescription}");
                    return Redirect("/index.html?auth_error=oauth_failed");
                }

                // Validate state parameter (CSRF protection)
                string storedState = HttpContext.Current.Session["oauth_state"] as string;
                if (string.IsNullOrWhiteSpace(storedState) || state != storedState)
                {
                    System.Diagnostics.Debug.WriteLine("State validation failed");
                    return Redirect("/index.html?auth_error=validation_failed");
                }

                // Validate code parameter
                if (string.IsNullOrWhiteSpace(code))
                {
                    System.Diagnostics.Debug.WriteLine("Authorization code not received");
                    return Redirect("/index.html?auth_error=auth_failed");
                }

                // Get the redirect URI used in the authorization request
                string redirectUri = HttpContext.Current.Session["oauth_redirect_uri"] as string;

                // Exchange code for tokens
                var tokenResponse = await _authService.ExchangeCodeForTokensAsync(code, redirectUri);

                // Store tokens in session
                HttpContext.Current.Session["access_token"] = tokenResponse.access_token;
                HttpContext.Current.Session["refresh_token"] = tokenResponse.refresh_token;
                HttpContext.Current.Session["token_expires_at"] = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in);

                // Clear OAuth state from session
                HttpContext.Current.Session.Remove("oauth_state");
                HttpContext.Current.Session.Remove("oauth_redirect_uri");

                // Redirect to main application with success flag only (no token in URL for security)
                return Redirect("/index.html?auth_success=true");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OAuth callback error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return Redirect("/index.html?auth_error=auth_failed");
            }
        }

        /// <summary>
        /// Gets the current access token from session.
        /// </summary>
        [HttpGet]
        [Route("token")]
        public IHttpActionResult GetToken()
        {
            try
            {
                string accessToken = HttpContext.Current.Session["access_token"] as string;
                var expiresAt = HttpContext.Current.Session["token_expires_at"] as DateTime?;

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return Ok(new
                    {
                        success = false,
                        message = "No active session. Please log in."
                    });
                }

                return Ok(new
                {
                    success = true,
                    access_token = accessToken,
                    expires_at = expiresAt
                });
            }
            catch (Exception ex)
            {
                return Content(
                    System.Net.HttpStatusCode.InternalServerError,
                    new
                    {
                        success = false,
                        message = $"Error retrieving token: {ex.Message}"
                    }
                );
            }
        }

        /// <summary>
        /// Refreshes the access token using the refresh token stored in session.
        /// </summary>
        [HttpPost]
        [Route("refresh")]
        public async Task<IHttpActionResult> RefreshToken()
        {
            try
            {
                string refreshToken = HttpContext.Current.Session["refresh_token"] as string;

                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    return Ok(new
                    {
                        success = false,
                        message = "No refresh token available. Please log in again."
                    });
                }

                // Exchange refresh token for new access token
                var tokenResponse = await _authService.RefreshTokenAsync(refreshToken);

                // Update session with new tokens
                HttpContext.Current.Session["access_token"] = tokenResponse.access_token;
                if (!string.IsNullOrWhiteSpace(tokenResponse.refresh_token))
                {
                    HttpContext.Current.Session["refresh_token"] = tokenResponse.refresh_token;
                }
                HttpContext.Current.Session["token_expires_at"] = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in);

                return Ok(new
                {
                    success = true,
                    access_token = tokenResponse.access_token,
                    message = "Token refreshed successfully"
                });
            }
            catch (Exception ex)
            {
                return Content(
                    System.Net.HttpStatusCode.InternalServerError,
                    new
                    {
                        success = false,
                        message = $"Error refreshing token: {ex.Message}"
                    }
                );
            }
        }

        /// <summary>
        /// Logs out the user by clearing the session.
        /// </summary>
        [HttpPost]
        [Route("logout")]
        public IHttpActionResult Logout()
        {
            try
            {
                HttpContext.Current.Session.Remove("access_token");
                HttpContext.Current.Session.Remove("refresh_token");
                HttpContext.Current.Session.Remove("token_expires_at");
                HttpContext.Current.Session.Remove("oauth_state");
                HttpContext.Current.Session.Remove("oauth_redirect_uri");

                return Ok(new
                {
                    success = true,
                    message = "Logged out successfully"
                });
            }
            catch (Exception ex)
            {
                return Content(
                    System.Net.HttpStatusCode.InternalServerError,
                    new
                    {
                        success = false,
                        message = $"Error logging out: {ex.Message}"
                    }
                );
            }
        }
    }
}

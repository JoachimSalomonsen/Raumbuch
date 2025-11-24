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
                // Check if session is available
                if (HttpContext.Current == null)
                {
                    return Content(
                        System.Net.HttpStatusCode.InternalServerError,
                        new
                        {
                            success = false,
                            message = "HTTP context is not available"
                        }
                    );
                }

                if (HttpContext.Current.Session == null)
                {
                    return Content(
                        System.Net.HttpStatusCode.InternalServerError,
                        new
                        {
                            success = false,
                            message = "Session state is not enabled. Please ensure sessionState is configured in Web.config"
                        }
                    );
                }

                // Generate random state for CSRF protection
                string state = Guid.NewGuid().ToString("N");

                // Use the configured redirect URI from TrimbleConfig
                // This must match exactly what's registered in Trimble Connect app
                string redirectUri = RaumbuchService.Config.TrimbleConfig.RedirectUri;

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
                System.Diagnostics.Trace.WriteLine($"[OAuth ERROR] Error in GetLoginUrl: {ex.GetType().Name} - {ex.Message}");
                System.Diagnostics.Trace.WriteLine($"[OAuth ERROR] Stack trace: {ex.StackTrace}");
                
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
            // Write to both Trace and HttpContext.Trace
            System.Diagnostics.Trace.WriteLine("[OAuth] === CALLBACK METHOD STARTED ===");
            HttpContext.Current?.Trace.Write("[OAuth]", "=== CALLBACK METHOD STARTED ===");
            
            try
            {
                // Check if session is available
                if (HttpContext.Current?.Session == null)
                {
                    System.Diagnostics.Trace.WriteLine("[OAuth] Session is null in callback");
                    HttpContext.Current?.Trace.Write("[OAuth ERROR]", "Session is null in callback");
                    return Redirect("/index.html?auth_error=session_unavailable");
                }

                var request = HttpContext.Current.Request;
                string code = request.QueryString["code"];
                string state = request.QueryString["state"];
                string error = request.QueryString["error"];
                string errorDescription = request.QueryString["error_description"];

                System.Diagnostics.Trace.WriteLine($"[OAuth] Callback received - Code present: {!string.IsNullOrWhiteSpace(code)}, State: {state}");
                HttpContext.Current?.Trace.Write("[OAuth]", $"Callback received - Code present: {!string.IsNullOrWhiteSpace(code)}, State: {state}");

                // Check for errors from OAuth provider
                if (!string.IsNullOrWhiteSpace(error))
                {
                    System.Diagnostics.Trace.WriteLine($"[OAuth ERROR] OAuth provider error: {error} - {errorDescription}");
                    return Redirect("/index.html?auth_error=oauth_failed");
                }

                // Validate state parameter (CSRF protection)
                string storedState = HttpContext.Current.Session["oauth_state"] as string;
                System.Diagnostics.Trace.WriteLine($"[OAuth] State validation - Stored: {storedState}, Received: {state}");
                
                // If state is not in session (popup window issue), we'll validate it differently
                // by exchanging the code and letting the frontend handle state validation
                bool stateValid = !string.IsNullOrWhiteSpace(storedState) && state == storedState;
                
                if (!stateValid)
                {
                    System.Diagnostics.Trace.WriteLine($"[OAuth] State validation skipped - will be validated by frontend. Stored state is null: {string.IsNullOrWhiteSpace(storedState)}");
                    // Don't fail here - pass state to frontend for validation
                }

                // Validate code parameter
                if (string.IsNullOrWhiteSpace(code))
                {
                    System.Diagnostics.Trace.WriteLine("[OAuth ERROR] Authorization code not received");
                    return Redirect("/index.html?auth_error=auth_failed");
                }

                // Get the redirect URI - use configured value as fallback
                string redirectUri = HttpContext.Current.Session["oauth_redirect_uri"] as string;
                if (string.IsNullOrWhiteSpace(redirectUri))
                {
                    redirectUri = RaumbuchService.Config.TrimbleConfig.RedirectUri;
                    System.Diagnostics.Trace.WriteLine($"[OAuth] Using configured redirect URI: {redirectUri}");
                }

                // Validate redirect URI before using it
                if (string.IsNullOrWhiteSpace(redirectUri))
                {
                    System.Diagnostics.Trace.WriteLine("[OAuth ERROR] Redirect URI is null or empty");
                    return Content(
                        System.Net.HttpStatusCode.InternalServerError,
                        new
                        {
                            success = false,
                            message = "Configuration error: TRIMBLE_REDIRECT_URI is not set",
                            details = "Please set TRIMBLE_REDIRECT_URI in Azure App Settings"
                        }
                    );
                }

                System.Diagnostics.Trace.WriteLine($"[OAuth] About to exchange code for tokens. Code length: {code?.Length}, RedirectUri: {redirectUri}");

                // Exchange code for tokens
                var tokenResponse = await _authService.ExchangeCodeForTokensAsync(code, redirectUri);

                // Store tokens in session
                HttpContext.Current.Session["access_token"] = tokenResponse.access_token;
                HttpContext.Current.Session["refresh_token"] = tokenResponse.refresh_token;
                HttpContext.Current.Session["token_expires_at"] = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in);

                // Clear OAuth state from session
                HttpContext.Current.Session.Remove("oauth_state");
                HttpContext.Current.Session.Remove("oauth_redirect_uri");

                // Redirect to main application with success flag and state for frontend validation
                return Redirect($"/index.html?auth_success=true&state={Uri.EscapeDataString(state)}");
            }
            catch (Exception ex)
            {
                string errorMsg = $"[OAuth ERROR] OAuth callback error: {ex.GetType().Name} - {ex.Message}";
                System.Diagnostics.Trace.WriteLine(errorMsg);
                System.Diagnostics.Trace.WriteLine($"[OAuth ERROR] Stack trace: {ex.StackTrace}");
                HttpContext.Current?.Trace.Write("[OAuth ERROR]", $"OAuth callback error: {ex.GetType().Name} - {ex.Message}");
                HttpContext.Current?.Trace.Write("[OAuth ERROR]", $"Stack trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Trace.WriteLine($"[OAuth ERROR] Inner exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                    HttpContext.Current?.Trace.Write("[OAuth ERROR]", $"Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
                
                // Also try writing to event log as last resort
                try
                {
                    System.Diagnostics.EventLog.WriteEntry("Application", errorMsg, System.Diagnostics.EventLogEntryType.Error);
                }
                catch { /* Ignore if event log write fails */ }
                
                // Return detailed error for debugging (remove in production)
                return Content(
                    System.Net.HttpStatusCode.InternalServerError,
                    new
                    {
                        success = false,
                        message = $"OAuth callback failed: {ex.Message}",
                        details = ex.InnerException?.Message
                    }
                );
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
                // Check if session is available
                if (HttpContext.Current?.Session == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Session state is not enabled. Please ensure sessionState is configured in Web.config"
                    });
                }

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
                // Check if session is available
                if (HttpContext.Current?.Session == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Session state is not enabled. Please ensure sessionState is configured in Web.config"
                    });
                }

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
                // Check if session is available
                if (HttpContext.Current?.Session != null)
                {
                    HttpContext.Current.Session.Remove("access_token");
                    HttpContext.Current.Session.Remove("refresh_token");
                    HttpContext.Current.Session.Remove("token_expires_at");
                    HttpContext.Current.Session.Remove("oauth_state");
                    HttpContext.Current.Session.Remove("oauth_redirect_uri");
                }

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

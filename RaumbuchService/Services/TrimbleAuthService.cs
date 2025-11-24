using Newtonsoft.Json;
using RaumbuchService.Config;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace RaumbuchService.Services
{
    /// <summary>
    /// Service for handling Trimble OAuth authentication flow.
    /// </summary>
    public class TrimbleAuthService
    {
        // Static HttpClient to avoid socket exhaustion
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Generates the OAuth authorization URL for redirecting user to Trimble login.
        /// </summary>
        /// <param name="state">Random state parameter for CSRF protection</param>
        /// <param name="redirectUri">The redirect URI (defaults to configured value)</param>
        /// <returns>Authorization URL</returns>
        public string GetAuthorizationUrl(string state, string redirectUri = null)
        {
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                redirectUri = TrimbleConfig.RedirectUri;
            }

            string url =
                $"{TrimbleConfig.AuthUrl}?" +
                $"client_id={TrimbleConfig.ClientId}" +
                $"&response_type=code" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope={Uri.EscapeDataString(TrimbleConfig.Scope)}" +
                $"&state={state}";

            return url;
        }

        /// <summary>
        /// Exchanges authorization code for access token and refresh token.
        /// </summary>
        /// <param name="code">Authorization code from OAuth callback</param>
        /// <param name="redirectUri">The redirect URI used in authorization request</param>
        /// <returns>Token response with access_token and refresh_token</returns>
        public async Task<TokenResponse> ExchangeCodeForTokensAsync(string code, string redirectUri = null)
        {
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                redirectUri = TrimbleConfig.RedirectUri;
            }

            // Validate all required configuration values
            string tokenUrl = TrimbleConfig.TokenUrl;
            string clientId = TrimbleConfig.ClientId;
            string clientSecret = TrimbleConfig.ClientSecret;

            if (string.IsNullOrWhiteSpace(tokenUrl))
            {
                throw new Exception("Configuration error: TRIMBLE_TOKEN_URL is not set or is empty");
            }

            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new Exception("Configuration error: TRIMBLE_CLIENT_ID is not set or is empty");
            }

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new Exception("Configuration error: TRIMBLE_CLIENT_SECRET is not set or is empty");
            }

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                throw new Exception("Configuration error: TRIMBLE_REDIRECT_URI is not set or is empty");
            }

            // Trim all values to remove any whitespace that might cause URI format issues
            tokenUrl = tokenUrl?.Trim();
            clientId = clientId?.Trim();
            clientSecret = clientSecret?.Trim();
            redirectUri = redirectUri?.Trim();

            System.Diagnostics.Debug.WriteLine($"Token exchange - TokenUrl: '{tokenUrl}', ClientId: '{clientId?.Substring(0, Math.Min(10, clientId.Length))}...', RedirectUri: '{redirectUri}'");

            // Validate tokenUrl is a valid URI
            if (!Uri.IsWellFormedUriString(tokenUrl, UriKind.Absolute))
            {
                throw new Exception($"Configuration error: TRIMBLE_TOKEN_URL is not a valid URI: '{tokenUrl}'");
            }

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret)
            });

            System.Diagnostics.Debug.WriteLine($"About to POST to: {tokenUrl}");
            
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync(tokenUrl, content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"POST to token URL failed: {ex.Message}");
                throw new Exception($"Failed to connect to token URL '{tokenUrl}': {ex.Message}", ex);
            }

            string json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Token exchange response: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Token exchange failed: {response.StatusCode} - {json}");
            }

            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);
            return tokenResponse;
        }

        /// <summary>
        /// Refreshes an access token using a refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token</param>
        /// <returns>Token response with new access_token</returns>
        public async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("client_id", TrimbleConfig.ClientId),
                new KeyValuePair<string, string>("client_secret", TrimbleConfig.ClientSecret)
            });

            var response = await _httpClient.PostAsync(TrimbleConfig.TokenUrl, content);
            string json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Token refresh failed: {response.StatusCode} - {json}");
            }

            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);
            return tokenResponse;
        }
    }

    /// <summary>
    /// Token response model from Trimble OAuth server.
    /// </summary>
    public class TokenResponse
    {
        public string access_token { get; set; }
        public string refresh_token { get; set; }
        public int expires_in { get; set; }
        public string token_type { get; set; }
    }
}

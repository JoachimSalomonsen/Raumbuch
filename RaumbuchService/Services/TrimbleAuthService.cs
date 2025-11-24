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

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("client_id", TrimbleConfig.ClientId),
                new KeyValuePair<string, string>("client_secret", TrimbleConfig.ClientSecret)
            });

            var response = await _httpClient.PostAsync(TrimbleConfig.TokenUrl, content);
            string json = await response.Content.ReadAsStringAsync();

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

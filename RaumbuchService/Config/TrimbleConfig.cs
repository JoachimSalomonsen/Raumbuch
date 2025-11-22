using System;
using System.Configuration;

namespace RaumbuchService.Config
{
    /// <summary>
    /// Configuration for Trimble Connect API.
    /// Reads from Azure App Settings (Environment Variables).
    /// </summary>
    public static class TrimbleConfig
    {
        /// <summary>
        /// Trimble Connect API Base URL
        /// EU Region: https://app21.connect.trimble.com/tc/api/2.0
        /// US Region: https://app.connect.trimble.com/tc/api/2.0
        /// </summary>
        public static string BaseUrl => 
            GetConfigValue("TRIMBLE_BASE_URL", "https://app21.connect.trimble.com/tc/api/2.0");

        /// <summary>
        /// OAuth Client ID
        /// </summary>
        public static string ClientId => 
            GetConfigValue("TRIMBLE_CLIENT_ID");

        /// <summary>
        /// OAuth Client Secret
        /// </summary>
        public static string ClientSecret => 
            GetConfigValue("TRIMBLE_CLIENT_SECRET");

        /// <summary>
        /// OAuth Authorization URL
        /// </summary>
        public static string AuthUrl => 
            GetConfigValue("TRIMBLE_AUTH_URL", "https://id.trimble.com/oauth/authorize");

        /// <summary>
        /// OAuth Token URL
        /// </summary>
        public static string TokenUrl => 
            GetConfigValue("TRIMBLE_TOKEN_URL", "https://id.trimble.com/oauth/token");

        /// <summary>
        /// OAuth Scope
        /// </summary>
        public static string Scope => 
            GetConfigValue("TRIMBLE_SCOPE", "openid CST-PowerBI");

        /// <summary>
        /// OAuth Redirect URI (for server-side flow, if used)
        /// </summary>
        public static string RedirectUri => 
            GetConfigValue("TRIMBLE_REDIRECT_URI", "http://localhost:5005/callback/");

        private static string GetConfigValue(string key, string defaultValue = null)
        {
            // Try Azure App Settings (Environment Variables) first
            string value = Environment.GetEnvironmentVariable(key);

            // Fallback to Web.config appSettings
            if (string.IsNullOrWhiteSpace(value))
            {
                value = ConfigurationManager.AppSettings[key];
            }

            // Use default if still not found
            if (string.IsNullOrWhiteSpace(value))
            {
                if (defaultValue != null)
                    return defaultValue;

                throw new ConfigurationErrorsException(
                    $"Configuration '{key}' is missing. Add it to Azure App Settings or Web.config.");
            }

            return value;
        }
    }
}

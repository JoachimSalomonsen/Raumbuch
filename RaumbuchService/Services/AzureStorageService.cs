using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaumbuchService.Services
{
    /// <summary>
    /// Service for managing Azure Blob Storage operations for configuration files.
    /// Stores JSON configurations organized by project number.
    /// </summary>
    public class AzureStorageService
    {
        private readonly string _connectionString;
        private readonly string _containerName = "raumbuch-configs";
        private readonly bool _isAzureEnabled;

        public AzureStorageService()
        {
            // Read from environment variable (Azure App Settings) or Web.config
            _connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") 
                ?? ConfigurationManager.AppSettings["AZURE_STORAGE_CONNECTION_STRING"];

            _isAzureEnabled = !string.IsNullOrWhiteSpace(_connectionString);

            if (_isAzureEnabled)
            {
                System.Diagnostics.Debug.WriteLine("Azure Storage enabled");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Azure Storage disabled - running in local mode");
            }
        }

        /// <summary>
        /// Checks if Azure Storage is configured and available.
        /// </summary>
        public bool IsAvailable()
        {
            return _isAzureEnabled;
        }

        /// <summary>
        /// Saves a configuration JSON file to Azure Blob Storage.
        /// Path structure: {projectNumber}/{configName}.json
        /// </summary>
        public async Task<bool> SaveConfigurationAsync(string projectNumber, string configName, string jsonContent)
        {
            if (!_isAzureEnabled)
            {
                throw new InvalidOperationException("Azure Storage is not configured.");
            }

            try
            {
                var blobServiceClient = new BlobServiceClient(_connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

                // Create container if it doesn't exist
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                // Blob name: projectNumber/configName.json
                string blobName = $"{SanitizePath(projectNumber)}/{SanitizeFileName(configName)}.json";
                var blobClient = containerClient.GetBlobClient(blobName);

                // Upload JSON content
                byte[] byteArray = Encoding.UTF8.GetBytes(jsonContent);
                using (var stream = new MemoryStream(byteArray))
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                System.Diagnostics.Debug.WriteLine($"Configuration saved to Azure: {blobName}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving configuration to Azure: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Lists all configuration files for a project.
        /// Returns list of configuration names (without .json extension).
        /// </summary>
        public async Task<List<ConfigurationInfo>> ListConfigurationsAsync(string projectNumber)
        {
            if (!_isAzureEnabled)
            {
                throw new InvalidOperationException("Azure Storage is not configured.");
            }

            try
            {
                var blobServiceClient = new BlobServiceClient(_connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

                // Check if container exists
                if (!await containerClient.ExistsAsync())
                {
                    return new List<ConfigurationInfo>();
                }

                var configurations = new List<ConfigurationInfo>();
                string prefix = $"{SanitizePath(projectNumber)}/";

                await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: prefix))
                {
                    if (blobItem.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract config name from blob path
                        string fileName = Path.GetFileName(blobItem.Name);
                        string configName = Path.GetFileNameWithoutExtension(fileName);

                        configurations.Add(new ConfigurationInfo
                        {
                            Name = configName,
                            BlobName = blobItem.Name,
                            LastModified = blobItem.Properties.LastModified?.DateTime,
                            Size = blobItem.Properties.ContentLength ?? 0
                        });
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Found {configurations.Count} configurations for project {projectNumber}");
                return configurations.OrderByDescending(c => c.LastModified).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error listing configurations from Azure: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Loads a configuration file from Azure Blob Storage.
        /// Returns the JSON content as string.
        /// </summary>
        public async Task<string> LoadConfigurationAsync(string projectNumber, string configName)
        {
            if (!_isAzureEnabled)
            {
                throw new InvalidOperationException("Azure Storage is not configured.");
            }

            try
            {
                var blobServiceClient = new BlobServiceClient(_connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

                string blobName = $"{SanitizePath(projectNumber)}/{SanitizeFileName(configName)}.json";
                var blobClient = containerClient.GetBlobClient(blobName);

                // Check if blob exists
                if (!await blobClient.ExistsAsync())
                {
                    throw new FileNotFoundException($"Configuration '{configName}' not found.");
                }

                // Download and read content
                var response = await blobClient.DownloadAsync();
                using (var streamReader = new StreamReader(response.Value.Content))
                {
                    string content = await streamReader.ReadToEndAsync();
                    System.Diagnostics.Debug.WriteLine($"Configuration loaded from Azure: {blobName}");
                    return content;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading configuration from Azure: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a configuration file from Azure Blob Storage.
        /// </summary>
        public async Task<bool> DeleteConfigurationAsync(string projectNumber, string configName)
        {
            if (!_isAzureEnabled)
            {
                throw new InvalidOperationException("Azure Storage is not configured.");
            }

            try
            {
                var blobServiceClient = new BlobServiceClient(_connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

                string blobName = $"{SanitizePath(projectNumber)}/{SanitizeFileName(configName)}.json";
                var blobClient = containerClient.GetBlobClient(blobName);

                bool deleted = await blobClient.DeleteIfExistsAsync();
                
                if (deleted)
                {
                    System.Diagnostics.Debug.WriteLine($"Configuration deleted from Azure: {blobName}");
                }
                
                return deleted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting configuration from Azure: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sanitizes project number/path for use in blob storage.
        /// </summary>
        private string SanitizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty.");
            }

            // Remove invalid characters for blob paths
            var invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new string(path.Where(c => !invalidChars.Contains(c) && c != '/' && c != '\\').ToArray());
            
            return sanitized;
        }

        /// <summary>
        /// Sanitizes file name for use in blob storage.
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be empty.");
            }

            // Remove invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            
            return sanitized;
        }
    }

    /// <summary>
    /// Information about a stored configuration file.
    /// </summary>
    public class ConfigurationInfo
    {
        public string Name { get; set; }
        public string BlobName { get; set; }
        public DateTime? LastModified { get; set; }
        public long Size { get; set; }
    }
}

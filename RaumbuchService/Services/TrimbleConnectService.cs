using Newtonsoft.Json;
using RaumbuchService.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace RaumbuchService.Services
{
    /// <summary>
    /// Service wrapper for Trimble Connect Core API (2.0).
    /// Supports:
    ///   - File listing, download, upload
    ///   - TODO creation
    /// </summary>
    public class TrimbleConnectService
    {
        private readonly HttpClient _http;
        private readonly string _token;
        public string BaseUrl { get; }

        public TrimbleConnectService(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentNullException(nameof(accessToken));

            _token = accessToken;
            _http = new HttpClient();
            BaseUrl = TrimbleConfig.BaseUrl.TrimEnd('/');
        }

        // --------------------------------------------------------------------
        //  FILE OPERATIONS
        // --------------------------------------------------------------------

        /// <summary>
        /// Lists all files in a folder.
        /// </summary>
        public async Task<List<TcFileInfo>> ListFilesInFolderAsync(string folderId)
        {
            string url = $"{BaseUrl}/folders/{folderId}/items";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string txt = await response.Content.ReadAsStringAsync();
                throw new Exception($"Fehler beim Laden der Ordnerinhalte: {response.StatusCode}\n{txt}");
            }

            string json = await response.Content.ReadAsStringAsync();
            var items = JsonConvert.DeserializeObject<List<TcFileInfo>>(json) ?? new List<TcFileInfo>();

            return items.Where(i => string.Equals(i.Type, "FILE", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Downloads a file and returns the local path.
        /// </summary>
        public async Task<string> DownloadFileAsync(string fileId, string targetFolder, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileId))
                throw new ArgumentNullException(nameof(fileId));

            Directory.CreateDirectory(targetFolder);

            string metaUrl = $"{BaseUrl}/files/fs/{fileId}/downloadurl";

            var request = new HttpRequestMessage(HttpMethod.Get, metaUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string txt = await response.Content.ReadAsStringAsync();
                throw new Exception($"Fehler beim Herunterladen der Datei: {response.StatusCode}\n{txt}");
            }

            string json = await response.Content.ReadAsStringAsync();
            var dl = JsonConvert.DeserializeObject<DownloadUrlResponse>(json);

            if (dl == null || string.IsNullOrWhiteSpace(dl.url))
                throw new Exception("Download-URL fehlt in der Antwort.");

            string localPath = Path.Combine(targetFolder, fileName);

            using (var client = new HttpClient())
            {
                var fileResponse = await client.GetAsync(dl.url);

                if (!fileResponse.IsSuccessStatusCode)
                {
                    string txt = await fileResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Fehler beim Herunterladen der Datei: {fileResponse.StatusCode}\n{txt}");
                }

                byte[] bytes = await fileResponse.Content.ReadAsByteArrayAsync();
                File.WriteAllBytes(localPath, bytes);
            }

            return localPath;
        }

        /// <summary>
        /// Uploads a file to a folder (creates new version if file with same name exists).
        /// Returns the Trimble Connect file ID (short format like "0ZErihgjtaM"), not the upload GUID.
        /// </summary>
        public async Task<string> UploadFileAsync(string folderId, string localPath)
        {
            if (string.IsNullOrWhiteSpace(folderId))
                throw new ArgumentNullException(nameof(folderId));
            if (!File.Exists(localPath))
                throw new FileNotFoundException("Lokale Datei nicht gefunden.", localPath);

            string fileName = Path.GetFileName(localPath);

            System.Diagnostics.Debug.WriteLine($"========== UploadFileAsync START ==========");
            System.Diagnostics.Debug.WriteLine($"Folder ID: {folderId}");
            System.Diagnostics.Debug.WriteLine($"File name: {fileName}");
            System.Diagnostics.Debug.WriteLine($"Local path: {localPath}");

            // Step 1: initiate upload
            string initUrl = $"{BaseUrl}/files/fs/upload?parentId={folderId}&parentType=FOLDER";

            var payload = new { name = fileName };
            string body = JsonConvert.SerializeObject(payload);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, initUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Content = content;

            System.Diagnostics.Debug.WriteLine("Step 1: Initiating upload...");
            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string txt = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Upload init failed: {response.StatusCode}\n{txt}");
                throw new Exception($"Fehler beim Initialisieren des Uploads: {response.StatusCode}\n{txt}");
            }

            string json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Upload init response: {json}");
            
            var init = JsonConvert.DeserializeObject<UploadInitResponse>(json);

            if (init == null || init.contents == null || init.contents.Count == 0)
                throw new Exception("Keine Upload-Informationen erhalten.");

            string uploadUrl = init.contents[0].url;
            if (string.IsNullOrWhiteSpace(uploadUrl))
                throw new Exception("Upload-URL fehlt in der Antwort.");

            System.Diagnostics.Debug.WriteLine($"Upload ID from init: {init.id}");
            System.Diagnostics.Debug.WriteLine($"Upload URL: {uploadUrl}");

            // Step 2: PUT file bytes
            System.Diagnostics.Debug.WriteLine("Step 2: Uploading file bytes...");
            using (var client = new HttpClient())
            using (var fs = File.OpenRead(localPath))
            {
                var streamContent = new StreamContent(fs);
                var putResponse = await client.PutAsync(uploadUrl, streamContent);

                if (!putResponse.IsSuccessStatusCode)
                {
                    string txt = await putResponse.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Upload PUT failed: {putResponse.StatusCode}\n{txt}");
                    throw new Exception($"Fehler beim Hochladen: {putResponse.StatusCode}\n{txt}");
                }
                
                System.Diagnostics.Debug.WriteLine("File bytes uploaded successfully");
            }

            // Step 3: Query folder to get the actual file ID
            // Try multiple times with increasing delays
            System.Diagnostics.Debug.WriteLine("Step 3: Querying folder to get file ID...");
            
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                System.Diagnostics.Debug.WriteLine($"Query attempt {attempt}/5");
                
                // Wait before querying (exponential backoff)
                int delayMs = attempt * 500; // 500ms, 1000ms, 1500ms, 2000ms, 2500ms
                await Task.Delay(delayMs);
                
                string folderUrl = $"{BaseUrl}/folders/{folderId}/items";
                var folderRequest = new HttpRequestMessage(HttpMethod.Get, folderUrl);
                folderRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                
                var folderResponse = await _http.SendAsync(folderRequest);
                
                if (!folderResponse.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Folder query failed with status {folderResponse.StatusCode}");
                    continue;
                }
                
                string folderJson = await folderResponse.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Folder items response (first 500 chars): {(folderJson.Length > 500 ? folderJson.Substring(0, 500) : folderJson)}");
                
                var items = JsonConvert.DeserializeObject<List<TcFileInfo>>(folderJson) ?? new List<TcFileInfo>();
                System.Diagnostics.Debug.WriteLine($"Found {items.Count} items in folder");
                
                // Find the file by name (case-insensitive)
                var uploadedFile = items.FirstOrDefault(f => 
                    string.Equals(f.Name, fileName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Type, "FILE", StringComparison.OrdinalIgnoreCase));
                
                if (uploadedFile != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Found matching file:");
                    System.Diagnostics.Debug.WriteLine($"  Name: {uploadedFile.Name}");
                    System.Diagnostics.Debug.WriteLine($"  ID: {uploadedFile.Id}");
                    System.Diagnostics.Debug.WriteLine($"  Type: {uploadedFile.Type}");
                    
                    if (!string.IsNullOrWhiteSpace(uploadedFile.Id))
                    {
                        System.Diagnostics.Debug.WriteLine($"SUCCESS: Found file ID: {uploadedFile.Id}");
                        System.Diagnostics.Debug.WriteLine("========== UploadFileAsync END (Success) ==========");
                        return uploadedFile.Id;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"File '{fileName}' not found in folder yet. Will retry...");
                    
                    // Log all files in folder for debugging
                    foreach (var item in items.Take(5))
                    {
                        System.Diagnostics.Debug.WriteLine($"  Item: Name='{item.Name}', Type={item.Type}, ID={item.Id}");
                    }
                }
            }
            
            // Fallback: return upload ID if we couldn't find the file after all attempts
            System.Diagnostics.Debug.WriteLine($"ERROR: Could not find file '{fileName}' in folder after 5 attempts.");
            System.Diagnostics.Debug.WriteLine($"Returning upload ID as fallback: {init.id}");
            System.Diagnostics.Debug.WriteLine("========== UploadFileAsync END (Fallback) ==========");
            return init.id ?? init.uploadId;
        }

        // --------------------------------------------------------------------
        //  TODO OPERATIONS
        // --------------------------------------------------------------------

        /// <summary>
        /// Gets project info to verify project ID format.
        /// </summary>
        public async Task<string> GetProjectInfoAsync(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentNullException(nameof(projectId));

            // Try with regional endpoint first
            string url = $"{BaseUrl}/projects/{projectId}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string txt = await response.Content.ReadAsStringAsync();
                throw new Exception($"Fehler beim Laden des Projekts: {response.StatusCode}\n{txt}");
            }

            string json = await response.Content.ReadAsStringAsync();
            return json;
        }

        /// <summary>
        /// Gets list of users in a project.
        /// API: GET /projects/{projectId}/users
        /// </summary>
        public async Task<List<ProjectUser>> GetProjectUsersAsync(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentNullException(nameof(projectId));

            string url = $"{BaseUrl}/projects/{projectId}/users";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string txt = await response.Content.ReadAsStringAsync();
                throw new Exception($"Fehler beim Laden der Projektbenutzer: {response.StatusCode}\n{txt}");
            }

            string json = await response.Content.ReadAsStringAsync();
            var users = JsonConvert.DeserializeObject<List<ProjectUser>>(json) ?? new List<ProjectUser>();

            return users;
        }

        /// <summary>
        /// Gets a file's download URL that can be shared via email.
        /// API: GET /files/fs/{fileId}/downloadurl
        /// </summary>
        public async Task<string> GetFileDownloadUrlAsync(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
                throw new ArgumentNullException(nameof(fileId));

            string metaUrl = $"{BaseUrl}/files/fs/{fileId}/downloadurl";

            var request = new HttpRequestMessage(HttpMethod.Get, metaUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string txt = await response.Content.ReadAsStringAsync();
                throw new Exception($"Fehler beim Abrufen der Download-URL: {response.StatusCode}\n{txt}");
            }

            string json = await response.Content.ReadAsStringAsync();
            var dl = JsonConvert.DeserializeObject<DownloadUrlResponse>(json);

            if (dl == null || string.IsNullOrWhiteSpace(dl.url))
                throw new Exception("Download-URL fehlt in der Antwort.");

            return dl.url;
        }

        /// <summary>
        /// Gets the original filename from Trimble Connect file metadata.
        /// API: GET /files/{fileId}
        /// </summary>
        public async Task<string> GetFileNameAsync(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
                throw new ArgumentNullException(nameof(fileId));

            string metaUrl = $"{BaseUrl}/files/{fileId}";

            var request = new HttpRequestMessage(HttpMethod.Get, metaUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string txt = await response.Content.ReadAsStringAsync();
                throw new Exception($"Fehler beim Abrufen der Datei-Metadaten: {response.StatusCode}\n{txt}");
            }

            string json = await response.Content.ReadAsStringAsync();
            var fileInfo = JsonConvert.DeserializeObject<TcFileInfo>(json);

            if (fileInfo == null || string.IsNullOrWhiteSpace(fileInfo.Name))
                throw new Exception("Dateiname fehlt in der Antwort.");

            return fileInfo.Name;
        }

        /// <summary>
        /// Creates a TODO in Trimble Connect.
        /// API: POST /todos
        /// Required fields: description, projectId
        /// NOTE: This is kept for backward compatibility. New code should use email notifications instead.
        /// </summary>
        public async Task<string> CreateTodoAsync(
            string projectId,
            string title,
            List<string> assignees,
            string label = null,
            string priority = "NORMAL",
            string dueDate = null)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentNullException(nameof(projectId));

            // Use same regional endpoint as other APIs (app21 for EU)
            string url = $"{BaseUrl}/todos";

            // Build payload with optional fields
            var payload = new
            {
                description = title ?? "Raumprogramm wurde erfolgreich erstellt",
                projectId = projectId,
                title = title ?? "Raumprogramm wurde erstellt",
                assignedTo = assignees ?? new List<string>(),
                dueDate = dueDate  // ISO 8601 format: yyyy-MM-dd
            };

            string body = JsonConvert.SerializeObject(payload);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Content = content;

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string txt = await response.Content.ReadAsStringAsync();
                throw new Exception($"Fehler beim Erstellen der Aufgabe: {response.StatusCode}\n{txt}");
            }

            string json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<TodoResponse>(json);

            return result?.id;
        }

        /// <summary>
        /// Creates a BCF Topic in Trimble Connect.
        /// API: POST https://open21.connect.trimble.com/bcf/2.1/projects/{projectId}/topics
        /// This is the preferred method for notifications as it's natively integrated with Trimble Connect.
        /// </summary>
        public async Task<string> CreateBcfTopicAsync(
            string projectId,
            string title,
            string description,
            string assignedTo,
            List<string> referenceLinks = null,
            string topicType = "Request",
            string topicStatus = "New",
            string priority = null,
            string dueDate = null)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentNullException(nameof(projectId));

            // BCF API is ALWAYS on open21.connect.trimble.com/bcf/2.1 (EU region)
            // Do NOT use BaseUrl replacement - hardcode the BCF endpoint
            string bcfBaseUrl = "https://open21.connect.trimble.com/bcf/2.1";
            string url = $"{bcfBaseUrl}/projects/{projectId}/topics";

            System.Diagnostics.Debug.WriteLine($"========== CreateBcfTopicAsync START ==========");
            System.Diagnostics.Debug.WriteLine($"BCF URL: {url}");
            System.Diagnostics.Debug.WriteLine($"Project ID: {projectId}");
            System.Diagnostics.Debug.WriteLine($"Title: {title}");
            System.Diagnostics.Debug.WriteLine($"Assigned To: {assignedTo}");

            // Build payload - only include fields that have values
            dynamic payload = new System.Dynamic.ExpandoObject();
            var payloadDict = (IDictionary<string, object>)payload;
            
            payloadDict["title"] = title;
            payloadDict["topic_type"] = topicType;
            payloadDict["topic_status"] = topicStatus;
            
            if (!string.IsNullOrWhiteSpace(description))
            {
                payloadDict["description"] = description;
            }
            
            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                payloadDict["assigned_to"] = assignedTo;
            }
            
            if (!string.IsNullOrWhiteSpace(priority))
            {
                payloadDict["priority"] = priority;
            }
            
            if (!string.IsNullOrWhiteSpace(dueDate))
            {
                payloadDict["due_date"] = dueDate;
            }

            // Note: reference_links are added separately via document_references endpoint
            // This is the correct BCF API approach

            string body = JsonConvert.SerializeObject(payload);
            System.Diagnostics.Debug.WriteLine($"Request body: {body}");
            
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Content = content;

            System.Diagnostics.Debug.WriteLine("Sending BCF API request...");

            var response = await _http.SendAsync(request);

            System.Diagnostics.Debug.WriteLine($"BCF API response status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                string txt = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"BCF API error response: {txt}");
                System.Diagnostics.Debug.WriteLine("========== CreateBcfTopicAsync END (Error) ==========");
                throw new Exception($"Fehler beim Erstellen des BCF Topics: {response.StatusCode}\n{txt}");
            }

            string json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"BCF API success response: {json}");
            
            var result = JsonConvert.DeserializeObject<BcfTopicResponse>(json);

            System.Diagnostics.Debug.WriteLine($"Topic GUID: {result?.guid}");
            System.Diagnostics.Debug.WriteLine("========== CreateBcfTopicAsync END (Success) ==========");

            return result?.guid;
        }

        /// <summary>
        /// Adds a document reference to an existing BCF Topic.
        /// API: POST /bcf/2.1/projects/{projectId}/topics/{topicGuid}/document_references
        /// </summary>
        public async Task<string> AddBcfDocumentReferenceAsync(
            string projectId,
            string topicGuid,
            string documentUrl,
            string description = null)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentNullException(nameof(projectId));
            if (string.IsNullOrWhiteSpace(topicGuid))
                throw new ArgumentNullException(nameof(topicGuid));
            if (string.IsNullOrWhiteSpace(documentUrl))
                throw new ArgumentNullException(nameof(documentUrl));

            // BCF API is ALWAYS on open21.connect.trimble.com/bcf/2.1 (EU region)
            string bcfBaseUrl = "https://open21.connect.trimble.com/bcf/2.1";
            string url = $"{bcfBaseUrl}/projects/{projectId}/topics/{topicGuid}/document_references";

            System.Diagnostics.Debug.WriteLine($"========== AddBcfDocumentReferenceAsync START ==========");
            System.Diagnostics.Debug.WriteLine($"Document reference URL: {url}");

            var payload = new
            {
                url = documentUrl,
                description = description ?? documentUrl
            };

            string body = JsonConvert.SerializeObject(payload);
            System.Diagnostics.Debug.WriteLine($"Request body: {body}");
            
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Content = content;

            var response = await _http.SendAsync(request);

            System.Diagnostics.Debug.WriteLine($"Document reference response status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                string txt = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Document reference error: {txt}");
                System.Diagnostics.Debug.WriteLine("========== AddBcfDocumentReferenceAsync END (Error) ==========");
                throw new Exception($"Fehler beim Hinzuf�gen der Dokumentreferenz: {response.StatusCode}\n{txt}");
            }

            string json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Document reference success response: {json}");
            
            var result = JsonConvert.DeserializeObject<BcfDocumentReferenceResponse>(json);

            System.Diagnostics.Debug.WriteLine($"Document reference GUID: {result?.guid}");
            System.Diagnostics.Debug.WriteLine("========== AddBcfDocumentReferenceAsync END (Success) ==========");

            return result?.guid;
        }

        // --------------------------------------------------------------------
        //  HELPER CLASSES
        // --------------------------------------------------------------------

        private class DownloadUrlResponse
        {
            public string id { get; set; }
            public string versionId { get; set; }
            public string url { get; set; }
        }

        private class UploadInitResponse
        {
            public string id { get; set; }
            public string name { get; set; }
            public string uploadId { get; set; }
            public string parentId { get; set; }
            public string parentType { get; set; }
            public string projectId { get; set; }
            public string status { get; set; }
            public List<UploadContent> contents { get; set; }
        }

        private class UploadContent
        {
            public bool fileset { get; set; }
            public bool multipart { get; set; }
            public string type { get; set; }
            public string url { get; set; }
            public string status { get; set; }
        }

        private class TodoResponse
        {
            public string id { get; set; }
            public string title { get; set; }
        }

        private class BcfTopicResponse
        {
            public string guid { get; set; }
            public string title { get; set; }
            public string topic_type { get; set; }
            public string topic_status { get; set; }
        }

        private class BcfDocumentReferenceResponse
        {
            public string guid { get; set; }
            public string url { get; set; }
            public string description { get; set; }
        }
    }

    // ------------------------------------------------------------------------
    //  PUBLIC DATA MODELS
    // ------------------------------------------------------------------------

    public class TcFileInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("versionId")]
        public string VersionId { get; set; }

        [JsonIgnore]
        public string Extension
        {
            get
            {
                if (string.IsNullOrEmpty(Name))
                    return null;
                var ext = Path.GetExtension(Name);
                return string.IsNullOrEmpty(ext) ? null : ext.TrimStart('.').ToLowerInvariant();
            }
        }

        public override string ToString() => Name ?? Id;
    }

    public class ProjectUser
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        // API returns "firstName" - map it to this property
        [JsonProperty("firstName")]
        public string FirstName { get; set; }

        // API returns "lastName" - map it to this property
        [JsonProperty("lastName")]
        public string LastName { get; set; }

        // API may return "name" - map it to this property
        [JsonProperty("name")]
        public string Name { get; set; }

        // API may return "givenName" for some endpoints
        [JsonProperty("givenName")]
        public string GivenName { get; set; }

        // API may return "familyName" for some endpoints
        [JsonProperty("familyName")]
        public string FamilyName { get; set; }

        public string DisplayName
        {
            get
            {
                // If Name is provided and not empty, use it
                if (!string.IsNullOrWhiteSpace(Name))
                    return Name.Trim();

                // Try FirstName and LastName first (from /projects/{id}/users endpoint)
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(FirstName))
                    parts.Add(FirstName.Trim());
                if (!string.IsNullOrWhiteSpace(LastName))
                    parts.Add(LastName.Trim());

                if (parts.Count > 0)
                {
                    return string.Join(" ", parts);
                }

                // Fallback to GivenName and FamilyName (from other endpoints)
                if (!string.IsNullOrWhiteSpace(GivenName))
                    parts.Add(GivenName.Trim());
                if (!string.IsNullOrWhiteSpace(FamilyName))
                    parts.Add(FamilyName.Trim());

                if (parts.Count > 0)
                {
                    return string.Join(" ", parts);
                }
                
                // Last resort: use email prefix and make it more readable
                if (!string.IsNullOrWhiteSpace(Email))
                {
                    var emailPrefix = Email.Split('@')[0];
                    // Replace dots and underscores with spaces, capitalize first letter of each word
                    var readable = emailPrefix.Replace('.', ' ').Replace('_', ' ');
                    return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(readable.ToLower());
                }

                return "Unknown User";
            }
        }

        public override string ToString() => $"{DisplayName} ({Email})";
    }
}

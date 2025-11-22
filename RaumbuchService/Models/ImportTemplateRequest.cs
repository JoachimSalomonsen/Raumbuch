using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for importing a Raumbuch template and creating Raumprogramm.
    /// </summary>
    public class ImportTemplateRequest
    {
        /// <summary>
        /// Trimble Connect access token
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Project ID in Trimble Connect
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Template file ID (Excel file in Trimble Connect)
        /// </summary>
        public string TemplateFileId { get; set; }

        /// <summary>
        /// Target folder ID where Raumprogramm.xlsx will be created
        /// </summary>
        public string TargetFolderId { get; set; }
    }

    public class ImportTemplateResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string RaumprogrammFileId { get; set; }
        public string RaumprogrammFileName { get; set; }
    }
}

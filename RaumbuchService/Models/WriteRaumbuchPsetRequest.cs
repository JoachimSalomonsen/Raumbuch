using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for writing Pset "Raumbuch" to IFC file.
    /// </summary>
    public class WriteRaumbuchPsetRequest
    {
        /// <summary>
        /// Trimble Connect access token
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// IFC file ID in Trimble Connect
        /// </summary>
        public string IfcFileId { get; set; }

        /// <summary>
        /// Raumbuch.xlsx file ID in Trimble Connect
        /// </summary>
        public string RaumbuchFileId { get; set; }

        /// <summary>
        /// Target folder ID where updated IFC will be uploaded
        /// </summary>
        public string TargetFolderId { get; set; }
    }

    public class WriteRaumbuchPsetResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string UpdatedIfcFileId { get; set; }
        public int RoomsUpdated { get; set; }
        public int RoomsSkipped { get; set; }
        public List<string> Warnings { get; set; }
    }
}

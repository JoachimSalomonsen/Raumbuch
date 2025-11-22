using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for analyzing rooms and writing Pset "Überprüfung der Raumkategorie" to IFC.
    /// </summary>
    public class AnalyzeRoomsRequest
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
        /// IFC file ID in Trimble Connect
        /// </summary>
        public string IfcFileId { get; set; }

        /// <summary>
        /// Raumbuch file ID (contains SOLL/IST analysis)
        /// </summary>
        public string RaumbuchFileId { get; set; }

        /// <summary>
        /// Target folder ID where updated IFC will be uploaded
        /// </summary>
        public string TargetFolderId { get; set; }
    }

    public class AnalyzeRoomsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string UpdatedIfcFileId { get; set; }
        public int RoomsMarked { get; set; }
        public List<string> MarkedRoomNames { get; set; }
    }
}

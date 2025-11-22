using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for updating an existing Raumbuch.xlsx file with new IFC and Raumprogramm data.
    /// </summary>
    public class UpdateRaumbuchRequest
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
        /// Existing Raumbuch file ID in Trimble Connect
        /// </summary>
        public string RaumbuchFileId { get; set; }

        /// <summary>
        /// IFC file ID in Trimble Connect
        /// </summary>
        public string IfcFileId { get; set; }

        /// <summary>
        /// Raumprogramm file ID (Excel, SOLL data)
        /// </summary>
        public string RaumprogrammFileId { get; set; }

        /// <summary>
        /// Target folder ID where updated Raumbuch.xlsx will be saved
        /// </summary>
        public string TargetFolderId { get; set; }
    }

    public class UpdateRaumbuchResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string RaumbuchFileId { get; set; }
        public string RaumbuchFileName { get; set; }
        public List<RoomCategoryAnalysis> Analysis { get; set; }
        public int RoomsUpdated { get; set; }
        public int RoomsAdded { get; set; }
        public int RoomsUnchanged { get; set; }
    }
}

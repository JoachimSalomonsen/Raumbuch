using System;
using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for filling inventory from IFC files into Raumbuch room sheets.
    /// </summary>
    public class FillInventoryRequest
    {
        /// <summary>
        /// Trimble Connect access token
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Raumbuch Excel file ID in Trimble Connect
        /// </summary>
        public string RaumbuchFileId { get; set; }

        /// <summary>
        /// List of IFC file IDs to read inventory from
        /// </summary>
        public List<string> IfcFileIds { get; set; }

        /// <summary>
        /// Partial Pset name to search for (e.g. "Plancal nova")
        /// </summary>
        public string PsetPartialName { get; set; }

        /// <summary>
        /// Property name for room identification (e.g. "Room Nbr")
        /// </summary>
        public string RoomPropertyName { get; set; }

        /// <summary>
        /// Target folder ID where updated Raumbuch will be uploaded
        /// </summary>
        public string TargetFolderId { get; set; }

        /// <summary>
        /// Optional list of additional property names to extract and write to Excel
        /// </summary>
        public List<string> AdditionalProperties { get; set; }
    }

    /// <summary>
    /// Response for filling inventory.
    /// </summary>
    public class FillInventoryResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string RaumbuchFileId { get; set; }
        public int RoomsUpdated { get; set; }
        public int TotalItems { get; set; }
        public List<string> Warnings { get; set; }
    }
}

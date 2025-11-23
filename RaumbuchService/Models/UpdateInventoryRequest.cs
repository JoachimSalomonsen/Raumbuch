using System;
using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for updating inventory in room sheets from IFC files.
    /// Swiss German: Aktualisiert Inventar in Raumblättern aus IFC-Dateien.
    /// </summary>
    public class UpdateInventoryRequest
    {
        /// <summary>
        /// Trimble Connect access token
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Raumbuch file ID from Trimble Connect
        /// </summary>
        public string RaumbuchFileId { get; set; }

        /// <summary>
        /// Target folder ID for uploading the updated Raumbuch
        /// </summary>
        public string TargetFolderId { get; set; }

        /// <summary>
        /// List of IFC file IDs to read inventory from
        /// </summary>
        public List<string> IfcFileIds { get; set; }

        /// <summary>
        /// Partial name of the Pset to read (e.g., "Raumzuordnung")
        /// </summary>
        public string PsetPartialName { get; set; }

        /// <summary>
        /// Name of the property in the Pset that contains the room name/number
        /// </summary>
        public string RoomPropertyName { get; set; }

        /// <summary>
        /// Optional list of additional property names to extract and write to Excel
        /// </summary>
        public List<string> AdditionalProperties { get; set; }
    }

    /// <summary>
    /// Response for update inventory operation.
    /// </summary>
    public class UpdateInventoryResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string RaumbuchFileId { get; set; }
        public int RoomsUpdated { get; set; }
        public int ItemsDeleted { get; set; }
        public int ItemsAdded { get; set; }
        public List<string> Warnings { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for creating room sheets in Raumbuch Excel file.
    /// Creates one sheet per room with inventory table structure.
    /// </summary>
    public class CreateRoomSheetsRequest
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
        /// Target folder ID where updated Raumbuch will be uploaded
        /// </summary>
        public string TargetFolderId { get; set; }
    }

    /// <summary>
    /// Response for creating room sheets.
    /// </summary>
    public class CreateRoomSheetsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string RaumbuchFileId { get; set; }
        public int RoomSheetsCreated { get; set; }
        public List<string> RoomNames { get; set; }
    }
}

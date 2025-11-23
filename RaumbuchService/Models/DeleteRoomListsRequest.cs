using System;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for deleting room lists (sheets 3+) from Raumbuch Excel.
    /// Swiss German: Löscht Raumlisten aus Raumbuch Excel.
    /// </summary>
    public class DeleteRoomListsRequest
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
    }

    /// <summary>
    /// Response for delete room lists operation.
    /// </summary>
    public class DeleteRoomListsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string RaumbuchFileId { get; set; }
        public int SheetsDeleted { get; set; }
        public int HyperlinksRemoved { get; set; }
    }
}

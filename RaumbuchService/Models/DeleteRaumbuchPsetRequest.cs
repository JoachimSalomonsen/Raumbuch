namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for deleting Pset "Raumbuch" from IFC file.
    /// </summary>
    public class DeleteRaumbuchPsetRequest
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
        /// Target folder ID where updated IFC will be uploaded
        /// </summary>
        public string TargetFolderId { get; set; }
    }

    public class DeleteRaumbuchPsetResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string UpdatedIfcFileId { get; set; }
        public int PsetsRemoved { get; set; }
    }
}

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for resetting IFC by removing Pset "Überprüfung der Raumkategorie".
    /// </summary>
    public class ResetIfcRequest
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
        /// Target folder ID where updated IFC will be uploaded
        /// </summary>
        public string TargetFolderId { get; set; }
    }

    public class ResetIfcResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string UpdatedIfcFileId { get; set; }
        public int PsetsRemoved { get; set; }
    }
}

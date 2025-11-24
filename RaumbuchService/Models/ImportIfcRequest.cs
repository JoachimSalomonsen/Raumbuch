using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for importing IFC and creating Raumbuch with SOLL/IST analysis.
    /// </summary>
    public class ImportIfcRequest
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
        /// Raumprogramm file ID (Excel, SOLL data)
        /// </summary>
        public string RaumprogrammFileId { get; set; }

        /// <summary>
        /// Target folder ID where Raumbuch.xlsx will be created
        /// </summary>
        public string TargetFolderId { get; set; }
    }

    public class ImportIfcResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string RaumbuchFileId { get; set; }
        public string RaumbuchFileName { get; set; }
        public List<RoomCategoryAnalysis> Analysis { get; set; }
    }

    /// <summary>
    /// SOLL/IST analysis per room category.
    /// German Raumbuch standard status values: "OK", "Zu wenig", "Zu viel"
    /// </summary>
    public class RoomCategoryAnalysis
    {
        public string RoomCategory { get; set; }
        public double SollArea { get; set; }
        public double IstArea { get; set; }
        public double Percentage { get; set; }

        /// <summary>
        /// Returns true if IST is less than SOLL (too little area).
        /// This is the condition that requires attention (red highlighting).
        /// </summary>
        public bool IsUnderLimit => Percentage < 100.0 && SollArea > 0;

        /// <summary>
        /// Returns true if IST exceeds SOLL (too much area).
        /// </summary>
        public bool IsOverLimit => Percentage > 100.0;

        /// <summary>
        /// Returns the German status string according to Raumbuch standard.
        /// "OK" - IST equals SOLL (within tolerance)
        /// "Zu wenig" - IST is less than SOLL (needs attention)
        /// "Zu viel" - IST exceeds SOLL
        /// </summary>
        public string Status
        {
            get
            {
                if (SollArea <= 0 && IstArea <= 0) return "OK";
                if (IsUnderLimit) return "Zu wenig";
                if (IsOverLimit) return "Zu viel";
                return "OK";
            }
        }
    }
}

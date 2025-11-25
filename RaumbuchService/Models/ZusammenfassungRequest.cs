using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for getting Zusammenfassung data from Raumbuch Excel.
    /// </summary>
    public class GetZusammenfassungRequest
    {
        /// <summary>
        /// Trimble Connect access token
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Raumbuch file ID in Trimble Connect
        /// </summary>
        public string RaumbuchFileId { get; set; }
    }

    /// <summary>
    /// Response containing Zusammenfassung data.
    /// </summary>
    public class GetZusammenfassungResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<ZusammenfassungItem> Zusammenfassung { get; set; }
    }

    /// <summary>
    /// Request for updating Zusammenfassung with comments and status.
    /// </summary>
    public class UpdateZusammenfassungRequest
    {
        public string AccessToken { get; set; }
        public string RaumbuchFileId { get; set; }
        public string TargetFolderId { get; set; }
        public List<ZusammenfassungItem> Zusammenfassung { get; set; }
        public double ToleranceMin { get; set; }
        public double ToleranceMax { get; set; }
    }

    /// <summary>
    /// Response for updating Zusammenfassung.
    /// </summary>
    public class UpdateZusammenfassungResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string RaumbuchFileId { get; set; }
    }

    /// <summary>
    /// A single row in the Zusammenfassung table.
    /// </summary>
    public class ZusammenfassungItem
    {
        public string RoomCategory { get; set; }
        public double SollArea { get; set; }
        public double IstArea { get; set; }
        public double Percentage { get; set; }
        public string Status { get; set; }
        public string Comment { get; set; }

        /// <summary>
        /// Returns true if IST is less than SOLL (too little area).
        /// </summary>
        public bool IsUnderLimit => !double.IsNaN(Percentage) && Percentage >= 0 && Percentage < 100.0 && SollArea > 0;

        /// <summary>
        /// Returns true if IST exceeds SOLL (too much area).
        /// </summary>
        public bool IsOverLimit => !double.IsNaN(Percentage) && Percentage > 100.0;
    }

    /// <summary>
    /// Request for getting 3D Viewer data (rooms grouped by status).
    /// </summary>
    public class GetViewerDataRequest
    {
        /// <summary>
        /// Trimble Connect access token
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Raumbuch file ID in Trimble Connect
        /// </summary>
        public string RaumbuchFileId { get; set; }

        /// <summary>
        /// Tolerance minimum (percentage below 100% considered as under)
        /// </summary>
        public double ToleranceMin { get; set; } = -10;

        /// <summary>
        /// Tolerance maximum (percentage above 100% considered as over)
        /// </summary>
        public double ToleranceMax { get; set; } = 10;
    }

    /// <summary>
    /// Response containing rooms grouped by status for 3D Viewer.
    /// </summary>
    public class GetViewerDataResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        
        /// <summary>
        /// Rooms that are within tolerance (Erfüllt)
        /// </summary>
        public List<string> Erfuellt { get; set; }
        
        /// <summary>
        /// Rooms that are under tolerance (Unterschritten / IST less than SOLL)
        /// </summary>
        public List<string> Unterschritten { get; set; }
        
        /// <summary>
        /// Rooms that are over tolerance (Überschritten / IST more than SOLL)
        /// </summary>
        public List<string> Ueberschritten { get; set; }
    }
}

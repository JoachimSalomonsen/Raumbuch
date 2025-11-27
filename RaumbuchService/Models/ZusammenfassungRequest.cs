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
        /// <summary>
        /// Room type (Raumtyp) - from template column A
        /// </summary>
        public string Raumtyp { get; set; }
        
        /// <summary>
        /// Room category (Raumkategorie) - from template column F or IFC LongName
        /// </summary>
        public string Raumkategorie { get; set; }
        
        /// <summary>
        /// Legacy property - maps to Raumtyp for backward compatibility
        /// </summary>
        public string RoomCategory 
        { 
            get => Raumtyp; 
            set => Raumtyp = value; 
        }
        
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
        [Newtonsoft.Json.JsonProperty("accessToken")]
        public string AccessToken { get; set; }

        /// <summary>
        /// Raumbuch file ID in Trimble Connect
        /// </summary>
        [Newtonsoft.Json.JsonProperty("raumbuchFileId")]
        public string RaumbuchFileId { get; set; }

        /// <summary>
        /// Tolerance minimum (percentage below 100% considered as under)
        /// </summary>
        [Newtonsoft.Json.JsonProperty("toleranceMin")]
        public double ToleranceMin { get; set; } = -10;

        /// <summary>
        /// Tolerance maximum (percentage above 100% considered as over)
        /// </summary>
        [Newtonsoft.Json.JsonProperty("toleranceMax")]
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
    
    /// <summary>
    /// Request for getting template headers for column mapping.
    /// </summary>
    public class GetTemplateHeadersRequest
    {
        public string AccessToken { get; set; }
        public string TemplateFileId { get; set; }
    }
    
    /// <summary>
    /// Response containing template headers.
    /// </summary>
    public class GetTemplateHeadersResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> Headers { get; set; }
    }
    
    /// <summary>
    /// Request for creating Raumbuch directly from template with column mappings.
    /// </summary>
    public class CreateRaumbuchRequest
    {
        public string AccessToken { get; set; }
        public string ProjectId { get; set; }
        public string TemplateFileId { get; set; }
        public string IfcFileId { get; set; }
        public string TargetFolderId { get; set; }
        public ColumnMappings ColumnMappings { get; set; }
    }
    
    /// <summary>
    /// Column mappings for template to Raumbuch conversion.
    /// </summary>
    public class ColumnMappings
    {
        /// <summary>
        /// Column index for Raumtyp (0-based)
        /// </summary>
        public int RaumtypColumn { get; set; }
        
        /// <summary>
        /// Column index for Raumkategorie (0-based), -1 if not specified
        /// </summary>
        public int RaumkategorieColumn { get; set; } = -1;
        
        /// <summary>
        /// Column index for Fläche Soll (0-based)
        /// </summary>
        public int FlaecheSollColumn { get; set; }
    }
    
    /// <summary>
    /// Response for creating Raumbuch.
    /// </summary>
    public class CreateRaumbuchResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string RaumbuchFileId { get; set; }
        public string RaumbuchFileName { get; set; }
        public List<RoomCategoryAnalysis> Analysis { get; set; }
    }
    
    /// <summary>
    /// Request for getting IST values from IFC file.
    /// </summary>
    public class GetIstFromIfcRequest
    {
        public string AccessToken { get; set; }
        public string IfcFileId { get; set; }
    }
    
    /// <summary>
    /// Response containing IST values by category.
    /// </summary>
    public class GetIstFromIfcResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        /// <summary>
        /// Dictionary mapping room category to total IST area
        /// </summary>
        public Dictionary<string, double> IstByCategory { get; set; }
    }
    
    /// <summary>
    /// Request for updating Raumbuch with column mappings from template.
    /// </summary>
    public class UpdateRaumbuchWithMappingsRequest
    {
        public string AccessToken { get; set; }
        public string ProjectId { get; set; }
        public string RaumbuchFileId { get; set; }
        public string TemplateFileId { get; set; }
        public string IfcFileId { get; set; }
        public string TargetFolderId { get; set; }
        public ColumnMappings ColumnMappings { get; set; }
    }
}

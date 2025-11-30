using RaumbuchService.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

namespace RaumbuchService.Controllers
{
    /// <summary>
    /// Controller for the redesigned Analysis page functionality.
    /// Provides deviation analysis between SOLL and IST values for areas and inventory.
    /// </summary>
    [RoutePrefix("api/analysis")]
    public class AnalysisController : ApiController
    {
        // ====================================================================
        // ANALYSIS SETTINGS ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Gets analysis settings for a specific element type.
        /// GET /api/analysis/settings?elementType=NetArea
        /// GET /api/analysis/settings?elementType=Inventory&inventoryTemplateId=5
        /// </summary>
        [HttpGet]
        [Route("settings")]
        public async Task<IHttpActionResult> GetAnalysisSettings(
            string elementType,
            int? inventoryTemplateId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(elementType))
                {
                    return BadRequest("elementType ist erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    var query = db.AnalysisSettings
                        .Where(a => a.SelectedElementType == elementType);

                    if (elementType == "Inventory" && inventoryTemplateId.HasValue)
                    {
                        query = query.Where(a => a.SelectedInventoryTemplateID == inventoryTemplateId.Value);
                    }

                    var settings = await query.FirstOrDefaultAsync();

                    if (settings == null)
                    {
                        // Return default settings
                        return Ok(new AnalysisSettingsResponse
                        {
                            Success = true,
                            Message = "Standardeinstellungen verwendet.",
                            Settings = new AnalysisSettingsDto
                            {
                                SelectedElementType = elementType,
                                SelectedInventoryTemplateID = inventoryTemplateId,
                                ToleranceMin = -10.00m,
                                ToleranceMax = 10.00m
                            }
                        });
                    }

                    return Ok(new AnalysisSettingsResponse
                    {
                        Success = true,
                        Message = "Einstellungen geladen.",
                        Settings = new AnalysisSettingsDto
                        {
                            AnalysisSettingsID = settings.AnalysisSettingsID,
                            SelectedElementType = settings.SelectedElementType,
                            SelectedInventoryTemplateID = settings.SelectedInventoryTemplateID,
                            ToleranceMin = settings.ToleranceMin,
                            ToleranceMax = settings.ToleranceMax,
                            ModifiedByUserID = settings.ModifiedByUserID,
                            ModifiedDate = settings.ModifiedDate
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAnalysisSettings: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Laden der Einstellungen: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Saves or updates analysis settings for a specific element type.
        /// POST /api/analysis/settings
        /// </summary>
        [HttpPost]
        [Route("settings")]
        public async Task<IHttpActionResult> SaveAnalysisSettings([FromBody] SaveAnalysisSettingsRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.SelectedElementType))
                {
                    return BadRequest("SelectedElementType ist erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    // Find existing settings
                    var query = db.AnalysisSettings
                        .Where(a => a.SelectedElementType == request.SelectedElementType);

                    if (request.SelectedElementType == "Inventory" && request.SelectedInventoryTemplateID.HasValue)
                    {
                        query = query.Where(a => a.SelectedInventoryTemplateID == request.SelectedInventoryTemplateID.Value);
                    }
                    else
                    {
                        query = query.Where(a => a.SelectedInventoryTemplateID == null);
                    }

                    var settings = await query.FirstOrDefaultAsync();

                    if (settings == null)
                    {
                        // Create new settings
                        settings = new AnalysisSettings
                        {
                            SelectedElementType = request.SelectedElementType,
                            SelectedInventoryTemplateID = request.SelectedInventoryTemplateID,
                            ToleranceMin = request.ToleranceMin,
                            ToleranceMax = request.ToleranceMax
                        };
                        db.AnalysisSettings.Add(settings);
                    }
                    else
                    {
                        // Update existing
                        settings.ToleranceMin = request.ToleranceMin;
                        settings.ToleranceMax = request.ToleranceMax;
                    }

                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new AnalysisSettingsResponse
                    {
                        Success = true,
                        Message = "Einstellungen gespeichert.",
                        Settings = new AnalysisSettingsDto
                        {
                            AnalysisSettingsID = settings.AnalysisSettingsID,
                            SelectedElementType = settings.SelectedElementType,
                            SelectedInventoryTemplateID = settings.SelectedInventoryTemplateID,
                            ToleranceMin = settings.ToleranceMin,
                            ToleranceMax = settings.ToleranceMax,
                            ModifiedByUserID = settings.ModifiedByUserID,
                            ModifiedDate = settings.ModifiedDate
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveAnalysisSettings: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Speichern der Einstellungen: {ex.Message}", ex));
            }
        }

        // ====================================================================
        // ANALYSIS ELEMENTS DROPDOWN
        // ====================================================================

        /// <summary>
        /// Gets available elements for analysis dropdown.
        /// Returns NetArea, GrossArea, and all inventory templates.
        /// GET /api/analysis/elements
        /// </summary>
        [HttpGet]
        [Route("elements")]
        public async Task<IHttpActionResult> GetAnalysisElements()
        {
            try
            {
                using (var db = new RaumbuchContext())
                {
                    var elements = new List<AnalysisElementDto>
                    {
                        new AnalysisElementDto { Type = "NetArea", Name = "Nettofläche", Category = "Area" },
                        new AnalysisElementDto { Type = "GrossArea", Name = "Bruttofläche", Category = "Area" }
                    };

                    // Add inventory templates
                    var templates = await db.InventoryTemplates
                        .OrderBy(t => t.PropertyName)
                        .Select(t => new AnalysisElementDto
                        {
                            Type = "Inventory",
                            Name = t.PropertyName,
                            Category = "Inventory",
                            InventoryTemplateID = t.InventoryTemplateID,
                            DataType = t.DataType,
                            Unit = t.Unit
                        })
                        .ToListAsync();

                    elements.AddRange(templates);

                    return Ok(new AnalysisElementsResponse
                    {
                        Success = true,
                        Message = $"{elements.Count} Elemente gefunden.",
                        Elements = elements
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAnalysisElements: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Laden der Elemente: {ex.Message}", ex));
            }
        }

        // ====================================================================
        // ANALYSIS DATA ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Gets analysis data for a specific element type.
        /// Computes and returns deviation data for all rooms.
        /// GET /api/analysis/data?elementType=NetArea&roomCategory=Büro
        /// </summary>
        [HttpGet]
        [Route("data")]
        public async Task<IHttpActionResult> GetAnalysisData(
            string elementType,
            int? inventoryTemplateId = null,
            string roomCategory = null,
            string status = null,
            string search = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(elementType))
                {
                    return BadRequest("elementType ist erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    // Get tolerance settings
                    var settingsQuery = db.AnalysisSettings
                        .Where(a => a.SelectedElementType == elementType);

                    if (elementType == "Inventory" && inventoryTemplateId.HasValue)
                    {
                        settingsQuery = settingsQuery.Where(a => a.SelectedInventoryTemplateID == inventoryTemplateId.Value);
                    }

                    var settings = await settingsQuery.FirstOrDefaultAsync();
                    decimal toleranceMin = settings?.ToleranceMin ?? -10.00m;
                    decimal toleranceMax = settings?.ToleranceMax ?? 10.00m;

                    List<AnalysisRowDto> rows;

                    if (elementType == "NetArea" || elementType == "GrossArea")
                    {
                        rows = await GetAreaAnalysisData(db, elementType, roomCategory, status, search, toleranceMin, toleranceMax);
                    }
                    else if (elementType == "Inventory" && inventoryTemplateId.HasValue)
                    {
                        rows = await GetInventoryAnalysisData(db, inventoryTemplateId.Value, roomCategory, status, search, toleranceMin, toleranceMax);
                    }
                    else
                    {
                        return BadRequest("Ungültiger elementType oder fehlende inventoryTemplateId.");
                    }

                    // Get room categories for filter dropdown
                    var roomCategories = await db.RoomTypes
                        .Where(rt => rt.RoomCategory != null && rt.RoomCategory != "")
                        .Select(rt => rt.RoomCategory)
                        .Distinct()
                        .OrderBy(c => c)
                        .ToListAsync();

                    // Calculate KPIs
                    var kpis = CalculateKpis(rows);

                    return Ok(new AnalysisDataResponse
                    {
                        Success = true,
                        Message = $"{rows.Count} Einträge gefunden.",
                        ElementType = elementType,
                        InventoryTemplateID = inventoryTemplateId,
                        ToleranceMin = toleranceMin,
                        ToleranceMax = toleranceMax,
                        Rows = rows,
                        RoomCategories = roomCategories,
                        TotalSoll = kpis.TotalSoll,
                        TotalIst = kpis.TotalIst,
                        TotalDeviation = kpis.TotalDeviation,
                        CountErfuellt = kpis.CountErfuellt,
                        CountUnterschritten = kpis.CountUnterschritten,
                        CountUeberschritten = kpis.CountUeberschritten
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAnalysisData: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Laden der Analysedaten: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Gets analysis data for area (NetArea or GrossArea).
        /// </summary>
        private async Task<List<AnalysisRowDto>> GetAreaAnalysisData(
            RaumbuchContext db,
            string elementType,
            string roomCategory,
            string status,
            string search,
            decimal toleranceMin,
            decimal toleranceMax)
        {
            var query = db.Rooms
                .Include(r => r.RoomType)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(roomCategory))
            {
                query = query.Where(r => r.RoomType.RoomCategory == roomCategory);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r =>
                    r.Name.Contains(search) ||
                    r.RoomType.Name.Contains(search) ||
                    r.RoomType.RoomCategory.Contains(search));
            }

            var rooms = await query.ToListAsync();

            var rows = rooms.Select(r =>
            {
                decimal? soll = elementType == "NetArea" ? r.NetAreaPlanned : r.GrossAreaPlanned;
                decimal? ist = elementType == "NetArea" ? r.NetAreaActual : r.GrossAreaActual;
                string commentIst = elementType == "NetArea" ? r.NetAreaCommentIst : r.GrossAreaCommentIst;
                DateTime? lastUpdated = elementType == "NetArea" ? r.NetAreaLastUpdated : r.GrossAreaLastUpdated;

                var deviation = CalculateDeviation(soll, ist, toleranceMin, toleranceMax);

                return new AnalysisRowDto
                {
                    RoomID = r.RoomID,
                    RoomTypeName = r.RoomType?.Name,
                    RoomCategory = r.RoomType?.RoomCategory,
                    RoomName = r.Name,
                    SollValue = soll ?? 0,
                    IstValue = ist ?? 0,
                    DeviationPercent = deviation.Percent,
                    DeviationValue = deviation.Value,
                    Status = deviation.Status,
                    StatusText = deviation.StatusText,
                    CommentIst = commentIst,
                    LastUpdated = lastUpdated
                };
            }).ToList();

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                int statusCode = status == "ok" ? 0 : (status == "under" ? -1 : 1);
                rows = rows.Where(r => r.Status == statusCode).ToList();
            }

            return rows.OrderBy(r => r.RoomTypeName).ThenBy(r => r.RoomName).ToList();
        }

        /// <summary>
        /// Gets analysis data for inventory.
        /// </summary>
        private async Task<List<AnalysisRowDto>> GetInventoryAnalysisData(
            RaumbuchContext db,
            int inventoryTemplateId,
            string roomCategory,
            string status,
            string search,
            decimal toleranceMin,
            decimal toleranceMax)
        {
            var template = await db.InventoryTemplates.FindAsync(inventoryTemplateId);
            if (template == null)
            {
                return new List<AnalysisRowDto>();
            }

            var query = db.RoomInventories
                .Include(ri => ri.Room)
                .Include(ri => ri.Room.RoomType)
                .Where(ri => ri.InventoryTemplateID == inventoryTemplateId);

            var inventories = await query.ToListAsync();

            // Apply room category filter
            if (!string.IsNullOrWhiteSpace(roomCategory))
            {
                inventories = inventories.Where(ri => ri.Room?.RoomType?.RoomCategory == roomCategory).ToList();
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                inventories = inventories.Where(ri =>
                    (ri.Room?.Name?.Contains(search) ?? false) ||
                    (ri.Room?.RoomType?.Name?.Contains(search) ?? false) ||
                    (ri.Room?.RoomType?.RoomCategory?.Contains(search) ?? false)).ToList();
            }

            var rows = inventories.Select(ri =>
            {
                decimal sollNorm = NormalizeInventoryValue(ri.ValuePlanned, template.DataType);
                decimal istNorm = NormalizeInventoryValue(ri.ValueActual, template.DataType);

                var deviation = CalculateDeviation(sollNorm, istNorm, toleranceMin, toleranceMax);

                return new AnalysisRowDto
                {
                    RoomID = ri.RoomID,
                    RoomInventoryID = ri.RoomInventoryID,
                    InventoryTemplateID = ri.InventoryTemplateID,
                    RoomTypeName = ri.Room?.RoomType?.Name,
                    RoomCategory = ri.Room?.RoomType?.RoomCategory,
                    RoomName = ri.Room?.Name,
                    SollValue = sollNorm,
                    IstValue = istNorm,
                    SollValueRaw = ri.ValuePlanned,
                    IstValueRaw = ri.ValueActual,
                    DeviationPercent = deviation.Percent,
                    DeviationValue = deviation.Value,
                    Status = deviation.Status,
                    StatusText = deviation.StatusText,
                    CommentIst = ri.CommentIst,
                    LastUpdated = ri.InventoryLastUpdated
                };
            }).ToList();

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                int statusCode = status == "ok" ? 0 : (status == "under" ? -1 : 1);
                rows = rows.Where(r => r.Status == statusCode).ToList();
            }

            return rows.OrderBy(r => r.RoomTypeName).ThenBy(r => r.RoomName).ToList();
        }

        // ====================================================================
        // COMPUTE AND SAVE DEVIATIONS
        // ====================================================================

        /// <summary>
        /// Computes and saves deviation values for a specific element type.
        /// POST /api/analysis/compute
        /// </summary>
        [HttpPost]
        [Route("compute")]
        public async Task<IHttpActionResult> ComputeAndSaveDeviations([FromBody] ComputeDeviationsRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.ElementType))
                {
                    return BadRequest("ElementType ist erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    int updatedCount = 0;
                    var now = DateTime.UtcNow;

                    if (request.ElementType == "NetArea" || request.ElementType == "GrossArea")
                    {
                        var rooms = await db.Rooms.ToListAsync();

                        foreach (var room in rooms)
                        {
                            if (request.ElementType == "NetArea")
                            {
                                var deviation = CalculateDeviation(room.NetAreaPlanned, room.NetAreaActual, request.ToleranceMin, request.ToleranceMax);
                                room.NetAreaDeviationPercent = deviation.Percent;
                                room.NetAreaDeviationValue = deviation.Value;
                                room.NetAreaStatus = deviation.Status;
                                room.NetAreaLastUpdated = now;
                            }
                            else
                            {
                                var deviation = CalculateDeviation(room.GrossAreaPlanned, room.GrossAreaActual, request.ToleranceMin, request.ToleranceMax);
                                room.GrossAreaDeviationPercent = deviation.Percent;
                                room.GrossAreaDeviationValue = deviation.Value;
                                room.GrossAreaStatus = deviation.Status;
                                room.GrossAreaLastUpdated = now;
                            }
                            updatedCount++;
                        }
                    }
                    else if (request.ElementType == "Inventory" && request.InventoryTemplateID.HasValue)
                    {
                        var template = await db.InventoryTemplates.FindAsync(request.InventoryTemplateID.Value);
                        if (template == null)
                        {
                            return BadRequest("InventoryTemplate nicht gefunden.");
                        }

                        var inventories = await db.RoomInventories
                            .Where(ri => ri.InventoryTemplateID == request.InventoryTemplateID.Value)
                            .ToListAsync();

                        foreach (var ri in inventories)
                        {
                            decimal sollNorm = NormalizeInventoryValue(ri.ValuePlanned, template.DataType);
                            decimal istNorm = NormalizeInventoryValue(ri.ValueActual, template.DataType);

                            var deviation = CalculateDeviation(sollNorm, istNorm, request.ToleranceMin, request.ToleranceMax);
                            ri.InventoryDeviationPercent = deviation.Percent;
                            ri.InventoryDeviationValue = deviation.Value;
                            ri.InventoryStatus = deviation.Status;
                            ri.InventoryLastUpdated = now;
                            updatedCount++;
                        }
                    }
                    else
                    {
                        return BadRequest("Ungültiger ElementType oder fehlende InventoryTemplateID.");
                    }

                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new ComputeDeviationsResponse
                    {
                        Success = true,
                        Message = $"{updatedCount} Einträge aktualisiert.",
                        UpdatedCount = updatedCount
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ComputeAndSaveDeviations: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Berechnen der Abweichungen: {ex.Message}", ex));
            }
        }

        // ====================================================================
        // UPDATE IST COMMENT (AUTOSAVE)
        // ====================================================================

        /// <summary>
        /// Updates the IST comment for a room or inventory item.
        /// PUT /api/analysis/comment
        /// </summary>
        [HttpPut]
        [Route("comment")]
        public async Task<IHttpActionResult> UpdateIstComment([FromBody] UpdateIstCommentRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.ElementType))
                {
                    return BadRequest("ElementType ist erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    if (request.ElementType == "NetArea" || request.ElementType == "GrossArea")
                    {
                        var room = await db.Rooms.FindAsync(request.RoomID);
                        if (room == null)
                        {
                            return NotFound();
                        }

                        if (request.ElementType == "NetArea")
                        {
                            room.NetAreaCommentIst = request.CommentIst;
                        }
                        else
                        {
                            room.GrossAreaCommentIst = request.CommentIst;
                        }
                    }
                    else if (request.ElementType == "Inventory")
                    {
                        var inventory = await db.RoomInventories.FindAsync(request.RoomInventoryID);
                        if (inventory == null)
                        {
                            return NotFound();
                        }

                        inventory.CommentIst = request.CommentIst;
                    }
                    else
                    {
                        return BadRequest("Ungültiger ElementType.");
                    }

                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new BaseResponse
                    {
                        Success = true,
                        Message = "Kommentar gespeichert."
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateIstComment: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Speichern des Kommentars: {ex.Message}", ex));
            }
        }

        // ====================================================================
        // HELPER METHODS
        // ====================================================================

        /// <summary>
        /// Normalizes inventory value based on data type.
        /// </summary>
        private decimal NormalizeInventoryValue(string value, string dataType)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;

            var dt = (dataType ?? "Text").ToLower();

            switch (dt)
            {
                case "number":
                case "integer":
                case "decimal":
                    if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal numValue))
                    {
                        return numValue;
                    }
                    return 0;

                case "boolean":
                    var upper = value.ToUpper().Trim();
                    if (upper == "TRUE" || upper == "YES" || upper == "JA" || upper == "1")
                        return 1;
                    return 0;

                case "text":
                default:
                    // Text presence: 1 if non-empty, 0 otherwise
                    return string.IsNullOrWhiteSpace(value) ? 0 : 1;
            }
        }

        /// <summary>
        /// Calculates deviation values and status.
        /// </summary>
        private DeviationResult CalculateDeviation(decimal? soll, decimal? ist, decimal toleranceMin, decimal toleranceMax)
        {
            decimal sollVal = soll ?? 0;
            decimal istVal = ist ?? 0;

            decimal deviationValue = istVal - sollVal;
            decimal? deviationPercent = null;

            if (sollVal != 0)
            {
                deviationPercent = ((istVal - sollVal) / sollVal) * 100;
            }

            // Determine status based on tolerance (using deviation from 100%)
            // Status: 0=Erfüllt, -1=Unterschritten, 1=Überschritten
            int status;
            string statusText;

            if (istVal == sollVal)
            {
                status = 0;
                statusText = "Erfüllt";
            }
            else if (istVal < sollVal)
            {
                // Check if within tolerance
                if (deviationPercent.HasValue && deviationPercent.Value >= toleranceMin)
                {
                    status = 0;
                    statusText = "Erfüllt";
                }
                else
                {
                    status = -1;
                    statusText = "Unterschritten";
                }
            }
            else
            {
                // IST > SOLL
                if (deviationPercent.HasValue && deviationPercent.Value <= toleranceMax)
                {
                    status = 0;
                    statusText = "Erfüllt";
                }
                else
                {
                    status = 1;
                    statusText = "Überschritten";
                }
            }

            return new DeviationResult
            {
                Value = deviationValue,
                Percent = deviationPercent,
                Status = status,
                StatusText = statusText
            };
        }

        /// <summary>
        /// Calculates KPIs for analysis data.
        /// </summary>
        private AnalysisKpis CalculateKpis(List<AnalysisRowDto> rows)
        {
            return new AnalysisKpis
            {
                TotalSoll = rows.Sum(r => r.SollValue),
                TotalIst = rows.Sum(r => r.IstValue),
                TotalDeviation = rows.Sum(r => r.DeviationValue),
                CountErfuellt = rows.Count(r => r.Status == 0),
                CountUnterschritten = rows.Count(r => r.Status == -1),
                CountUeberschritten = rows.Count(r => r.Status == 1)
            };
        }
    }

    // ====================================================================
    // DTOs and Request/Response Models for Analysis
    // ====================================================================

    public class AnalysisSettingsDto
    {
        public int AnalysisSettingsID { get; set; }
        public string SelectedElementType { get; set; }
        public int? SelectedInventoryTemplateID { get; set; }
        public decimal ToleranceMin { get; set; }
        public decimal ToleranceMax { get; set; }
        public string ModifiedByUserID { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class AnalysisSettingsResponse : BaseResponse
    {
        public AnalysisSettingsDto Settings { get; set; }
    }

    public class SaveAnalysisSettingsRequest
    {
        public string SelectedElementType { get; set; }
        public int? SelectedInventoryTemplateID { get; set; }
        public decimal ToleranceMin { get; set; }
        public decimal ToleranceMax { get; set; }
        public string UserId { get; set; }
    }

    public class AnalysisElementDto
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public int? InventoryTemplateID { get; set; }
        public string DataType { get; set; }
        public string Unit { get; set; }
    }

    public class AnalysisElementsResponse : BaseResponse
    {
        public List<AnalysisElementDto> Elements { get; set; }
    }

    public class AnalysisRowDto
    {
        public int RoomID { get; set; }
        public int? RoomInventoryID { get; set; }
        public int? InventoryTemplateID { get; set; }
        public string RoomTypeName { get; set; }
        public string RoomCategory { get; set; }
        public string RoomName { get; set; }
        public decimal SollValue { get; set; }
        public decimal IstValue { get; set; }
        public string SollValueRaw { get; set; }
        public string IstValueRaw { get; set; }
        public decimal? DeviationPercent { get; set; }
        public decimal DeviationValue { get; set; }
        public int Status { get; set; }
        public string StatusText { get; set; }
        public string CommentIst { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    public class AnalysisDataResponse : BaseResponse
    {
        public string ElementType { get; set; }
        public int? InventoryTemplateID { get; set; }
        public decimal ToleranceMin { get; set; }
        public decimal ToleranceMax { get; set; }
        public List<AnalysisRowDto> Rows { get; set; }
        public List<string> RoomCategories { get; set; }
        public decimal TotalSoll { get; set; }
        public decimal TotalIst { get; set; }
        public decimal TotalDeviation { get; set; }
        public int CountErfuellt { get; set; }
        public int CountUnterschritten { get; set; }
        public int CountUeberschritten { get; set; }
    }

    public class ComputeDeviationsRequest
    {
        public string ElementType { get; set; }
        public int? InventoryTemplateID { get; set; }
        public decimal ToleranceMin { get; set; }
        public decimal ToleranceMax { get; set; }
        public string UserId { get; set; }
    }

    public class ComputeDeviationsResponse : BaseResponse
    {
        public int UpdatedCount { get; set; }
    }

    public class UpdateIstCommentRequest
    {
        public string ElementType { get; set; }
        public int? RoomID { get; set; }
        public int? RoomInventoryID { get; set; }
        public string CommentIst { get; set; }
        public string UserId { get; set; }
    }

    internal class DeviationResult
    {
        public decimal Value { get; set; }
        public decimal? Percent { get; set; }
        public int Status { get; set; }
        public string StatusText { get; set; }
    }

    internal class AnalysisKpis
    {
        public decimal TotalSoll { get; set; }
        public decimal TotalIst { get; set; }
        public decimal TotalDeviation { get; set; }
        public int CountErfuellt { get; set; }
        public int CountUnterschritten { get; set; }
        public int CountUeberschritten { get; set; }
    }
}

using RaumbuchService.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;

namespace RaumbuchService.Controllers
{
    /// <summary>
    /// Controller for multi-building management.
    /// Provides CRUD operations for buildings and building-related data.
    /// </summary>
    [RoutePrefix("api/building")]
    public class BuildingController : ApiController
    {
        // ====================================================================
        // BUILDING CRUD ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Gets all buildings.
        /// GET /api/building/list
        /// </summary>
        [HttpGet]
        [Route("list")]
        public async Task<IHttpActionResult> GetBuildings()
        {
            try
            {
                using (var db = new RaumbuchContext())
                {
                    var buildings = await db.Buildings
                        .OrderBy(b => b.BuildingName)
                        .Select(b => new BuildingDto
                        {
                            BuildingID = b.BuildingID,
                            BuildingName = b.BuildingName,
                            BuildingCode = b.BuildingCode,
                            Description = b.Description,
                            AddressStreet = b.AddressStreet,
                            AddressCity = b.AddressCity,
                            AddressPostalCode = b.AddressPostalCode,
                            AddressCountry = b.AddressCountry,
                            Owner = b.Owner,
                            Creator = b.Creator,
                            IFCProjectGUID = b.IFCProjectGUID,
                            IFCBuildingGUID = b.IFCBuildingGUID,
                            IFCEnabled = b.IFCEnabled,
                            IFCFileUrl = b.IFCFileUrl,
                            CoordinateSystem = b.CoordinateSystem,
                            LocalOriginX = b.LocalOriginX,
                            LocalOriginY = b.LocalOriginY,
                            LocalOriginZ = b.LocalOriginZ,
                            LogoUrl = b.LogoUrl,
                            ModifiedByUserID = b.ModifiedByUserID,
                            ModifiedDate = b.ModifiedDate
                        })
                        .ToListAsync();

                    return Ok(new BuildingListResponse
                    {
                        Success = true,
                        Message = $"{buildings.Count} Gebäude gefunden.",
                        Buildings = buildings
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetBuildings: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Laden der Gebäude: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Gets a building by ID.
        /// GET /api/building/{id}
        /// </summary>
        [HttpGet]
        [Route("{id:int}")]
        public async Task<IHttpActionResult> GetBuilding(int id)
        {
            try
            {
                using (var db = new RaumbuchContext())
                {
                    var building = await db.Buildings.FindAsync(id);

                    if (building == null)
                    {
                        return NotFound();
                    }

                    return Ok(new BuildingResponse
                    {
                        Success = true,
                        Message = "Gebäude geladen.",
                        Building = MapToDto(building)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetBuilding: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Laden des Gebäudes: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Creates a new building.
        /// POST /api/building
        /// </summary>
        [HttpPost]
        [Route("")]
        public async Task<IHttpActionResult> CreateBuilding([FromBody] CreateBuildingRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.BuildingName))
                {
                    return BadRequest("BuildingName ist erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    var building = new Building
                    {
                        BuildingName = request.BuildingName,
                        BuildingCode = request.BuildingCode,
                        Description = request.Description,
                        AddressStreet = request.AddressStreet,
                        AddressCity = request.AddressCity,
                        AddressPostalCode = request.AddressPostalCode,
                        AddressCountry = request.AddressCountry,
                        Owner = request.Owner,
                        Creator = request.Creator,
                        IFCProjectGUID = request.IFCProjectGUID,
                        IFCBuildingGUID = request.IFCBuildingGUID,
                        IFCEnabled = request.IFCEnabled,
                        IFCFileUrl = request.IFCFileUrl,
                        CoordinateSystem = request.CoordinateSystem ?? "LV95",
                        LocalOriginX = request.LocalOriginX ?? 0,
                        LocalOriginY = request.LocalOriginY ?? 0,
                        LocalOriginZ = request.LocalOriginZ ?? 0,
                        LogoUrl = request.LogoUrl
                    };

                    db.Buildings.Add(building);
                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new BuildingResponse
                    {
                        Success = true,
                        Message = "Gebäude erstellt.",
                        Building = MapToDto(building),
                        StandardizedName = StandardizeName(building.BuildingName)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CreateBuilding: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Erstellen des Gebäudes: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Updates an existing building.
        /// PUT /api/building/{id}
        /// </summary>
        [HttpPut]
        [Route("{id:int}")]
        public async Task<IHttpActionResult> UpdateBuilding(int id, [FromBody] UpdateBuildingRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Request body ist erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    var building = await db.Buildings.FindAsync(id);

                    if (building == null)
                    {
                        return NotFound();
                    }

                    // Update fields
                    if (!string.IsNullOrWhiteSpace(request.BuildingName))
                        building.BuildingName = request.BuildingName;
                    if (request.BuildingCode != null)
                        building.BuildingCode = request.BuildingCode;
                    if (request.Description != null)
                        building.Description = request.Description;
                    if (request.AddressStreet != null)
                        building.AddressStreet = request.AddressStreet;
                    if (request.AddressCity != null)
                        building.AddressCity = request.AddressCity;
                    if (request.AddressPostalCode != null)
                        building.AddressPostalCode = request.AddressPostalCode;
                    if (request.AddressCountry != null)
                        building.AddressCountry = request.AddressCountry;
                    if (request.Owner != null)
                        building.Owner = request.Owner;
                    if (request.Creator != null)
                        building.Creator = request.Creator;
                    if (request.IFCProjectGUID != null)
                        building.IFCProjectGUID = request.IFCProjectGUID;
                    if (request.IFCBuildingGUID != null)
                        building.IFCBuildingGUID = request.IFCBuildingGUID;
                    if (request.IFCEnabled.HasValue)
                        building.IFCEnabled = request.IFCEnabled.Value;
                    if (request.IFCFileUrl != null)
                        building.IFCFileUrl = request.IFCFileUrl;
                    if (request.CoordinateSystem != null)
                        building.CoordinateSystem = request.CoordinateSystem;
                    if (request.LocalOriginX.HasValue)
                        building.LocalOriginX = request.LocalOriginX.Value;
                    if (request.LocalOriginY.HasValue)
                        building.LocalOriginY = request.LocalOriginY.Value;
                    if (request.LocalOriginZ.HasValue)
                        building.LocalOriginZ = request.LocalOriginZ.Value;
                    if (request.LogoUrl != null)
                        building.LogoUrl = request.LogoUrl;

                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new BuildingResponse
                    {
                        Success = true,
                        Message = "Gebäude aktualisiert.",
                        Building = MapToDto(building),
                        StandardizedName = StandardizeName(building.BuildingName)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateBuilding: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Aktualisieren des Gebäudes: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Deletes a building and all related data.
        /// DELETE /api/building/{id}
        /// </summary>
        [HttpDelete]
        [Route("{id:int}")]
        public async Task<IHttpActionResult> DeleteBuilding(int id, [FromUri] string userId = null)
        {
            try
            {
                using (var db = new RaumbuchContext())
                {
                    var building = await db.Buildings.FindAsync(id);

                    if (building == null)
                    {
                        return NotFound();
                    }

                    // Delete related RoomInventory records
                    var roomInventories = await db.RoomInventories
                        .Where(ri => ri.Room.BuildingID == id)
                        .ToListAsync();
                    db.RoomInventories.RemoveRange(roomInventories);

                    // Delete related Room records
                    var rooms = await db.Rooms
                        .Where(r => r.BuildingID == id)
                        .ToListAsync();
                    db.Rooms.RemoveRange(rooms);

                    // Delete related RoomType records
                    var roomTypes = await db.RoomTypes
                        .Where(rt => rt.BuildingID == id)
                        .ToListAsync();
                    db.RoomTypes.RemoveRange(roomTypes);

                    // Delete the building
                    db.Buildings.Remove(building);

                    await db.SaveChangesAsync();

                    return Ok(new BaseResponse
                    {
                        Success = true,
                        Message = $"Gebäude '{building.BuildingName}' und alle zugehörigen Daten wurden gelöscht."
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeleteBuilding: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Löschen des Gebäudes: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Standardizes a building name for Azure folder and JSON filename.
        /// GET /api/building/standardize-name?name=Hauptgebäude
        /// </summary>
        [HttpGet]
        [Route("standardize-name")]
        public IHttpActionResult StandardizeBuildingName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("name ist erforderlich.");
            }

            return Ok(new
            {
                Success = true,
                OriginalName = name,
                StandardizedName = StandardizeName(name)
            });
        }

        /// <summary>
        /// Gets building statistics (room count, etc.).
        /// GET /api/building/{id}/stats
        /// </summary>
        [HttpGet]
        [Route("{id:int}/stats")]
        public async Task<IHttpActionResult> GetBuildingStats(int id)
        {
            try
            {
                using (var db = new RaumbuchContext())
                {
                    var building = await db.Buildings.FindAsync(id);

                    if (building == null)
                    {
                        return NotFound();
                    }

                    var roomCount = await db.Rooms.CountAsync(r => r.BuildingID == id);
                    var roomTypeCount = await db.RoomTypes.CountAsync(rt => rt.BuildingID == id);

                    return Ok(new
                    {
                        Success = true,
                        BuildingID = id,
                        BuildingName = building.BuildingName,
                        RoomCount = roomCount,
                        RoomTypeCount = roomTypeCount
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetBuildingStats: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Laden der Statistiken: {ex.Message}", ex));
            }
        }

        // ====================================================================
        // CONFIGURATION ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Saves configuration for a building (updates IFC settings and other config data).
        /// POST /api/building/{id}/save-configuration
        /// </summary>
        [HttpPost]
        [Route("{id:int}/save-configuration")]
        public async Task<IHttpActionResult> SaveConfiguration(int id, [FromBody] SaveConfigurationRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Request body ist erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    var building = await db.Buildings.FindAsync(id);

                    if (building == null)
                    {
                        return NotFound();
                    }

                    // Update IFC settings
                    if (request.IfcEnabled.HasValue)
                        building.IFCEnabled = request.IfcEnabled.Value;
                    if (request.IfcProjectGUID != null)
                        building.IFCProjectGUID = request.IfcProjectGUID;
                    if (request.IfcBuildingGUID != null)
                        building.IFCBuildingGUID = request.IfcBuildingGUID;
                    if (request.IfcFileUrl != null)
                        building.IFCFileUrl = request.IfcFileUrl;

                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new BaseResponse
                    {
                        Success = true,
                        Message = "Konfiguration gespeichert."
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveConfiguration: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Speichern der Konfiguration: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Reads IFC properties from a file (placeholder - actual IFC reading requires GeometricGym or similar).
        /// GET /api/building/read-ifc-properties?buildingId={id}&ifcFileName={name}
        /// </summary>
        [HttpGet]
        [Route("read-ifc-properties")]
        public IHttpActionResult ReadIfcProperties(int buildingId, string ifcFileName)
        {
            try
            {
                // For now, return placeholder data
                // In a real implementation, this would use GeometricGym or xBIM to read IFC properties
                // The actual IFC file reading requires the file to be accessible (downloaded from Azure or accessed via URL)
                
                return Ok(new
                {
                    Success = true,
                    Message = "IFC-Eigenschaften konnten gelesen werden.",
                    ProjectGUID = (string)null, // Placeholder - would be read from IFC file
                    BuildingGUID = (string)null, // Placeholder - would be read from IFC file
                    MultipleBuildingsError = false
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ReadIfcProperties: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Lesen der IFC-Eigenschaften: {ex.Message}", ex));
            }
        }

        // ====================================================================
        // HELPER METHODS
        // ====================================================================

        /// <summary>
        /// Standardizes a name by removing spaces, replacing special characters.
        /// Rules:
        /// - Remove spaces and symbols
        /// - Replace æ/ø/å → ae/oe/aa
        /// - Keep only A-Z, a-z, 0-9 and _
        /// </summary>
        public static string StandardizeName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "";

            var result = rawName;

            // Replace special Scandinavian/German characters
            result = result.Replace("æ", "ae").Replace("Æ", "Ae");
            result = result.Replace("ø", "oe").Replace("Ø", "Oe");
            result = result.Replace("å", "aa").Replace("Å", "Aa");
            result = result.Replace("ä", "ae").Replace("Ä", "Ae");
            result = result.Replace("ö", "oe").Replace("Ö", "Oe");
            result = result.Replace("ü", "ue").Replace("Ü", "Ue");
            result = result.Replace("ß", "ss");

            // Replace spaces with underscores
            result = result.Replace(" ", "_");

            // Remove all characters except A-Z, a-z, 0-9, and _
            result = Regex.Replace(result, @"[^A-Za-z0-9_]", "");

            // Remove multiple consecutive underscores
            result = Regex.Replace(result, @"_+", "_");

            // Trim leading/trailing underscores
            result = result.Trim('_');

            return result;
        }

        private static BuildingDto MapToDto(Building building)
        {
            return new BuildingDto
            {
                BuildingID = building.BuildingID,
                BuildingName = building.BuildingName,
                BuildingCode = building.BuildingCode,
                Description = building.Description,
                AddressStreet = building.AddressStreet,
                AddressCity = building.AddressCity,
                AddressPostalCode = building.AddressPostalCode,
                AddressCountry = building.AddressCountry,
                Owner = building.Owner,
                Creator = building.Creator,
                IFCProjectGUID = building.IFCProjectGUID,
                IFCBuildingGUID = building.IFCBuildingGUID,
                IFCEnabled = building.IFCEnabled,
                IFCFileUrl = building.IFCFileUrl,
                CoordinateSystem = building.CoordinateSystem,
                LocalOriginX = building.LocalOriginX,
                LocalOriginY = building.LocalOriginY,
                LocalOriginZ = building.LocalOriginZ,
                LogoUrl = building.LogoUrl,
                ModifiedByUserID = building.ModifiedByUserID,
                ModifiedDate = building.ModifiedDate
            };
        }
    }

    // ====================================================================
    // DTOs and Request/Response Models for Building
    // ====================================================================

    public class BuildingDto
    {
        public int BuildingID { get; set; }
        public string BuildingName { get; set; }
        public string BuildingCode { get; set; }
        public string Description { get; set; }
        public string AddressStreet { get; set; }
        public string AddressCity { get; set; }
        public string AddressPostalCode { get; set; }
        public string AddressCountry { get; set; }
        public string Owner { get; set; }
        public string Creator { get; set; }
        public string IFCProjectGUID { get; set; }
        public string IFCBuildingGUID { get; set; }
        public bool IFCEnabled { get; set; }
        public string IFCFileUrl { get; set; }
        public string CoordinateSystem { get; set; }
        public decimal? LocalOriginX { get; set; }
        public decimal? LocalOriginY { get; set; }
        public decimal? LocalOriginZ { get; set; }
        public string LogoUrl { get; set; }
        public string ModifiedByUserID { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class BuildingListResponse : BaseResponse
    {
        public List<BuildingDto> Buildings { get; set; }
    }

    public class BuildingResponse : BaseResponse
    {
        public BuildingDto Building { get; set; }
        public string StandardizedName { get; set; }
    }

    public class CreateBuildingRequest
    {
        public string BuildingName { get; set; }
        public string BuildingCode { get; set; }
        public string Description { get; set; }
        public string AddressStreet { get; set; }
        public string AddressCity { get; set; }
        public string AddressPostalCode { get; set; }
        public string AddressCountry { get; set; }
        public string Owner { get; set; }
        public string Creator { get; set; }
        public string IFCProjectGUID { get; set; }
        public string IFCBuildingGUID { get; set; }
        public bool IFCEnabled { get; set; }
        public string IFCFileUrl { get; set; }
        public string CoordinateSystem { get; set; }
        public decimal? LocalOriginX { get; set; }
        public decimal? LocalOriginY { get; set; }
        public decimal? LocalOriginZ { get; set; }
        public string LogoUrl { get; set; }
        public string UserId { get; set; }
    }

    public class UpdateBuildingRequest
    {
        public string BuildingName { get; set; }
        public string BuildingCode { get; set; }
        public string Description { get; set; }
        public string AddressStreet { get; set; }
        public string AddressCity { get; set; }
        public string AddressPostalCode { get; set; }
        public string AddressCountry { get; set; }
        public string Owner { get; set; }
        public string Creator { get; set; }
        public string IFCProjectGUID { get; set; }
        public string IFCBuildingGUID { get; set; }
        public bool? IFCEnabled { get; set; }
        public string IFCFileUrl { get; set; }
        public string CoordinateSystem { get; set; }
        public decimal? LocalOriginX { get; set; }
        public decimal? LocalOriginY { get; set; }
        public decimal? LocalOriginZ { get; set; }
        public string LogoUrl { get; set; }
        public string UserId { get; set; }
    }

    public class SaveConfigurationRequest
    {
        public string PredefinedFolder { get; set; }
        public string IfcSpacesFile { get; set; }
        public bool? IfcEnabled { get; set; }
        public string IfcProjectGUID { get; set; }
        public string IfcBuildingGUID { get; set; }
        public string IfcFileUrl { get; set; }
        public string UserId { get; set; }
    }
}

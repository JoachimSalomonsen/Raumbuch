using ClosedXML.Excel;
using RaumbuchService.Data;
using RaumbuchService.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

namespace RaumbuchService.Controllers
{
    /// <summary>
    /// Controller for managing Raumprogramm (SOLL) data using Azure SQL Database.
    /// Provides CRUD operations for RoomType, InventoryTemplate, and Room data.
    /// Implements authorization checks against UserAccess table.
    /// </summary>
    [RoutePrefix("api")]
    public class RaumprogrammController : ApiController
    {
        private readonly string _tempFolder = Path.Combine(Path.GetTempPath(), "Raumbuch");
        
        /// <summary>
        /// Valid DataType values for InventoryTemplate.
        /// </summary>
        private static readonly string[] ValidDataTypes = { "Text", "Number", "Boolean", "Integer", "Decimal" };

        public RaumprogrammController()
        {
            Directory.CreateDirectory(_tempFolder);
        }

        // ====================================================================
        // AUTHORIZATION HELPERS
        // ====================================================================

        /// <summary>
        /// Gets user role from UserAccess table.
        /// If the table is empty (first-time setup), returns "Admin" to allow initial setup.
        /// If userId is empty but table has users, returns "NoAccess".
        /// </summary>
        private async Task<string> GetUserRoleAsync(RaumbuchContext db, string userId)
        {
            // Check if UserAccess table is empty (first-time setup mode)
            var hasAnyUsers = await db.UserAccess.AnyAsync();
            if (!hasAnyUsers)
            {
                // Allow all operations during initial setup
                return "Admin";
            }

            if (string.IsNullOrEmpty(userId))
                return "NoAccess";

            var user = await db.UserAccess.FindAsync(userId);
            return user?.Role ?? "NoAccess";
        }

        /// <summary>
        /// Checks if user has read access (Reader, Editor, or Admin).
        /// </summary>
        private bool CanRead(string role)
        {
            return role == "Reader" || role == "Editor" || role == "Admin";
        }

        /// <summary>
        /// Checks if user has write access (Editor or Admin).
        /// </summary>
        private bool CanWrite(string role)
        {
            return role == "Editor" || role == "Admin";
        }

        // ====================================================================
        // USER ACCESS ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Gets all users with their access levels.
        /// GET /api/useraccess
        /// </summary>
        [HttpGet]
        [Route("useraccess")]
        public async Task<IHttpActionResult> GetUserAccess()
        {
            try
            {
                using (var db = new RaumbuchContext())
                {
                    var users = await db.UserAccess
                        .OrderBy(u => u.UserName)
                        .Select(u => new UserAccessDto
                        {
                            UserID = u.UserID,
                            UserName = u.UserName,
                            Role = u.Role
                        })
                        .ToListAsync();

                    return Ok(new UserAccessListResponse
                    {
                        Success = true,
                        Message = $"{users.Count} Benutzer gefunden.",
                        Users = users
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetUserAccess: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Laden der Benutzer: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Creates or updates user access.
        /// POST /api/useraccess
        /// </summary>
        [HttpPost]
        [Route("useraccess")]
        public async Task<IHttpActionResult> UpsertUserAccess([FromBody] UserAccessRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.UserID))
                {
                    return BadRequest("UserID ist erforderlich.");
                }

                // Validate role
                var validRoles = new[] { "Admin", "Editor", "Reader", "NoAccess" };
                if (!validRoles.Contains(request.Role))
                {
                    return BadRequest($"Ungültige Rolle. Erlaubt: {string.Join(", ", validRoles)}");
                }

                using (var db = new RaumbuchContext())
                {
                    var user = await db.UserAccess.FindAsync(request.UserID);
                    
                    if (user == null)
                    {
                        user = new UserAccess
                        {
                            UserID = request.UserID,
                            UserName = request.UserName,
                            Role = request.Role
                        };
                        db.UserAccess.Add(user);
                    }
                    else
                    {
                        user.UserName = request.UserName;
                        user.Role = request.Role;
                    }

                    await db.SaveChangesAsync();

                    return Ok(new UserAccessResponse
                    {
                        Success = true,
                        Message = "Benutzerzugriff gespeichert.",
                        User = new UserAccessDto
                        {
                            UserID = user.UserID,
                            UserName = user.UserName,
                            Role = user.Role
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpsertUserAccess: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Speichern des Benutzerzugriffs: {ex.Message}", ex));
            }
        }

        // ====================================================================
        // ROOM TYPE ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Creates a new RoomType.
        /// POST /api/roomtype
        /// </summary>
        [HttpPost]
        [Route("roomtype")]
        public async Task<IHttpActionResult> CreateRoomType([FromBody] RoomTypeRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest("Name ist erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    // Authorization check
                    var role = await GetUserRoleAsync(db, request.UserId);
                    if (!CanWrite(role))
                    {
                        return Unauthorized();
                    }

                    // Check for duplicate name
                    bool exists = await db.RoomTypes
                        .AnyAsync(rt => rt.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase));

                    if (exists)
                    {
                        return BadRequest($"Raumtyp '{request.Name}' existiert bereits.");
                    }

                    var roomType = new RoomType 
                    { 
                        Name = request.Name,
                        RoomCategory = request.RoomCategory
                    };
                    db.RoomTypes.Add(roomType);
                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new RoomTypeResponse
                    {
                        Success = true,
                        Message = "Raumtyp erfolgreich erstellt.",
                        RoomType = new RoomTypeDto
                        {
                            RoomTypeID = roomType.RoomTypeID,
                            Name = roomType.Name,
                            RoomCategory = roomType.RoomCategory,
                            ModifiedByUserID = roomType.ModifiedByUserID,
                            ModifiedDate = roomType.ModifiedDate
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CreateRoomType: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Erstellen des Raumtyps: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Gets all RoomTypes.
        /// GET /api/roomtype
        /// </summary>
        [HttpGet]
        [Route("roomtype")]
        public async Task<IHttpActionResult> GetRoomTypes()
        {
            try
            {
                using (var db = new RaumbuchContext())
                {
                    var roomTypes = await db.RoomTypes
                        .OrderBy(rt => rt.Name)
                        .Select(rt => new RoomTypeDto
                        {
                            RoomTypeID = rt.RoomTypeID,
                            Name = rt.Name,
                            RoomCategory = rt.RoomCategory,
                            ModifiedByUserID = rt.ModifiedByUserID,
                            ModifiedDate = rt.ModifiedDate
                        })
                        .ToListAsync();

                    return Ok(new RoomTypesResponse
                    {
                        Success = true,
                        Message = $"{roomTypes.Count} Raumtypen gefunden.",
                        RoomTypes = roomTypes
                    });
                }
            }
            catch (System.Data.Entity.Core.EntityException ex)
            {
                // Database connection or entity framework error
                System.Diagnostics.Debug.WriteLine($"Entity Framework error in GetRoomTypes: {ex.Message}");
                var innerMessage = ex.InnerException?.Message ?? "Keine weiteren Details";
                return InternalServerError(new Exception($"Datenbankverbindungsfehler: {innerMessage}. Bitte stellen Sie sicher, dass CreateSchema.sql ausgeführt wurde.", ex));
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                // SQL Server specific error
                System.Diagnostics.Debug.WriteLine($"SQL error in GetRoomTypes: {ex.Message}");
                return InternalServerError(new Exception($"SQL-Fehler: {ex.Message}. Fehlercode: {ex.Number}", ex));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetRoomTypes: {ex.Message}");
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                return InternalServerError(new Exception($"Fehler beim Laden der Raumtypen: {innerMessage}", ex));
            }
        }

        /// <summary>
        /// Updates a RoomType.
        /// PUT /api/roomtype/{id}
        /// </summary>
        [HttpPut]
        [Route("roomtype/{id:int}")]
        public async Task<IHttpActionResult> UpdateRoomType(int id, [FromBody] RoomTypeRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest("Name ist erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    // Authorization check
                    var role = await GetUserRoleAsync(db, request.UserId);
                    if (!CanWrite(role))
                    {
                        return Unauthorized();
                    }

                    var roomType = await db.RoomTypes.FindAsync(id);
                    if (roomType == null)
                    {
                        return NotFound();
                    }

                    // Check for duplicate name (excluding current record)
                    bool exists = await db.RoomTypes
                        .AnyAsync(rt => rt.RoomTypeID != id && 
                                       rt.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase));

                    if (exists)
                    {
                        return BadRequest($"Raumtyp '{request.Name}' existiert bereits.");
                    }

                    roomType.Name = request.Name;
                    roomType.RoomCategory = request.RoomCategory;
                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new RoomTypeResponse
                    {
                        Success = true,
                        Message = "Raumtyp erfolgreich aktualisiert.",
                        RoomType = new RoomTypeDto
                        {
                            RoomTypeID = roomType.RoomTypeID,
                            Name = roomType.Name,
                            RoomCategory = roomType.RoomCategory,
                            ModifiedByUserID = roomType.ModifiedByUserID,
                            ModifiedDate = roomType.ModifiedDate
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateRoomType: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Aktualisieren des Raumtyps: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Deletes a RoomType.
        /// DELETE /api/roomtype/{id}
        /// </summary>
        [HttpDelete]
        [Route("roomtype/{id:int}")]
        public async Task<IHttpActionResult> DeleteRoomType(int id, [FromUri] string userId = null)
        {
            try
            {
                using (var db = new RaumbuchContext())
                {
                    // Authorization check
                    var role = await GetUserRoleAsync(db, userId);
                    if (!CanWrite(role))
                    {
                        return Unauthorized();
                    }

                    var roomType = await db.RoomTypes
                        .Include(rt => rt.Rooms)
                        .FirstOrDefaultAsync(rt => rt.RoomTypeID == id);

                    if (roomType == null)
                    {
                        return NotFound();
                    }

                    // Check if there are rooms using this type
                    if (roomType.Rooms.Any())
                    {
                        return BadRequest($"Raumtyp kann nicht gelöscht werden, da {roomType.Rooms.Count} Räume diesen Typ verwenden.");
                    }

                    db.RoomTypes.Remove(roomType);
                    await db.SaveChangesAsync();

                    return Ok(new BaseResponse
                    {
                        Success = true,
                        Message = "Raumtyp erfolgreich gelöscht."
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeleteRoomType: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Löschen des Raumtyps: {ex.Message}", ex));
            }
        }

        // ====================================================================
        // INVENTORY TEMPLATE ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Creates a new InventoryTemplate.
        /// POST /api/inventorytemplate
        /// </summary>
        [HttpPost]
        [Route("inventorytemplate")]
        public async Task<IHttpActionResult> CreateInventoryTemplate([FromBody] InventoryTemplateRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.PropertyName))
                {
                    return BadRequest("PropertyName ist erforderlich.");
                }

                // Validate DataType if provided
                if (!string.IsNullOrWhiteSpace(request.DataType) && !ValidDataTypes.Contains(request.DataType))
                {
                    return BadRequest($"Ungültiger DataType. Erlaubt: {string.Join(", ", ValidDataTypes)}");
                }

                using (var db = new RaumbuchContext())
                {
                    // Authorization check
                    var role = await GetUserRoleAsync(db, request.UserId);
                    if (!CanWrite(role))
                    {
                        return Unauthorized();
                    }

                    // Check for duplicate property name
                    bool exists = await db.InventoryTemplates
                        .AnyAsync(it => it.PropertyName.Equals(request.PropertyName, StringComparison.OrdinalIgnoreCase));

                    if (exists)
                    {
                        return BadRequest($"Ausstattungseigenschaft '{request.PropertyName}' existiert bereits.");
                    }

                    var template = new InventoryTemplate 
                    { 
                        PropertyName = request.PropertyName,
                        DataType = request.DataType ?? InventoryTemplate.DefaultDataType,
                        Unit = request.Unit
                    };
                    db.InventoryTemplates.Add(template);
                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new InventoryTemplateResponse
                    {
                        Success = true,
                        Message = "Ausstattungseigenschaft erfolgreich erstellt.",
                        InventoryTemplate = new InventoryTemplateDto
                        {
                            InventoryTemplateID = template.InventoryTemplateID,
                            PropertyName = template.PropertyName,
                            DataType = template.DataType,
                            Unit = template.Unit,
                            ModifiedByUserID = template.ModifiedByUserID,
                            ModifiedDate = template.ModifiedDate
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CreateInventoryTemplate: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Erstellen der Ausstattungseigenschaft: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Gets all InventoryTemplates.
        /// GET /api/inventorytemplate
        /// </summary>
        [HttpGet]
        [Route("inventorytemplate")]
        public async Task<IHttpActionResult> GetInventoryTemplates()
        {
            try
            {
                using (var db = new RaumbuchContext())
                {
                    var templates = await db.InventoryTemplates
                        .OrderBy(it => it.PropertyName)
                        .Select(it => new InventoryTemplateDto
                        {
                            InventoryTemplateID = it.InventoryTemplateID,
                            PropertyName = it.PropertyName,
                            DataType = it.DataType,
                            Unit = it.Unit,
                            ModifiedByUserID = it.ModifiedByUserID,
                            ModifiedDate = it.ModifiedDate
                        })
                        .ToListAsync();

                    return Ok(new InventoryTemplatesResponse
                    {
                        Success = true,
                        Message = $"{templates.Count} Ausstattungseigenschaften gefunden.",
                        InventoryTemplates = templates
                    });
                }
            }
            catch (System.Data.Entity.Core.EntityException ex)
            {
                // Database connection or entity framework error
                System.Diagnostics.Debug.WriteLine($"Entity Framework error in GetInventoryTemplates: {ex.Message}");
                var innerMessage = ex.InnerException?.Message ?? "Keine weiteren Details";
                return InternalServerError(new Exception($"Datenbankverbindungsfehler: {innerMessage}. Bitte stellen Sie sicher, dass CreateSchema.sql ausgeführt wurde.", ex));
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                // SQL Server specific error
                System.Diagnostics.Debug.WriteLine($"SQL error in GetInventoryTemplates: {ex.Message}");
                return InternalServerError(new Exception($"SQL-Fehler: {ex.Message}. Fehlercode: {ex.Number}", ex));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetInventoryTemplates: {ex.Message}");
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                return InternalServerError(new Exception($"Fehler beim Laden der Ausstattungseigenschaften: {innerMessage}", ex));
            }
        }

        /// <summary>
        /// Updates an InventoryTemplate.
        /// PUT /api/inventorytemplate/{id}
        /// </summary>
        [HttpPut]
        [Route("inventorytemplate/{id:int}")]
        public async Task<IHttpActionResult> UpdateInventoryTemplate(int id, [FromBody] InventoryTemplateRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.PropertyName))
                {
                    return BadRequest("PropertyName ist erforderlich.");
                }

                // Validate DataType if provided
                if (!string.IsNullOrWhiteSpace(request.DataType) && !ValidDataTypes.Contains(request.DataType))
                {
                    return BadRequest($"Ungültiger DataType. Erlaubt: {string.Join(", ", ValidDataTypes)}");
                }

                using (var db = new RaumbuchContext())
                {
                    // Authorization check
                    var role = await GetUserRoleAsync(db, request.UserId);
                    if (!CanWrite(role))
                    {
                        return Unauthorized();
                    }

                    var template = await db.InventoryTemplates.FindAsync(id);
                    if (template == null)
                    {
                        return NotFound();
                    }

                    // Check for duplicate property name (excluding current record)
                    bool exists = await db.InventoryTemplates
                        .AnyAsync(it => it.InventoryTemplateID != id && 
                                       it.PropertyName.Equals(request.PropertyName, StringComparison.OrdinalIgnoreCase));

                    if (exists)
                    {
                        return BadRequest($"Ausstattungseigenschaft '{request.PropertyName}' existiert bereits.");
                    }

                    template.PropertyName = request.PropertyName;
                    template.DataType = request.DataType ?? template.DataType ?? InventoryTemplate.DefaultDataType;
                    template.Unit = request.Unit;
                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new InventoryTemplateResponse
                    {
                        Success = true,
                        Message = "Ausstattungseigenschaft erfolgreich aktualisiert.",
                        InventoryTemplate = new InventoryTemplateDto
                        {
                            InventoryTemplateID = template.InventoryTemplateID,
                            PropertyName = template.PropertyName,
                            DataType = template.DataType,
                            Unit = template.Unit,
                            ModifiedByUserID = template.ModifiedByUserID,
                            ModifiedDate = template.ModifiedDate
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateInventoryTemplate: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Aktualisieren der Ausstattungseigenschaft: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Deletes an InventoryTemplate.
        /// DELETE /api/inventorytemplate/{id}
        /// </summary>
        [HttpDelete]
        [Route("inventorytemplate/{id:int}")]
        public async Task<IHttpActionResult> DeleteInventoryTemplate(int id, [FromUri] string userId = null)
        {
            try
            {
                using (var db = new RaumbuchContext())
                {
                    // Authorization check
                    var role = await GetUserRoleAsync(db, userId);
                    if (!CanWrite(role))
                    {
                        return Unauthorized();
                    }

                    var template = await db.InventoryTemplates
                        .Include(it => it.RoomInventories)
                        .FirstOrDefaultAsync(it => it.InventoryTemplateID == id);

                    if (template == null)
                    {
                        return NotFound();
                    }

                    // Check if there are room inventories using this template
                    if (template.RoomInventories.Any())
                    {
                        return BadRequest($"Ausstattungseigenschaft kann nicht gelöscht werden, da {template.RoomInventories.Count} Inventareinträge diese Eigenschaft verwenden.");
                    }

                    db.InventoryTemplates.Remove(template);
                    await db.SaveChangesAsync();

                    return Ok(new BaseResponse
                    {
                        Success = true,
                        Message = "Ausstattungseigenschaft erfolgreich gelöscht."
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeleteInventoryTemplate: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Löschen der Ausstattungseigenschaft: {ex.Message}", ex));
            }
        }

        // ====================================================================
        // ROOM ENDPOINTS (for manual room management)
        // ====================================================================

        /// <summary>
        /// Creates a new Room.
        /// POST /api/room
        /// </summary>
        [HttpPost]
        [Route("room")]
        public async Task<IHttpActionResult> CreateRoom([FromBody] RoomRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Name) || request.RoomTypeID <= 0)
                {
                    return BadRequest("Name und RoomTypeID sind erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    // Authorization check
                    var role = await GetUserRoleAsync(db, request.UserId);
                    if (!CanWrite(role))
                    {
                        return Unauthorized();
                    }

                    // Verify room type exists
                    var roomType = await db.RoomTypes.FindAsync(request.RoomTypeID);
                    if (roomType == null)
                    {
                        return BadRequest($"Raumtyp mit ID {request.RoomTypeID} existiert nicht.");
                    }

                    var room = new Room
                    {
                        RoomTypeID = request.RoomTypeID,
                        Name = request.Name,
                        NetAreaPlanned = request.NetAreaPlanned,
                        NetAreaActual = request.NetAreaActual,
                        GrossAreaPlanned = request.GrossAreaPlanned,
                        GrossAreaActual = request.GrossAreaActual
                    };

                    db.Rooms.Add(room);
                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new RoomResponse
                    {
                        Success = true,
                        Message = "Raum erfolgreich erstellt.",
                        Room = new RoomDto
                        {
                            RoomID = room.RoomID,
                            RoomTypeID = room.RoomTypeID,
                            RoomTypeName = roomType.Name,
                            RoomCategory = roomType.RoomCategory,
                            Name = room.Name,
                            NetAreaPlanned = room.NetAreaPlanned,
                            NetAreaActual = room.NetAreaActual,
                            GrossAreaPlanned = room.GrossAreaPlanned,
                            GrossAreaActual = room.GrossAreaActual,
                            ModifiedByUserID = room.ModifiedByUserID,
                            ModifiedDate = room.ModifiedDate
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CreateRoom: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Erstellen des Raums: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Updates a Room.
        /// PUT /api/room/{id}
        /// </summary>
        [HttpPut]
        [Route("room/{id:int}")]
        public async Task<IHttpActionResult> UpdateRoom(int id, [FromBody] RoomRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Name) || request.RoomTypeID <= 0)
                {
                    return BadRequest("Name und RoomTypeID sind erforderlich.");
                }

                using (var db = new RaumbuchContext())
                {
                    // Authorization check
                    var role = await GetUserRoleAsync(db, request.UserId);
                    if (!CanWrite(role))
                    {
                        return Unauthorized();
                    }

                    var room = await db.Rooms
                        .Include(r => r.RoomType)
                        .FirstOrDefaultAsync(r => r.RoomID == id);

                    if (room == null)
                    {
                        return NotFound();
                    }

                    // Verify room type exists
                    var roomType = await db.RoomTypes.FindAsync(request.RoomTypeID);
                    if (roomType == null)
                    {
                        return BadRequest($"Raumtyp mit ID {request.RoomTypeID} existiert nicht.");
                    }

                    room.RoomTypeID = request.RoomTypeID;
                    room.Name = request.Name;
                    room.NetAreaPlanned = request.NetAreaPlanned;
                    room.NetAreaActual = request.NetAreaActual;
                    room.GrossAreaPlanned = request.GrossAreaPlanned;
                    room.GrossAreaActual = request.GrossAreaActual;

                    await db.SaveChangesWithAuditAsync(request.UserId);

                    return Ok(new RoomResponse
                    {
                        Success = true,
                        Message = "Raum erfolgreich aktualisiert.",
                        Room = new RoomDto
                        {
                            RoomID = room.RoomID,
                            RoomTypeID = room.RoomTypeID,
                            RoomTypeName = roomType.Name,
                            RoomCategory = roomType.RoomCategory,
                            Name = room.Name,
                            NetAreaPlanned = room.NetAreaPlanned,
                            NetAreaActual = room.NetAreaActual,
                            GrossAreaPlanned = room.GrossAreaPlanned,
                            GrossAreaActual = room.GrossAreaActual,
                            ModifiedByUserID = room.ModifiedByUserID,
                            ModifiedDate = room.ModifiedDate
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateRoom: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Aktualisieren des Raums: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Deletes a Room.
        /// DELETE /api/room/{id}
        /// </summary>
        [HttpDelete]
        [Route("room/{id:int}")]
        public async Task<IHttpActionResult> DeleteRoom(int id, [FromUri] string userId = null)
        {
            try
            {
                using (var db = new RaumbuchContext())
                {
                    // Authorization check
                    var role = await GetUserRoleAsync(db, userId);
                    if (!CanWrite(role))
                    {
                        return Unauthorized();
                    }

                    var room = await db.Rooms
                        .Include(r => r.RoomInventories)
                        .FirstOrDefaultAsync(r => r.RoomID == id);

                    if (room == null)
                    {
                        return NotFound();
                    }

                    // Delete associated room inventories first
                    if (room.RoomInventories.Any())
                    {
                        db.RoomInventories.RemoveRange(room.RoomInventories);
                    }

                    db.Rooms.Remove(room);
                    await db.SaveChangesAsync();

                    return Ok(new BaseResponse
                    {
                        Success = true,
                        Message = "Raum erfolgreich gelöscht."
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeleteRoom: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Löschen des Raums: {ex.Message}", ex));
            }
        }

        // ====================================================================
        // RAUMPROGRAMM ENDPOINTS (Main Data Access)
        // ====================================================================

        /// <summary>
        /// Gets the full hierarchical Raumprogramm (SOLL) data.
        /// GET /api/raumprogram
        /// Supports pagination and filtering.
        /// </summary>
        [HttpGet]
        [Route("raumprogram")]
        public async Task<IHttpActionResult> GetRaumprogram(
            int page = 1, 
            int pageSize = 50, 
            int? roomTypeId = null,
            int? inventoryTemplateId = null,
            string search = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 100) pageSize = 100;

                using (var db = new RaumbuchContext())
                {
                    // Build query for rooms with their related data
                    var query = db.Rooms
                        .Include(r => r.RoomType)
                        .Include(r => r.RoomInventories.Select(ri => ri.InventoryTemplate))
                        .AsQueryable();

                    // Apply filters
                    if (roomTypeId.HasValue)
                    {
                        query = query.Where(r => r.RoomTypeID == roomTypeId.Value);
                    }

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        query = query.Where(r => r.Name.Contains(search) || 
                                                  r.RoomType.Name.Contains(search));
                    }

                    // Get total count for pagination
                    int totalCount = await query.CountAsync();
                    int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                    // Apply pagination
                    var rooms = await query
                        .OrderBy(r => r.RoomType.Name)
                        .ThenBy(r => r.Name)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    // Filter inventory items if inventoryTemplateId is specified
                    var roomDtos = rooms.Select(r => new RoomWithInventoryDto
                    {
                        RoomID = r.RoomID,
                        RoomTypeID = r.RoomTypeID,
                        RoomTypeName = r.RoomType?.Name,
                        RoomCategory = r.RoomType?.RoomCategory,
                        Name = r.Name,
                        NetAreaPlanned = r.NetAreaPlanned,
                        NetAreaActual = r.NetAreaActual,
                        GrossAreaPlanned = r.GrossAreaPlanned,
                        GrossAreaActual = r.GrossAreaActual,
                        // IFC Properties
                        PubliclyAccessible = r.PubliclyAccessible,
                        HandicapAccessible = r.HandicapAccessible,
                        IsExternal = r.IsExternal,
                        Description = r.Description,
                        ObjectType = r.ObjectType,
                        PredefinedType = r.PredefinedType,
                        ElevationWithFlooring = r.ElevationWithFlooring,
                        ModifiedByUserID = r.ModifiedByUserID,
                        ModifiedDate = r.ModifiedDate,
                        Inventory = (inventoryTemplateId.HasValue
                            ? r.RoomInventories.Where(ri => ri.InventoryTemplateID == inventoryTemplateId.Value)
                            : r.RoomInventories)
                            .Select(ri => new RoomInventoryDto
                            {
                                RoomInventoryID = ri.RoomInventoryID,
                                InventoryTemplateID = ri.InventoryTemplateID,
                                PropertyName = ri.InventoryTemplate?.PropertyName,
                                DataType = ri.InventoryTemplate?.DataType,
                                Unit = ri.InventoryTemplate?.Unit,
                                ValuePlanned = ri.ValuePlanned,
                                ValueActual = ri.ValueActual,
                                Comment = ri.Comment,
                                ModifiedByUserID = ri.ModifiedByUserID,
                                ModifiedDate = ri.ModifiedDate
                            }).ToList()
                    }).ToList();

                    return Ok(new RaumprogramResponse
                    {
                        Success = true,
                        Message = $"{totalCount} Räume gefunden.",
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = totalCount,
                        TotalPages = totalPages,
                        Rooms = roomDtos
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetRaumprogram: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Laden des Raumprogramms: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Imports Raumprogramm data from an Excel file.
        /// POST /api/raumprogram/import
        /// </summary>
        [HttpPost]
        [Route("raumprogram/import")]
        public async Task<IHttpActionResult> ImportRaumprogram([FromBody] RaumprogramImportRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.FileId))
                {
                    return BadRequest("AccessToken und FileId sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);

                // Download Excel file
                string excelPath = await tcService.DownloadFileAsync(
                    request.FileId,
                    _tempFolder,
                    "Raumprogramm_Import.xlsx"
                );

                int roomTypesCreated = 0;
                int roomsCreated = 0;
                int roomsUpdated = 0;
                int inventoryItemsCreated = 0;
                var warnings = new List<string>();

                using (var db = new RaumbuchContext())
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        using (var wb = new XLWorkbook(excelPath))
                        {
                            var ws = wb.Worksheet(1);
                            var range = ws.RangeUsed();
                            if (range == null)
                            {
                                return BadRequest("Die Excel-Datei enthält keine Daten.");
                            }

                            int firstRow = range.FirstRow().RowNumber();
                            int lastRow = range.LastRow().RowNumber();
                            int lastCol = range.LastColumn().ColumnNumber();

                            // Get column mappings from request or use defaults
                            int raumtypCol = (request.ColumnMappings?.RaumtypColumn ?? 0) + 1;
                            int roomNameCol = (request.ColumnMappings?.RoomNameColumn ?? 1) + 1;
                            int areaPlannedCol = (request.ColumnMappings?.AreaPlannedColumn ?? 2) + 1;
                            int areaActualCol = request.ColumnMappings?.AreaActualColumn.HasValue == true 
                                ? request.ColumnMappings.AreaActualColumn.Value + 1 
                                : -1;

                            // Cache existing room types and rooms
                            var existingRoomTypes = await db.RoomTypes
                                .ToDictionaryAsync(rt => rt.Name.ToLower(), rt => rt);
                            var existingRooms = await db.Rooms
                                .Include(r => r.RoomType)
                                .ToDictionaryAsync(r => $"{r.RoomType.Name.ToLower()}:{r.Name.ToLower()}", r => r);

                            // Parse data rows (skip header)
                            for (int row = firstRow + 1; row <= lastRow; row++)
                            {
                                string raumtypName = ws.Cell(row, raumtypCol).GetString().Trim();
                                string roomName = ws.Cell(row, roomNameCol).GetString().Trim();

                                if (string.IsNullOrWhiteSpace(raumtypName) || string.IsNullOrWhiteSpace(roomName))
                                {
                                    continue;
                                }

                                // Parse area values - mapping to NetAreaPlanned/NetAreaActual
                                decimal? netAreaPlanned = null;
                                decimal? netAreaActual = null;

                                if (decimal.TryParse(ws.Cell(row, areaPlannedCol).GetString().Trim()
                                    .Replace(",", "."), 
                                    System.Globalization.NumberStyles.Any, 
                                    System.Globalization.CultureInfo.InvariantCulture, 
                                    out decimal parsedAreaPlanned))
                                {
                                    netAreaPlanned = parsedAreaPlanned;
                                }

                                if (areaActualCol > 0 && decimal.TryParse(ws.Cell(row, areaActualCol).GetString().Trim()
                                    .Replace(",", "."), 
                                    System.Globalization.NumberStyles.Any, 
                                    System.Globalization.CultureInfo.InvariantCulture, 
                                    out decimal parsedAreaActual))
                                {
                                    netAreaActual = parsedAreaActual;
                                }

                                // Get or create room type
                                RoomType roomType;
                                if (!existingRoomTypes.TryGetValue(raumtypName.ToLower(), out roomType))
                                {
                                    roomType = new RoomType { Name = raumtypName };
                                    db.RoomTypes.Add(roomType);
                                    // Save immediately to get the ID - necessary for FK relationship
                                    existingRoomTypes[raumtypName.ToLower()] = roomType;
                                    roomTypesCreated++;
                                }

                                // Get or create room
                                string roomKey = $"{raumtypName.ToLower()}:{roomName.ToLower()}";
                                Room room;
                                if (!existingRooms.TryGetValue(roomKey, out room))
                                {
                                    room = new Room
                                    {
                                        RoomType = roomType, // Use navigation property instead of ID
                                        Name = roomName,
                                        NetAreaPlanned = netAreaPlanned,
                                        NetAreaActual = netAreaActual
                                    };
                                    db.Rooms.Add(room);
                                    roomsCreated++;
                                }
                                else
                                {
                                    // Update existing room
                                    bool updated = false;
                                    if (netAreaPlanned.HasValue && room.NetAreaPlanned != netAreaPlanned)
                                    {
                                        room.NetAreaPlanned = netAreaPlanned;
                                        updated = true;
                                    }
                                    if (netAreaActual.HasValue && room.NetAreaActual != netAreaActual)
                                    {
                                        room.NetAreaActual = netAreaActual;
                                        updated = true;
                                    }
                                    if (updated) roomsUpdated++;
                                }
                            }

                            // Save all changes in a single batch operation
                            await db.SaveChangesAsync();
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }

                // Cleanup
                File.Delete(excelPath);

                return Ok(new RaumprogramImportResponse
                {
                    Success = true,
                    Message = "Import erfolgreich abgeschlossen.",
                    RoomTypesCreated = roomTypesCreated,
                    RoomsCreated = roomsCreated,
                    RoomsUpdated = roomsUpdated,
                    InventoryItemsCreated = inventoryItemsCreated,
                    Warnings = warnings
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ImportRaumprogram: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Importieren des Raumprogramms: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Exports Raumprogramm data to an Excel file.
        /// GET /api/raumprogram/export
        /// </summary>
        [HttpPost]
        [Route("raumprogram/export")]
        public async Task<IHttpActionResult> ExportRaumprogram([FromBody] RaumprogramExportRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.AccessToken) ||
                    string.IsNullOrWhiteSpace(request.TargetFolderId))
                {
                    return BadRequest("AccessToken und TargetFolderId sind erforderlich.");
                }

                var tcService = new TrimbleConnectService(request.AccessToken);
                string excelPath = Path.Combine(_tempFolder, "Raumprogramm_Export.xlsx");

                using (var db = new RaumbuchContext())
                {
                    // Load all data with related entities
                    var rooms = await db.Rooms
                        .Include(r => r.RoomType)
                        .Include(r => r.RoomInventories.Select(ri => ri.InventoryTemplate))
                        .OrderBy(r => r.RoomType.Name)
                        .ThenBy(r => r.Name)
                        .ToListAsync();

                    var inventoryTemplates = await db.InventoryTemplates
                        .OrderBy(it => it.PropertyName)
                        .ToListAsync();

                    using (var wb = new XLWorkbook())
                    {
                        // Create Raumprogramm sheet
                        var ws = wb.Worksheets.Add("Raumprogramm");

                        // Headers
                        ws.Cell(1, 1).Value = "Raumtyp";
                        ws.Cell(1, 2).Value = "Raum Name";
                        ws.Cell(1, 3).Value = "Nettofläche SOLL (m²)";
                        ws.Cell(1, 4).Value = "Nettofläche IST (m²)";
                        ws.Cell(1, 5).Value = "Bruttofläche SOLL (m²)";
                        ws.Cell(1, 6).Value = "Bruttofläche IST (m²)";

                        // Add inventory template columns starting after fixed columns
                        // Fixed columns: 1=Raumtyp, 2=Raum Name, 3-4=Nettofläche SOLL/IST, 5-6=Bruttofläche SOLL/IST
                        const int firstInventoryColumn = 7;
                        int col = firstInventoryColumn;
                        var templateColumns = new Dictionary<int, int>(); // TemplateID -> Column
                        foreach (var template in inventoryTemplates)
                        {
                            ws.Cell(1, col).Value = $"{template.PropertyName} (SOLL)";
                            ws.Cell(1, col + 1).Value = $"{template.PropertyName} (IST)";
                            templateColumns[template.InventoryTemplateID] = col;
                            col += 2;
                        }

                        // Style headers
                        var headerRange = ws.Range(1, 1, 1, col - 1);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        // Data rows
                        int row = 2;
                        foreach (var room in rooms)
                        {
                            ws.Cell(row, 1).Value = room.RoomType?.Name ?? "";
                            ws.Cell(row, 2).Value = room.Name;
                            ws.Cell(row, 3).Value = room.NetAreaPlanned ?? 0;
                            ws.Cell(row, 4).Value = room.NetAreaActual ?? 0;
                            ws.Cell(row, 5).Value = room.GrossAreaPlanned ?? 0;
                            ws.Cell(row, 6).Value = room.GrossAreaActual ?? 0;

                            // Add inventory values
                            foreach (var inventory in room.RoomInventories)
                            {
                                if (templateColumns.TryGetValue(inventory.InventoryTemplateID, out int invCol))
                                {
                                    ws.Cell(row, invCol).Value = inventory.ValuePlanned ?? "";
                                    ws.Cell(row, invCol + 1).Value = inventory.ValueActual ?? "";
                                }
                            }

                            row++;
                        }

                        // Auto-fit columns
                        ws.Columns().AdjustToContents();

                        // Create Zusammenfassung (Summary) sheet
                        var summaryWs = wb.Worksheets.Add("Zusammenfassung");

                        // Summary headers
                        summaryWs.Cell(1, 1).Value = "Raumtyp";
                        summaryWs.Cell(1, 2).Value = "Anzahl Räume";
                        summaryWs.Cell(1, 3).Value = "Nettofläche SOLL (m²)";
                        summaryWs.Cell(1, 4).Value = "Nettofläche IST (m²)";
                        summaryWs.Cell(1, 5).Value = "Abweichung (%)";
                        summaryWs.Cell(1, 6).Value = "Status";

                        var summaryHeaderRange = summaryWs.Range(1, 1, 1, 6);
                        summaryHeaderRange.Style.Font.Bold = true;
                        summaryHeaderRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        // Group by room type - use NetArea for summary
                        var roomTypeSummary = rooms
                            .GroupBy(r => r.RoomType?.Name ?? "Unbekannt")
                            .Select(g => new
                            {
                                RoomTypeName = g.Key,
                                RoomCount = g.Count(),
                                TotalSoll = g.Sum(r => r.NetAreaPlanned ?? 0),
                                TotalIst = g.Sum(r => r.NetAreaActual ?? 0)
                            })
                            .OrderBy(x => x.RoomTypeName)
                            .ToList();

                        int summaryRow = 2;
                        foreach (var summary in roomTypeSummary)
                        {
                            summaryWs.Cell(summaryRow, 1).Value = summary.RoomTypeName;
                            summaryWs.Cell(summaryRow, 2).Value = summary.RoomCount;
                            summaryWs.Cell(summaryRow, 3).Value = summary.TotalSoll;
                            summaryWs.Cell(summaryRow, 4).Value = summary.TotalIst;

                            decimal percentage = summary.TotalSoll > 0 
                                ? (summary.TotalIst / summary.TotalSoll) * 100 
                                : 0;
                            summaryWs.Cell(summaryRow, 5).Value = Math.Round(percentage, 1);

                            string status = percentage >= 90 && percentage <= 110 
                                ? "Erfüllt" 
                                : percentage < 90 
                                    ? "Unterschritten" 
                                    : "Überschritten";
                            summaryWs.Cell(summaryRow, 6).Value = status;

                            // Color rows based on status
                            if (status == "Unterschritten")
                            {
                                summaryWs.Range(summaryRow, 1, summaryRow, 6).Style.Fill.BackgroundColor = XLColor.LightPink;
                            }
                            else if (status == "Erfüllt")
                            {
                                summaryWs.Range(summaryRow, 1, summaryRow, 6).Style.Fill.BackgroundColor = XLColor.LightGreen;
                            }

                            summaryRow++;
                        }

                        summaryWs.Columns().AdjustToContents();

                        wb.SaveAs(excelPath);
                    }
                }

                // Upload to Trimble Connect
                string fileId = await tcService.UploadFileAsync(request.TargetFolderId, excelPath);

                // Cleanup
                File.Delete(excelPath);

                return Ok(new RaumprogramExportResponse
                {
                    Success = true,
                    Message = "Export erfolgreich abgeschlossen.",
                    FileId = fileId,
                    FileName = "Raumprogramm_Export.xlsx"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ExportRaumprogram: {ex.Message}");
                return InternalServerError(new Exception($"Fehler beim Exportieren des Raumprogramms: {ex.Message}", ex));
            }
        }
    }

    // ====================================================================
    // DTOs and Request/Response Models
    // ====================================================================

    public class BaseResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    // User Access DTOs
    public class UserAccessRequest
    {
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string Role { get; set; }
    }

    public class UserAccessDto
    {
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string Role { get; set; }
    }

    public class UserAccessResponse : BaseResponse
    {
        public UserAccessDto User { get; set; }
    }

    public class UserAccessListResponse : BaseResponse
    {
        public List<UserAccessDto> Users { get; set; }
    }

    // Room Type DTOs
    public class RoomTypeRequest
    {
        public string Name { get; set; }
        public string RoomCategory { get; set; }
        public string UserId { get; set; }
    }

    public class RoomTypeDto
    {
        public int RoomTypeID { get; set; }
        public string Name { get; set; }
        public string RoomCategory { get; set; }
        public string ModifiedByUserID { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class RoomTypeResponse : BaseResponse
    {
        public RoomTypeDto RoomType { get; set; }
    }

    public class RoomTypesResponse : BaseResponse
    {
        public List<RoomTypeDto> RoomTypes { get; set; }
    }

    // Inventory Template DTOs
    public class InventoryTemplateRequest
    {
        public string PropertyName { get; set; }
        public string DataType { get; set; }
        public string Unit { get; set; }
        public string UserId { get; set; }
    }

    public class InventoryTemplateDto
    {
        public int InventoryTemplateID { get; set; }
        public string PropertyName { get; set; }
        public string DataType { get; set; }
        public string Unit { get; set; }
        public string ModifiedByUserID { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class InventoryTemplateResponse : BaseResponse
    {
        public InventoryTemplateDto InventoryTemplate { get; set; }
    }

    public class InventoryTemplatesResponse : BaseResponse
    {
        public List<InventoryTemplateDto> InventoryTemplates { get; set; }
    }

    // Room DTOs
    public class RoomRequest
    {
        public int RoomTypeID { get; set; }
        public string Name { get; set; }
        public decimal? NetAreaPlanned { get; set; }
        public decimal? NetAreaActual { get; set; }
        public decimal? GrossAreaPlanned { get; set; }
        public decimal? GrossAreaActual { get; set; }
        // IFC Standard Properties
        public bool? PubliclyAccessible { get; set; }
        public bool? HandicapAccessible { get; set; }
        public bool? IsExternal { get; set; }
        public string Description { get; set; }
        public string ObjectType { get; set; }
        public string PredefinedType { get; set; }
        public decimal? ElevationWithFlooring { get; set; }
        public string UserId { get; set; }
    }

    public class RoomDto
    {
        public int RoomID { get; set; }
        public int RoomTypeID { get; set; }
        public string RoomTypeName { get; set; }
        public string RoomCategory { get; set; }
        public string Name { get; set; }
        public decimal? NetAreaPlanned { get; set; }
        public decimal? NetAreaActual { get; set; }
        public decimal? GrossAreaPlanned { get; set; }
        public decimal? GrossAreaActual { get; set; }
        // IFC Standard Properties
        public bool? PubliclyAccessible { get; set; }
        public bool? HandicapAccessible { get; set; }
        public bool? IsExternal { get; set; }
        public string Description { get; set; }
        public string ObjectType { get; set; }
        public string PredefinedType { get; set; }
        public decimal? ElevationWithFlooring { get; set; }
        public string ModifiedByUserID { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class RoomResponse : BaseResponse
    {
        public RoomDto Room { get; set; }
    }

    // Room Inventory DTOs
    public class RoomInventoryDto
    {
        public int RoomInventoryID { get; set; }
        public int InventoryTemplateID { get; set; }
        public string PropertyName { get; set; }
        public string DataType { get; set; }
        public string Unit { get; set; }
        public string ValuePlanned { get; set; }
        public string ValueActual { get; set; }
        public string Comment { get; set; }
        public string ModifiedByUserID { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class RoomWithInventoryDto : RoomDto
    {
        public List<RoomInventoryDto> Inventory { get; set; }
    }

    // Raumprogramm Response
    public class RaumprogramResponse : BaseResponse
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<RoomWithInventoryDto> Rooms { get; set; }
    }

    // Import/Export DTOs
    public class ImportColumnMappings
    {
        public int RaumtypColumn { get; set; }
        public int RoomNameColumn { get; set; }
        public int AreaPlannedColumn { get; set; }
        public int? AreaActualColumn { get; set; }
    }

    public class RaumprogramImportRequest
    {
        public string AccessToken { get; set; }
        public string FileId { get; set; }
        public ImportColumnMappings ColumnMappings { get; set; }
    }

    public class RaumprogramImportResponse : BaseResponse
    {
        public int RoomTypesCreated { get; set; }
        public int RoomsCreated { get; set; }
        public int RoomsUpdated { get; set; }
        public int InventoryItemsCreated { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class RaumprogramExportRequest
    {
        public string AccessToken { get; set; }
        public string TargetFolderId { get; set; }
    }

    public class RaumprogramExportResponse : BaseResponse
    {
        public string FileId { get; set; }
        public string FileName { get; set; }
    }
}

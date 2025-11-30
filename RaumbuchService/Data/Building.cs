using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaumbuchService.Data
{
    /// <summary>
    /// Building entity - represents a building in the multi-building management system.
    /// Maps to the Building table in Azure SQL.
    /// </summary>
    [Table("Building")]
    public class Building
    {
        /// <summary>
        /// Primary key, auto-incremented.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BuildingID { get; set; }

        /// <summary>
        /// Name of the building (e.g., "Hauptgebäude").
        /// </summary>
        [Required]
        [StringLength(255)]
        public string BuildingName { get; set; }

        /// <summary>
        /// Short code for the building (e.g., "HG01").
        /// </summary>
        [StringLength(100)]
        public string BuildingCode { get; set; }

        /// <summary>
        /// Description of the building.
        /// </summary>
        public string Description { get; set; }

        // ====================================================================
        // Address Fields
        // ====================================================================

        /// <summary>
        /// Street address.
        /// </summary>
        [StringLength(500)]
        public string AddressStreet { get; set; }

        /// <summary>
        /// City.
        /// </summary>
        [StringLength(200)]
        public string AddressCity { get; set; }

        /// <summary>
        /// Postal code.
        /// </summary>
        [StringLength(50)]
        public string AddressPostalCode { get; set; }

        /// <summary>
        /// Country.
        /// </summary>
        [StringLength(100)]
        public string AddressCountry { get; set; }

        // ====================================================================
        // Ownership Fields
        // ====================================================================

        /// <summary>
        /// Owner of the building.
        /// </summary>
        [StringLength(255)]
        public string Owner { get; set; }

        /// <summary>
        /// Creator of the building record.
        /// </summary>
        [StringLength(255)]
        public string Creator { get; set; }

        // ====================================================================
        // IFC Fields
        // ====================================================================

        /// <summary>
        /// IFC Project GUID.
        /// </summary>
        [StringLength(255)]
        public string IFCProjectGUID { get; set; }

        /// <summary>
        /// IFC Building GUID.
        /// </summary>
        [StringLength(255)]
        public string IFCBuildingGUID { get; set; }

        /// <summary>
        /// Whether IFC is enabled for this building.
        /// </summary>
        public bool IFCEnabled { get; set; }

        /// <summary>
        /// URL to the IFC file.
        /// </summary>
        [StringLength(500)]
        public string IFCFileUrl { get; set; }

        // ====================================================================
        // Coordinate Settings
        // ====================================================================

        /// <summary>
        /// Coordinate system (e.g., "LV95").
        /// </summary>
        [StringLength(200)]
        public string CoordinateSystem { get; set; }

        /// <summary>
        /// Local origin X coordinate.
        /// </summary>
        [Column(TypeName = "decimal")]
        public decimal? LocalOriginX { get; set; }

        /// <summary>
        /// Local origin Y coordinate.
        /// </summary>
        [Column(TypeName = "decimal")]
        public decimal? LocalOriginY { get; set; }

        /// <summary>
        /// Local origin Z coordinate.
        /// </summary>
        [Column(TypeName = "decimal")]
        public decimal? LocalOriginZ { get; set; }

        // ====================================================================
        // Logo
        // ====================================================================

        /// <summary>
        /// URL to the building logo.
        /// </summary>
        [StringLength(500)]
        public string LogoUrl { get; set; }

        // ====================================================================
        // Audit Fields
        // ====================================================================

        /// <summary>
        /// User ID who last modified this record.
        /// </summary>
        [StringLength(255)]
        public string ModifiedByUserID { get; set; }

        /// <summary>
        /// Date and time when this record was last modified.
        /// </summary>
        public DateTime? ModifiedDate { get; set; }

        /// <summary>
        /// Navigation property for the user who modified this record.
        /// </summary>
        [ForeignKey("ModifiedByUserID")]
        public virtual UserAccess ModifiedByUser { get; set; }

        // ====================================================================
        // Navigation Properties
        // ====================================================================

        /// <summary>
        /// Navigation property for room types in this building.
        /// </summary>
        public virtual ICollection<RoomType> RoomTypes { get; set; }

        /// <summary>
        /// Navigation property for rooms in this building.
        /// </summary>
        public virtual ICollection<Room> Rooms { get; set; }

        public Building()
        {
            RoomTypes = new HashSet<RoomType>();
            Rooms = new HashSet<Room>();
            IFCEnabled = false;
            CoordinateSystem = "LV95";
        }
    }
}

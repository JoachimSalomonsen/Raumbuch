using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaumbuchService.Data
{
    /// <summary>
    /// UserAccess entity - manages user access control.
    /// Maps to the UserAccess table in Azure SQL.
    /// </summary>
    [Table("UserAccess")]
    public class UserAccess
    {
        /// <summary>
        /// Primary key - Unique ID from Trimble Connect (e.g., email or system ID).
        /// </summary>
        [Key]
        [StringLength(255)]
        public string UserID { get; set; }

        /// <summary>
        /// Display name of the user.
        /// </summary>
        [StringLength(100)]
        public string UserName { get; set; }

        /// <summary>
        /// User role: 'Admin', 'Editor', 'Reader', 'NoAccess'.
        /// </summary>
        [StringLength(50)]
        public string Role { get; set; }
    }

    /// <summary>
    /// RoomType entity - represents a type/category of rooms.
    /// Maps to the RoomType table in Azure SQL.
    /// </summary>
    [Table("RoomType")]
    public class RoomType
    {
        /// <summary>
        /// Primary key, auto-incremented.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoomTypeID { get; set; }

        /// <summary>
        /// Name of the room type (e.g., "Büro", "Besprechungsraum").
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        /// <summary>
        /// Standardized category for every RoomType.
        /// </summary>
        [StringLength(100)]
        public string RoomCategory { get; set; }

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

        /// <summary>
        /// Navigation property for rooms of this type.
        /// </summary>
        public virtual ICollection<Room> Rooms { get; set; }

        public RoomType()
        {
            Rooms = new HashSet<Room>();
        }
    }

    /// <summary>
    /// Room entity - represents a planned or actual room.
    /// Maps to the Room table in Azure SQL.
    /// </summary>
    [Table("Room")]
    public class Room
    {
        /// <summary>
        /// Primary key, auto-incremented.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoomID { get; set; }

        /// <summary>
        /// Foreign key to RoomType.
        /// </summary>
        public int RoomTypeID { get; set; }

        /// <summary>
        /// Name of the room.
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        /// <summary>
        /// Planned area (SOLL) in square meters.
        /// </summary>
        [Column(TypeName = "decimal")]
        public decimal? AreaPlanned { get; set; }

        /// <summary>
        /// Actual area (IST) in square meters.
        /// </summary>
        [Column(TypeName = "decimal")]
        public decimal? AreaActual { get; set; }

        /// <summary>
        /// Net area in square meters.
        /// </summary>
        [Column(TypeName = "decimal")]
        public decimal? NetArea { get; set; }

        /// <summary>
        /// Gross area in square meters.
        /// </summary>
        [Column(TypeName = "decimal")]
        public decimal? GrossArea { get; set; }

        // ====================================================================
        // IFC Standard Properties (Pset_SpaceCommon and IfcSpace)
        // ====================================================================

        /// <summary>
        /// Pset_SpaceCommon.PubliclyAccessible [Boolean]
        /// Indicates whether the space is publicly accessible.
        /// </summary>
        public bool? PubliclyAccessible { get; set; }

        /// <summary>
        /// Pset_SpaceCommon.HandicapAccessible [Boolean]
        /// Indicates whether the space is handicap accessible.
        /// </summary>
        public bool? HandicapAccessible { get; set; }

        /// <summary>
        /// Pset_SpaceCommon.IsExternal [Boolean]
        /// Indicates whether the space is external or internal.
        /// </summary>
        public bool? IsExternal { get; set; }

        /// <summary>
        /// IfcSpace.Description [Text]
        /// A textual description of the space.
        /// </summary>
        [StringLength(500)]
        public string Description { get; set; }

        /// <summary>
        /// IfcSpace.ObjectType [Label]
        /// The type/class of the space object.
        /// </summary>
        [StringLength(100)]
        public string ObjectType { get; set; }

        /// <summary>
        /// IfcSpace.PredefinedType {NOTDEFINED; USERDEFINED; SPACE; PARKING; INTERNAL; EXTERNAL; BERTH; GFA}
        /// The predefined type of the space.
        /// </summary>
        [StringLength(50)]
        public string PredefinedType { get; set; }

        /// <summary>
        /// IfcSpace.ElevationWithFlooring [LengthMeasure]
        /// The elevation of the space with flooring in meters.
        /// </summary>
        [Column(TypeName = "decimal")]
        public decimal? ElevationWithFlooring { get; set; }

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

        /// <summary>
        /// Navigation property for room type.
        /// </summary>
        [ForeignKey("RoomTypeID")]
        public virtual RoomType RoomType { get; set; }

        /// <summary>
        /// Navigation property for room inventory items.
        /// </summary>
        public virtual ICollection<RoomInventory> RoomInventories { get; set; }

        public Room()
        {
            RoomInventories = new HashSet<RoomInventory>();
        }
    }

    /// <summary>
    /// InventoryTemplate entity - defines property names for room inventory.
    /// Maps to the InventoryTemplate table in Azure SQL.
    /// </summary>
    [Table("InventoryTemplate")]
    public class InventoryTemplate
    {
        /// <summary>
        /// Default DataType value for new inventory templates.
        /// </summary>
        public const string DefaultDataType = "Text";

        /// <summary>
        /// Primary key, auto-incremented.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InventoryTemplateID { get; set; }

        /// <summary>
        /// Name of the property (e.g., "Bodenbelag", "Beleuchtung").
        /// </summary>
        [Required]
        [StringLength(100)]
        public string PropertyName { get; set; }

        /// <summary>
        /// Data type for the property value.
        /// Allowed values: 'Text', 'Number', 'Boolean', 'Integer', 'Decimal'
        /// </summary>
        [StringLength(50)]
        public string DataType { get; set; }

        /// <summary>
        /// Unit of measurement for Number types (e.g., 'm²', 'kg', 'Stück').
        /// </summary>
        [StringLength(50)]
        public string Unit { get; set; }

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

        /// <summary>
        /// Navigation property for room inventory items using this template.
        /// </summary>
        public virtual ICollection<RoomInventory> RoomInventories { get; set; }

        public InventoryTemplate()
        {
            RoomInventories = new HashSet<RoomInventory>();
            DataType = DefaultDataType;
        }
    }

    /// <summary>
    /// RoomInventory entity - stores planned and actual values for room inventory items.
    /// Maps to the RoomInventory table in Azure SQL.
    /// </summary>
    [Table("RoomInventory")]
    public class RoomInventory
    {
        /// <summary>
        /// Primary key, auto-incremented.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoomInventoryID { get; set; }

        /// <summary>
        /// Foreign key to Room.
        /// </summary>
        public int RoomID { get; set; }

        /// <summary>
        /// Foreign key to InventoryTemplate.
        /// </summary>
        public int InventoryTemplateID { get; set; }

        /// <summary>
        /// Planned value (SOLL).
        /// </summary>
        [StringLength(255)]
        public string ValuePlanned { get; set; }

        /// <summary>
        /// Actual value (IST).
        /// </summary>
        [StringLength(255)]
        public string ValueActual { get; set; }

        /// <summary>
        /// Free-text commentary for specific inventory items.
        /// </summary>
        public string Comment { get; set; }

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

        /// <summary>
        /// Navigation property for room.
        /// </summary>
        [ForeignKey("RoomID")]
        public virtual Room Room { get; set; }

        /// <summary>
        /// Navigation property for inventory template.
        /// </summary>
        [ForeignKey("InventoryTemplateID")]
        public virtual InventoryTemplate InventoryTemplate { get; set; }
    }
}

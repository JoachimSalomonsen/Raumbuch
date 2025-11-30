using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaumbuchService.Data
{
    /// <summary>
    /// Entity class for analysis tolerance settings.
    /// Stores tolerance configuration per element type (NetArea, GrossArea, or specific Inventory item).
    /// </summary>
    [Table("AnalysisSettings")]
    public class AnalysisSettings
    {
        /// <summary>
        /// Primary key.
        /// </summary>
        [Key]
        public int AnalysisSettingsID { get; set; }

        /// <summary>
        /// Type of element: "NetArea", "GrossArea", or "Inventory".
        /// </summary>
        [Required]
        [StringLength(50)]
        public string SelectedElementType { get; set; }

        /// <summary>
        /// Foreign key to InventoryTemplate when element type is "Inventory".
        /// </summary>
        public int? SelectedInventoryTemplateID { get; set; }

        /// <summary>
        /// Navigation property to InventoryTemplate.
        /// </summary>
        [ForeignKey("SelectedInventoryTemplateID")]
        public virtual InventoryTemplate InventoryTemplate { get; set; }

        /// <summary>
        /// Minimum tolerance percentage (default -10%).
        /// </summary>
        public decimal ToleranceMin { get; set; } = -10.00m;

        /// <summary>
        /// Maximum tolerance percentage (default 10%).
        /// </summary>
        public decimal ToleranceMax { get; set; } = 10.00m;

        /// <summary>
        /// User who last modified this setting.
        /// </summary>
        [StringLength(255)]
        public string ModifiedByUserID { get; set; }

        /// <summary>
        /// Navigation property to the user who last modified.
        /// </summary>
        [ForeignKey("ModifiedByUserID")]
        public virtual UserAccess ModifiedByUser { get; set; }

        /// <summary>
        /// Date/time when this setting was last modified.
        /// </summary>
        public DateTime? ModifiedDate { get; set; }
    }
}

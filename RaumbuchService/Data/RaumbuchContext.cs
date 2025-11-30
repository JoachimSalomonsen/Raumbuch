using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace RaumbuchService.Data
{
    /// <summary>
    /// Entity Framework DbContext for Raumbuch Azure SQL Database.
    /// Connection string configured in Web.config as "RaumbuchContext".
    /// </summary>
    public class RaumbuchContext : DbContext
    {
        /// <summary>
        /// Static constructor to set the database initializer.
        /// We use null initializer because we manage the schema manually via CreateSchema.sql.
        /// </summary>
        static RaumbuchContext()
        {
            // Disable automatic database creation/migration - we use manual SQL scripts
            Database.SetInitializer<RaumbuchContext>(null);
        }

        /// <summary>
        /// Initializes a new instance of the RaumbuchContext.
        /// Uses the "RaumbuchContext" connection string from Web.config.
        /// </summary>
        public RaumbuchContext() : base("name=RaumbuchContext")
        {
            // Disable lazy loading and proxy creation for better API performance
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        /// <summary>
        /// User access control table.
        /// </summary>
        public DbSet<UserAccess> UserAccess { get; set; }

        /// <summary>
        /// Buildings for multi-building management.
        /// </summary>
        public DbSet<Building> Buildings { get; set; }

        /// <summary>
        /// Room types (Raumtyp) - the main categorization for rooms.
        /// </summary>
        public DbSet<RoomType> RoomTypes { get; set; }

        /// <summary>
        /// Rooms with planned and actual area values.
        /// </summary>
        public DbSet<Room> Rooms { get; set; }

        /// <summary>
        /// Inventory templates defining property names for room inventory.
        /// </summary>
        public DbSet<InventoryTemplate> InventoryTemplates { get; set; }

        /// <summary>
        /// Room inventory items with planned and actual values.
        /// </summary>
        public DbSet<RoomInventory> RoomInventories { get; set; }

        /// <summary>
        /// Analysis settings for tolerance configuration per element.
        /// </summary>
        public DbSet<AnalysisSettings> AnalysisSettings { get; set; }

        /// <summary>
        /// Saves changes and automatically populates audit fields.
        /// </summary>
        /// <param name="userId">The user ID performing the operation.</param>
        public async Task<int> SaveChangesWithAuditAsync(string userId)
        {
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
            {
                var entity = entry.Entity;

                // Set audit fields using reflection
                var modifiedByProperty = entity.GetType().GetProperty("ModifiedByUserID");
                var modifiedDateProperty = entity.GetType().GetProperty("ModifiedDate");

                if (modifiedByProperty != null && !string.IsNullOrEmpty(userId))
                {
                    modifiedByProperty.SetValue(entity, userId);
                }

                if (modifiedDateProperty != null)
                {
                    modifiedDateProperty.SetValue(entity, now);
                }
            }

            return await base.SaveChangesAsync();
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Disable pluralizing table names - use exact table names from [Table] attributes
            modelBuilder.Conventions.Remove<System.Data.Entity.ModelConfiguration.Conventions.PluralizingTableNameConvention>();

            // Explicitly map to table names (matches CreateSchema.sql exactly)
            modelBuilder.Entity<UserAccess>().ToTable("UserAccess");
            modelBuilder.Entity<Building>().ToTable("Building");
            modelBuilder.Entity<RoomType>().ToTable("RoomType");
            modelBuilder.Entity<Room>().ToTable("Room");
            modelBuilder.Entity<InventoryTemplate>().ToTable("InventoryTemplate");
            modelBuilder.Entity<RoomInventory>().ToTable("RoomInventory");

            // Configure UserAccess entity
            modelBuilder.Entity<UserAccess>()
                .HasKey(ua => ua.UserID);

            modelBuilder.Entity<UserAccess>()
                .Property(ua => ua.UserID)
                .IsRequired()
                .HasMaxLength(255);

            modelBuilder.Entity<UserAccess>()
                .Property(ua => ua.UserName)
                .HasMaxLength(100);

            modelBuilder.Entity<UserAccess>()
                .Property(ua => ua.Role)
                .HasMaxLength(50);

            // Configure Building entity
            modelBuilder.Entity<Building>()
                .HasKey(b => b.BuildingID);

            modelBuilder.Entity<Building>()
                .Property(b => b.BuildingName)
                .IsRequired()
                .HasMaxLength(255);

            modelBuilder.Entity<Building>()
                .Property(b => b.BuildingCode)
                .HasMaxLength(100);

            modelBuilder.Entity<Building>()
                .Property(b => b.LocalOriginX)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Building>()
                .Property(b => b.LocalOriginY)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Building>()
                .Property(b => b.LocalOriginZ)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Building>()
                .HasOptional(b => b.ModifiedByUser)
                .WithMany()
                .HasForeignKey(b => b.ModifiedByUserID);

            // Configure RoomType entity
            modelBuilder.Entity<RoomType>()
                .HasKey(rt => rt.RoomTypeID);

            modelBuilder.Entity<RoomType>()
                .Property(rt => rt.Name)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<RoomType>()
                .Property(rt => rt.RoomCategory)
                .HasMaxLength(100);

            modelBuilder.Entity<RoomType>()
                .Property(rt => rt.ModifiedByUserID)
                .HasMaxLength(255);

            modelBuilder.Entity<RoomType>()
                .HasOptional(rt => rt.ModifiedByUser)
                .WithMany()
                .HasForeignKey(rt => rt.ModifiedByUserID);

            modelBuilder.Entity<RoomType>()
                .HasOptional(rt => rt.Building)
                .WithMany(b => b.RoomTypes)
                .HasForeignKey(rt => rt.BuildingID);

            // Configure Room entity
            modelBuilder.Entity<Room>()
                .HasKey(r => r.RoomID);

            modelBuilder.Entity<Room>()
                .Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<Room>()
                .Property(r => r.NetAreaPlanned)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Room>()
                .Property(r => r.NetAreaActual)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Room>()
                .Property(r => r.GrossAreaPlanned)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Room>()
                .Property(r => r.GrossAreaActual)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Room>()
                .Property(r => r.ModifiedByUserID)
                .HasMaxLength(255);

            modelBuilder.Entity<Room>()
                .HasRequired(r => r.RoomType)
                .WithMany(rt => rt.Rooms)
                .HasForeignKey(r => r.RoomTypeID);

            modelBuilder.Entity<Room>()
                .HasOptional(r => r.ModifiedByUser)
                .WithMany()
                .HasForeignKey(r => r.ModifiedByUserID);

            modelBuilder.Entity<Room>()
                .HasOptional(r => r.Building)
                .WithMany(b => b.Rooms)
                .HasForeignKey(r => r.BuildingID);

            // Configure InventoryTemplate entity
            modelBuilder.Entity<InventoryTemplate>()
                .HasKey(it => it.InventoryTemplateID);

            modelBuilder.Entity<InventoryTemplate>()
                .Property(it => it.PropertyName)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<InventoryTemplate>()
                .Property(it => it.ModifiedByUserID)
                .HasMaxLength(255);

            modelBuilder.Entity<InventoryTemplate>()
                .HasOptional(it => it.ModifiedByUser)
                .WithMany()
                .HasForeignKey(it => it.ModifiedByUserID);

            // Configure RoomInventory entity
            modelBuilder.Entity<RoomInventory>()
                .HasKey(ri => ri.RoomInventoryID);

            modelBuilder.Entity<RoomInventory>()
                .Property(ri => ri.ValuePlanned)
                .HasMaxLength(255);

            modelBuilder.Entity<RoomInventory>()
                .Property(ri => ri.ValueActual)
                .HasMaxLength(255);

            modelBuilder.Entity<RoomInventory>()
                .Property(ri => ri.Comment)
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<RoomInventory>()
                .Property(ri => ri.ModifiedByUserID)
                .HasMaxLength(255);

            modelBuilder.Entity<RoomInventory>()
                .HasRequired(ri => ri.Room)
                .WithMany(r => r.RoomInventories)
                .HasForeignKey(ri => ri.RoomID);

            modelBuilder.Entity<RoomInventory>()
                .HasRequired(ri => ri.InventoryTemplate)
                .WithMany(it => it.RoomInventories)
                .HasForeignKey(ri => ri.InventoryTemplateID);

            modelBuilder.Entity<RoomInventory>()
                .HasOptional(ri => ri.ModifiedByUser)
                .WithMany()
                .HasForeignKey(ri => ri.ModifiedByUserID);

            // Configure AnalysisSettings entity
            modelBuilder.Entity<AnalysisSettings>().ToTable("AnalysisSettings");

            modelBuilder.Entity<AnalysisSettings>()
                .HasKey(a => a.AnalysisSettingsID);

            modelBuilder.Entity<AnalysisSettings>()
                .Property(a => a.SelectedElementType)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<AnalysisSettings>()
                .Property(a => a.ToleranceMin)
                .HasPrecision(10, 2);

            modelBuilder.Entity<AnalysisSettings>()
                .Property(a => a.ToleranceMax)
                .HasPrecision(10, 2);

            modelBuilder.Entity<AnalysisSettings>()
                .Property(a => a.ModifiedByUserID)
                .HasMaxLength(255);

            modelBuilder.Entity<AnalysisSettings>()
                .HasOptional(a => a.InventoryTemplate)
                .WithMany()
                .HasForeignKey(a => a.SelectedInventoryTemplateID);

            modelBuilder.Entity<AnalysisSettings>()
                .HasOptional(a => a.ModifiedByUser)
                .WithMany()
                .HasForeignKey(a => a.ModifiedByUserID);

            // Configure new Room deviation properties
            modelBuilder.Entity<Room>()
                .Property(r => r.NetAreaDeviationPercent)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Room>()
                .Property(r => r.NetAreaDeviationValue)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Room>()
                .Property(r => r.GrossAreaDeviationPercent)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Room>()
                .Property(r => r.GrossAreaDeviationValue)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Room>()
                .Property(r => r.NetAreaCommentIst)
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<Room>()
                .Property(r => r.GrossAreaCommentIst)
                .HasColumnType("nvarchar(max)");

            // Configure new RoomInventory deviation properties
            modelBuilder.Entity<RoomInventory>()
                .Property(ri => ri.InventoryDeviationPercent)
                .HasPrecision(10, 2);

            modelBuilder.Entity<RoomInventory>()
                .Property(ri => ri.InventoryDeviationValue)
                .HasPrecision(10, 2);

            modelBuilder.Entity<RoomInventory>()
                .Property(ri => ri.CommentIst)
                .HasColumnType("nvarchar(max)");
        }
    }
}

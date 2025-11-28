using System.Data.Entity;

namespace RaumbuchService.Data
{
    /// <summary>
    /// Entity Framework DbContext for Raumbuch Azure SQL Database.
    /// Connection string configured in Web.config as "RaumbuchContext".
    /// </summary>
    public class RaumbuchContext : DbContext
    {
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

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure RoomType entity
            modelBuilder.Entity<RoomType>()
                .HasKey(rt => rt.RoomTypeID);

            modelBuilder.Entity<RoomType>()
                .Property(rt => rt.Name)
                .IsRequired()
                .HasMaxLength(100);

            // Configure Room entity
            modelBuilder.Entity<Room>()
                .HasKey(r => r.RoomID);

            modelBuilder.Entity<Room>()
                .Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<Room>()
                .Property(r => r.AreaPlanned)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Room>()
                .Property(r => r.AreaActual)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Room>()
                .HasRequired(r => r.RoomType)
                .WithMany(rt => rt.Rooms)
                .HasForeignKey(r => r.RoomTypeID);

            // Configure InventoryTemplate entity
            modelBuilder.Entity<InventoryTemplate>()
                .HasKey(it => it.InventoryTemplateID);

            modelBuilder.Entity<InventoryTemplate>()
                .Property(it => it.PropertyName)
                .IsRequired()
                .HasMaxLength(100);

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
                .HasRequired(ri => ri.Room)
                .WithMany(r => r.RoomInventories)
                .HasForeignKey(ri => ri.RoomID);

            modelBuilder.Entity<RoomInventory>()
                .HasRequired(ri => ri.InventoryTemplate)
                .WithMany(it => it.RoomInventories)
                .HasForeignKey(ri => ri.InventoryTemplateID);
        }
    }
}

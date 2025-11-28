-- ================================================================
-- Azure SQL Database Schema for Raumbuch Raumprogramm (SOLL Data)
-- Run this script on the Raumbuch database in Azure SQL
-- Server: buildingpointch.database.windows.net
-- Database: Raumbuch
-- ================================================================

-- Create RoomType table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RoomType]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RoomType] (
        [RoomTypeID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(100) NOT NULL
    );
    PRINT 'Created table: RoomType';
END
GO

-- Create Room table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Room]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Room] (
        [RoomID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RoomTypeID] INT NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [AreaPlanned] DECIMAL(18,2) NULL,
        [AreaActual] DECIMAL(18,2) NULL,
        CONSTRAINT [FK_Room_RoomType] FOREIGN KEY ([RoomTypeID]) 
            REFERENCES [dbo].[RoomType] ([RoomTypeID])
    );
    PRINT 'Created table: Room';
END
GO

-- Create InventoryTemplate table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[InventoryTemplate]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[InventoryTemplate] (
        [InventoryTemplateID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PropertyName] NVARCHAR(100) NOT NULL
    );
    PRINT 'Created table: InventoryTemplate';
END
GO

-- Create RoomInventory table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RoomInventory]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RoomInventory] (
        [RoomInventoryID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RoomID] INT NOT NULL,
        [InventoryTemplateID] INT NOT NULL,
        [ValuePlanned] NVARCHAR(255) NULL,
        [ValueActual] NVARCHAR(255) NULL,
        CONSTRAINT [FK_RoomInventory_Room] FOREIGN KEY ([RoomID]) 
            REFERENCES [dbo].[Room] ([RoomID]),
        CONSTRAINT [FK_RoomInventory_InventoryTemplate] FOREIGN KEY ([InventoryTemplateID]) 
            REFERENCES [dbo].[InventoryTemplate] ([InventoryTemplateID])
    );
    PRINT 'Created table: RoomInventory';
END
GO

-- Create indexes for better query performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Room_RoomTypeID')
BEGIN
    CREATE INDEX [IX_Room_RoomTypeID] ON [dbo].[Room] ([RoomTypeID]);
    PRINT 'Created index: IX_Room_RoomTypeID';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RoomInventory_RoomID')
BEGIN
    CREATE INDEX [IX_RoomInventory_RoomID] ON [dbo].[RoomInventory] ([RoomID]);
    PRINT 'Created index: IX_RoomInventory_RoomID';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RoomInventory_InventoryTemplateID')
BEGIN
    CREATE INDEX [IX_RoomInventory_InventoryTemplateID] ON [dbo].[RoomInventory] ([InventoryTemplateID]);
    PRINT 'Created index: IX_RoomInventory_InventoryTemplateID';
END
GO

-- Insert sample data (optional - uncomment if needed)
/*
-- Sample Room Types
INSERT INTO [dbo].[RoomType] ([Name]) VALUES ('Büro');
INSERT INTO [dbo].[RoomType] ([Name]) VALUES ('Besprechungsraum');
INSERT INTO [dbo].[RoomType] ([Name]) VALUES ('Küche');
INSERT INTO [dbo].[RoomType] ([Name]) VALUES ('WC');
INSERT INTO [dbo].[RoomType] ([Name]) VALUES ('Flur');

-- Sample Inventory Templates
INSERT INTO [dbo].[InventoryTemplate] ([PropertyName]) VALUES ('Bodenbelag');
INSERT INTO [dbo].[InventoryTemplate] ([PropertyName]) VALUES ('Beleuchtung');
INSERT INTO [dbo].[InventoryTemplate] ([PropertyName]) VALUES ('Klimaanlage');
INSERT INTO [dbo].[InventoryTemplate] ([PropertyName]) VALUES ('Netzwerk');
INSERT INTO [dbo].[InventoryTemplate] ([PropertyName]) VALUES ('Möblierung');
*/

PRINT 'Database schema setup complete.';
GO

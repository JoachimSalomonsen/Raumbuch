-- ================================================================
-- Azure SQL Database Schema for Raumbuch Raumprogramm (SOLL Data)
-- Run this script on the Raumbuch database in Azure SQL
-- Server: buildingpointch.database.windows.net
-- Database: Raumbuch
-- ================================================================

-- Create UserAccess table (Access Control)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserAccess]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UserAccess] (
        [UserID] NVARCHAR(255) NOT NULL PRIMARY KEY,
        [UserName] NVARCHAR(100) NULL,
        [Role] NVARCHAR(50) NULL -- Must be: 'Admin', 'Editor', 'Reader', 'NoAccess'
    );
    PRINT 'Created table: UserAccess';
END
GO

-- Create RoomType table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RoomType]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RoomType] (
        [RoomTypeID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(100) NOT NULL,
        [RoomCategory] NVARCHAR(100) NULL,
        [ModifiedByUserID] NVARCHAR(255) NULL,
        [ModifiedDate] DATETIME2 NULL,
        CONSTRAINT [FK_RoomType_UserAccess] FOREIGN KEY ([ModifiedByUserID]) 
            REFERENCES [dbo].[UserAccess] ([UserID])
    );
    PRINT 'Created table: RoomType';
END
ELSE
BEGIN
    -- Add new columns if table exists but columns don't
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RoomType]') AND name = 'RoomCategory')
    BEGIN
        ALTER TABLE [dbo].[RoomType] ADD [RoomCategory] NVARCHAR(100) NULL;
        PRINT 'Added column: RoomType.RoomCategory';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RoomType]') AND name = 'ModifiedByUserID')
    BEGIN
        ALTER TABLE [dbo].[RoomType] ADD [ModifiedByUserID] NVARCHAR(255) NULL;
        PRINT 'Added column: RoomType.ModifiedByUserID';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RoomType]') AND name = 'ModifiedDate')
    BEGIN
        ALTER TABLE [dbo].[RoomType] ADD [ModifiedDate] DATETIME2 NULL;
        PRINT 'Added column: RoomType.ModifiedDate';
    END
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
        [ModifiedByUserID] NVARCHAR(255) NULL,
        [ModifiedDate] DATETIME2 NULL,
        CONSTRAINT [FK_Room_RoomType] FOREIGN KEY ([RoomTypeID]) 
            REFERENCES [dbo].[RoomType] ([RoomTypeID]),
        CONSTRAINT [FK_Room_UserAccess] FOREIGN KEY ([ModifiedByUserID]) 
            REFERENCES [dbo].[UserAccess] ([UserID])
    );
    PRINT 'Created table: Room';
END
ELSE
BEGIN
    -- Add new columns if table exists but columns don't
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Room]') AND name = 'ModifiedByUserID')
    BEGIN
        ALTER TABLE [dbo].[Room] ADD [ModifiedByUserID] NVARCHAR(255) NULL;
        PRINT 'Added column: Room.ModifiedByUserID';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Room]') AND name = 'ModifiedDate')
    BEGIN
        ALTER TABLE [dbo].[Room] ADD [ModifiedDate] DATETIME2 NULL;
        PRINT 'Added column: Room.ModifiedDate';
    END
END
GO

-- Create InventoryTemplate table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[InventoryTemplate]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[InventoryTemplate] (
        [InventoryTemplateID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PropertyName] NVARCHAR(100) NOT NULL,
        [ModifiedByUserID] NVARCHAR(255) NULL,
        [ModifiedDate] DATETIME2 NULL,
        CONSTRAINT [FK_InventoryTemplate_UserAccess] FOREIGN KEY ([ModifiedByUserID]) 
            REFERENCES [dbo].[UserAccess] ([UserID])
    );
    PRINT 'Created table: InventoryTemplate';
END
ELSE
BEGIN
    -- Add new columns if table exists but columns don't
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryTemplate]') AND name = 'ModifiedByUserID')
    BEGIN
        ALTER TABLE [dbo].[InventoryTemplate] ADD [ModifiedByUserID] NVARCHAR(255) NULL;
        PRINT 'Added column: InventoryTemplate.ModifiedByUserID';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryTemplate]') AND name = 'ModifiedDate')
    BEGIN
        ALTER TABLE [dbo].[InventoryTemplate] ADD [ModifiedDate] DATETIME2 NULL;
        PRINT 'Added column: InventoryTemplate.ModifiedDate';
    END
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
        [Comment] NVARCHAR(MAX) NULL,
        [ModifiedByUserID] NVARCHAR(255) NULL,
        [ModifiedDate] DATETIME2 NULL,
        CONSTRAINT [FK_RoomInventory_Room] FOREIGN KEY ([RoomID]) 
            REFERENCES [dbo].[Room] ([RoomID]),
        CONSTRAINT [FK_RoomInventory_InventoryTemplate] FOREIGN KEY ([InventoryTemplateID]) 
            REFERENCES [dbo].[InventoryTemplate] ([InventoryTemplateID]),
        CONSTRAINT [FK_RoomInventory_UserAccess] FOREIGN KEY ([ModifiedByUserID]) 
            REFERENCES [dbo].[UserAccess] ([UserID])
    );
    PRINT 'Created table: RoomInventory';
END
ELSE
BEGIN
    -- Add new columns if table exists but columns don't
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RoomInventory]') AND name = 'Comment')
    BEGIN
        ALTER TABLE [dbo].[RoomInventory] ADD [Comment] NVARCHAR(MAX) NULL;
        PRINT 'Added column: RoomInventory.Comment';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RoomInventory]') AND name = 'ModifiedByUserID')
    BEGIN
        ALTER TABLE [dbo].[RoomInventory] ADD [ModifiedByUserID] NVARCHAR(255) NULL;
        PRINT 'Added column: RoomInventory.ModifiedByUserID';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RoomInventory]') AND name = 'ModifiedDate')
    BEGIN
        ALTER TABLE [dbo].[RoomInventory] ADD [ModifiedDate] DATETIME2 NULL;
        PRINT 'Added column: RoomInventory.ModifiedDate';
    END
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
-- Sample User Access
INSERT INTO [dbo].[UserAccess] ([UserID], [UserName], [Role]) VALUES ('admin@example.com', 'Administrator', 'Admin');
INSERT INTO [dbo].[UserAccess] ([UserID], [UserName], [Role]) VALUES ('editor@example.com', 'Editor User', 'Editor');
INSERT INTO [dbo].[UserAccess] ([UserID], [UserName], [Role]) VALUES ('reader@example.com', 'Reader User', 'Reader');

-- Sample Room Types
INSERT INTO [dbo].[RoomType] ([Name], [RoomCategory]) VALUES ('Büro', 'Arbeitsfläche');
INSERT INTO [dbo].[RoomType] ([Name], [RoomCategory]) VALUES ('Besprechungsraum', 'Gemeinschaftsfläche');
INSERT INTO [dbo].[RoomType] ([Name], [RoomCategory]) VALUES ('Küche', 'Versorgungsfläche');
INSERT INTO [dbo].[RoomType] ([Name], [RoomCategory]) VALUES ('WC', 'Sanitärfläche');
INSERT INTO [dbo].[RoomType] ([Name], [RoomCategory]) VALUES ('Flur', 'Verkehrsfläche');

-- Sample Inventory Templates
INSERT INTO [dbo].[InventoryTemplate] ([PropertyName]) VALUES ('Bodenbelag');
INSERT INTO [dbo].[InventoryTemplate] ([PropertyName]) VALUES ('Beleuchtung');
INSERT INTO [dbo].[InventoryTemplate] ([PropertyName]) VALUES ('Klimaanlage');
INSERT INTO [dbo].[InventoryTemplate] ([PropertyName]) VALUES ('Netzwerk');
INSERT INTO [dbo].[InventoryTemplate] ([PropertyName]) VALUES ('Möblierung');
*/

PRINT 'Database schema setup complete.';
GO

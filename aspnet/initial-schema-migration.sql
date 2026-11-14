IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(80) NOT NULL,
        [GroupKey] varchar(30) NOT NULL DEFAULT 'flowers',
        [SortOrder] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DeliveryZones] (
        [Id] int NOT NULL IDENTITY,
        [Province] nvarchar(120) NOT NULL,
        [District] nvarchar(120) NOT NULL,
        [Fee] decimal(18,0) NOT NULL,
        [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
        [UpdatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_DeliveryZones] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DeliveryZones_Fee] CHECK ([Fee] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[RateLimitCounters] (
        [Scope] varchar(30) NOT NULL,
        [KeyHash] varbinary(32) NOT NULL,
        [Count] int NOT NULL,
        [LockedUntil] datetime2(3) NULL,
        [ExpiresAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_RateLimitCounters] PRIMARY KEY ([Scope], [KeyHash]),
        CONSTRAINT [CK_RateLimitCounters_Count] CHECK ([Count] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Suppliers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Phone] varchar(20) NULL,
        [Address] nvarchar(300) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id] int NOT NULL IDENTITY,
        [FirebaseUid] nvarchar(128) NOT NULL,
        [Email] nvarchar(256) NULL,
        [DisplayName] nvarchar(120) NULL,
        [Phone] varchar(20) NULL,
        [Role] varchar(20) NOT NULL DEFAULT 'customer',
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Users_Role] CHECK ([Role] IN ('customer', 'admin', 'owner'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[WrappingFeeTiers] (
        [Id] int NOT NULL IDENTITY,
        [MaxStems] int NOT NULL,
        [Fee] decimal(18,0) NOT NULL,
        CONSTRAINT [PK_WrappingFeeTiers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_WrappingFeeTiers_Fee] CHECK ([Fee] >= 0),
        CONSTRAINT [CK_WrappingFeeTiers_Max] CHECK ([MaxStems] BETWEEN 1 AND 50)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [ActorUserId] int NULL,
        [Action] varchar(60) NOT NULL,
        [EntityType] varchar(40) NOT NULL,
        [EntityId] nvarchar(64) NOT NULL,
        [OldValue] nvarchar(max) NULL,
        [NewValue] nvarchar(max) NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AuditLogs_NewJson] CHECK ([NewValue] IS NULL OR ISJSON([NewValue]) = 1),
        CONSTRAINT [CK_AuditLogs_OldJson] CHECK ([OldValue] IS NULL OR ISJSON([OldValue]) = 1),
        CONSTRAINT [FK_AuditLogs_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Promotions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [DiscountPercent] decimal(5,2) NOT NULL,
        [StartDate] date NULL,
        [EndDate] date NULL,
        [AppliesToAll] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedBy] int NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Promotions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Promotions_Dates] CHECK ([StartDate] IS NULL OR [EndDate] IS NULL OR [StartDate] <= [EndDate]),
        CONSTRAINT [CK_Promotions_Percent] CHECK ([DiscountPercent] > 0 AND [DiscountPercent] <= 90),
        CONSTRAINT [FK_Promotions_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[StockReceipts] (
        [Id] int NOT NULL IDENTITY,
        [SupplierId] int NULL,
        [ReceivedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] int NOT NULL,
        [Note] nvarchar(500) NULL,
        CONSTRAINT [PK_StockReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockReceipts_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers] ([Id]),
        CONSTRAINT [FK_StockReceipts_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Orders] (
        [Id] bigint NOT NULL IDENTITY,
        [OrderCode] varchar(32) NOT NULL,
        [PaymentReference] varchar(40) NOT NULL,
        [IdempotencyKey] varchar(80) NOT NULL,
        [RequestHash] varchar(64) NOT NULL,
        [UserId] int NULL,
        [OrderType] varchar(20) NOT NULL,
        [CustomerName] nvarchar(120) NOT NULL,
        [Phone] varchar(20) NOT NULL,
        [Address] nvarchar(300) NOT NULL,
        [RecipientName] nvarchar(120) NOT NULL,
        [RecipientPhone] varchar(20) NOT NULL,
        [CustomerIsRecipient] bit NOT NULL,
        [DeliveryZoneId] int NOT NULL,
        [DeliveryProvince] nvarchar(120) NOT NULL,
        [DeliveryDistrict] nvarchar(120) NOT NULL,
        [DeliveryDate] date NOT NULL,
        [DeliveryTime] nvarchar(40) NOT NULL,
        [GreetingCard] nvarchar(300) NULL,
        [AnonymousGreetingCard] bit NOT NULL,
        [GiftWrap] bit NOT NULL,
        [OrderNote] nvarchar(500) NULL,
        [PromotionId] int NULL,
        [PromotionName] nvarchar(120) NULL,
        [DiscountPercent] decimal(5,2) NULL,
        [Subtotal] decimal(18,0) NOT NULL,
        [Savings] decimal(18,0) NOT NULL DEFAULT 0.0,
        [WrappingFee] decimal(18,0) NOT NULL DEFAULT 0.0,
        [ShippingFee] decimal(18,0) NOT NULL DEFAULT 0.0,
        [Tax] decimal(18,0) NOT NULL DEFAULT 0.0,
        [Total] decimal(18,0) NOT NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'pending',
        [CurrentLocation] nvarchar(200) NULL,
        [EstimatedDelivery] nvarchar(100) NULL,
        [PaymentMethod] varchar(10) NOT NULL,
        [PaymentStatus] varchar(20) NOT NULL,
        [InventoryState] varchar(20) NOT NULL DEFAULT 'reserved',
        [ReservationExpiresAt] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CancelledAt] datetime2(3) NULL,
        [RowVer] rowversion NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Orders_Inventory] CHECK ([InventoryState] IN ('reserved', 'committed', 'released')),
        CONSTRAINT [CK_Orders_Money] CHECK ([Subtotal] >= 0 AND [Savings] >= 0 AND [Savings] <= [Subtotal] AND [WrappingFee] >= 0 AND [ShippingFee] >= 0 AND [Tax] >= 0),
        CONSTRAINT [CK_Orders_PayMethod] CHECK ([PaymentMethod] IN ('cod', 'qr', 'vnpay')),
        CONSTRAINT [CK_Orders_PayStatus] CHECK ([PaymentStatus] IN ('cod_pending', 'awaiting', 'paid', 'failed', 'refunded', 'partially_refunded')),
        CONSTRAINT [CK_Orders_Status] CHECK ([Status] IN ('pending', 'preparing', 'shipping', 'delivered', 'cancelled', 'expired')),
        CONSTRAINT [CK_Orders_Total] CHECK ([Total] = [Subtotal] - [Savings] + [WrappingFee] + [ShippingFee] + [Tax]),
        CONSTRAINT [CK_Orders_Type] CHECK ([OrderType] IN ('ready_made', 'custom_bouquet')),
        CONSTRAINT [FK_Orders_DeliveryZones_DeliveryZoneId] FOREIGN KEY ([DeliveryZoneId]) REFERENCES [dbo].[DeliveryZones] ([Id]),
        CONSTRAINT [FK_Orders_Promotions_PromotionId] FOREIGN KEY ([PromotionId]) REFERENCES [dbo].[Promotions] ([Id]),
        CONSTRAINT [FK_Orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[CustomerMessages] (
        [Id] bigint NOT NULL IDENTITY,
        [OrderId] bigint NULL,
        [CustomerName] nvarchar(120) NOT NULL,
        [Content] nvarchar(1000) NOT NULL,
        [Reply] nvarchar(1000) NULL,
        [CompletedAt] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_CustomerMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerMessages_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DesignRequests] (
        [Id] bigint NOT NULL IDENTITY,
        [RequestCode] varchar(32) NOT NULL,
        [AccessTokenHash] varbinary(32) NOT NULL,
        [UserId] int NULL,
        [CustomerName] nvarchar(120) NOT NULL,
        [Phone] varchar(20) NOT NULL,
        [DeliveryZoneId] int NULL,
        [DeliveryProvince] nvarchar(120) NULL,
        [DeliveryDistrict] nvarchar(120) NULL,
        [DeliveryDate] date NULL,
        [Design] nvarchar(max) NOT NULL,
        [SpecialRequest] nvarchar(1500) NULL,
        [EstimatedTotal] decimal(18,0) NOT NULL DEFAULT 0.0,
        [QuotedTotal] decimal(18,0) NULL,
        [QuoteNote] nvarchar(1000) NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'consulting',
        [ConvertedOrderId] bigint NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_DesignRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DesignRequests_Design] CHECK (ISJSON([Design]) = 1),
        CONSTRAINT [CK_DesignRequests_Money] CHECK ([EstimatedTotal] >= 0 AND ([QuotedTotal] IS NULL OR [QuotedTotal] >= 0)),
        CONSTRAINT [CK_DesignRequests_Status] CHECK ([Status] IN ('consulting', 'quoting', 'awaiting_customer', 'in_progress', 'completed', 'cancelled')),
        CONSTRAINT [FK_DesignRequests_DeliveryZones_DeliveryZoneId] FOREIGN KEY ([DeliveryZoneId]) REFERENCES [dbo].[DeliveryZones] ([Id]),
        CONSTRAINT [FK_DesignRequests_Orders_ConvertedOrderId] FOREIGN KEY ([ConvertedOrderId]) REFERENCES [dbo].[Orders] ([Id]),
        CONSTRAINT [FK_DesignRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[OrderCustomBouquets] (
        [Id] bigint NOT NULL IDENTITY,
        [OrderId] bigint NOT NULL,
        [BouquetKey] varchar(64) NOT NULL,
        [StemCount] int NOT NULL,
        [WrappingFee] decimal(18,0) NOT NULL,
        CONSTRAINT [PK_OrderCustomBouquets] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OrderCustomBouquets_Fee] CHECK ([WrappingFee] >= 0),
        CONSTRAINT [CK_OrderCustomBouquets_Stems] CHECK ([StemCount] BETWEEN 1 AND 50),
        CONSTRAINT [FK_OrderCustomBouquets_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[OrderStatusHistory] (
        [Id] bigint NOT NULL IDENTITY,
        [OrderId] bigint NOT NULL,
        [Status] varchar(20) NOT NULL,
        [Location] nvarchar(200) NULL,
        [EstimatedDelivery] nvarchar(100) NULL,
        [ChangedBy] int NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_OrderStatusHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderStatusHistory_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderStatusHistory_Users_ChangedBy] FOREIGN KEY ([ChangedBy]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Payments] (
        [Id] bigint NOT NULL IDENTITY,
        [OrderId] bigint NOT NULL,
        [Provider] varchar(10) NOT NULL,
        [TxnRef] varchar(64) NOT NULL,
        [Attempt] int NOT NULL,
        [Amount] decimal(18,0) NOT NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'pending',
        [ProviderTransactionNo] varchar(64) NULL,
        [BankCode] varchar(20) NULL,
        [ResponseCode] varchar(10) NULL,
        [RawResponse] nvarchar(max) NULL,
        [ExpiresAt] datetime2(3) NULL,
        [PaidAt] datetime2(3) NULL,
        [VerifiedBy] int NULL,
        [ReconciliationReference] nvarchar(120) NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_Payments_Provider] CHECK ([Provider] IN ('cod', 'qr', 'vnpay')),
        CONSTRAINT [CK_Payments_Raw] CHECK ([RawResponse] IS NULL OR ISJSON([RawResponse]) = 1),
        CONSTRAINT [CK_Payments_Status] CHECK ([Status] IN ('pending', 'paid', 'failed', 'expired', 'cancelled')),
        CONSTRAINT [FK_Payments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]),
        CONSTRAINT [FK_Payments_Users_VerifiedBy] FOREIGN KEY ([VerifiedBy]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Reviews] (
        [Id] bigint NOT NULL IDENTITY,
        [AuthorUserId] int NOT NULL,
        [OrderId] bigint NULL,
        [CustomerName] nvarchar(120) NOT NULL,
        [Rating] tinyint NOT NULL,
        [Comment] nvarchar(1000) NOT NULL,
        [ModerationStatus] varchar(10) NOT NULL DEFAULT 'pending',
        [ModerationReason] nvarchar(500) NULL,
        [ModeratedBy] int NULL,
        [ModeratedAt] datetime2(3) NULL,
        [Reply] nvarchar(1000) NULL,
        [CompletedAt] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Reviews_Moderation] CHECK ([ModerationStatus] IN ('pending', 'approved', 'hidden')),
        CONSTRAINT [CK_Reviews_Rating] CHECK ([Rating] BETWEEN 1 AND 5),
        CONSTRAINT [FK_Reviews_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]),
        CONSTRAINT [FK_Reviews_Users_AuthorUserId] FOREIGN KEY ([AuthorUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_Reviews_Users_ModeratedBy] FOREIGN KEY ([ModeratedBy]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DesignRequestHistory] (
        [Id] bigint NOT NULL IDENTITY,
        [RequestId] bigint NOT NULL,
        [Status] varchar(20) NOT NULL,
        [Note] nvarchar(1000) NULL,
        [ChangedBy] int NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_DesignRequestHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DesignRequestHistory_DesignRequests_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [dbo].[DesignRequests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DesignRequestHistory_Users_ChangedBy] FOREIGN KEY ([ChangedBy]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Refunds] (
        [Id] bigint NOT NULL IDENTITY,
        [PaymentId] bigint NOT NULL,
        [Amount] decimal(18,0) NOT NULL,
        [Reference] nvarchar(120) NOT NULL,
        [Reason] nvarchar(300) NULL,
        [RefundedBy] int NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Refunds] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Refunds_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [FK_Refunds_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[Payments] ([Id]),
        CONSTRAINT [FK_Refunds_Users_RefundedBy] FOREIGN KEY ([RefundedBy]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ReviewImages] (
        [Id] bigint NOT NULL IDENTITY,
        [ReviewId] bigint NOT NULL,
        [Data] varbinary(max) NOT NULL,
        [ContentType] varchar(40) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ReviewImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewImages_Reviews_ReviewId] FOREIGN KEY ([ReviewId]) REFERENCES [dbo].[Reviews] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[CartItems] (
        [UserId] int NOT NULL,
        [LineKey] varchar(100) NOT NULL,
        [ProductId] int NOT NULL,
        [Quantity] int NOT NULL,
        [Options] nvarchar(1000) NULL,
        [UpdatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_CartItems] PRIMARY KEY ([UserId], [LineKey]),
        CONSTRAINT [CK_CartItems_Options] CHECK ([Options] IS NULL OR ISJSON([Options]) = 1),
        CONSTRAINT [CK_CartItems_Qty] CHECK ([Quantity] BETWEEN 1 AND 999),
        CONSTRAINT [FK_CartItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[DesignRequestStems] (
        [RequestId] bigint NOT NULL,
        [ProductId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,0) NOT NULL,
        CONSTRAINT [PK_DesignRequestStems] PRIMARY KEY ([RequestId], [ProductId]),
        CONSTRAINT [CK_DesignRequestStems_Qty] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_DesignRequestStems_DesignRequests_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [dbo].[DesignRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[InventoryMovements] (
        [Id] bigint NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [QuantityChange] int NOT NULL,
        [StockAfter] int NOT NULL,
        [MovementType] varchar(20) NOT NULL,
        [OrderId] bigint NULL,
        [ReceiptItemId] int NULL,
        [Note] nvarchar(300) NULL,
        [CreatedBy] int NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_InventoryMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InventoryMovements_After] CHECK ([StockAfter] >= 0),
        CONSTRAINT [CK_InventoryMovements_Link] CHECK (([MovementType] IN ('reserve', 'release', 'return') AND [OrderId] IS NOT NULL) OR ([MovementType] = 'import' AND [ReceiptItemId] IS NOT NULL) OR [MovementType] = 'adjust'),
        CONSTRAINT [CK_InventoryMovements_Qty] CHECK ([QuantityChange] <> 0),
        CONSTRAINT [CK_InventoryMovements_Type] CHECK ([MovementType] IN ('import', 'reserve', 'release', 'adjust', 'return')),
        CONSTRAINT [FK_InventoryMovements_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]),
        CONSTRAINT [FK_InventoryMovements_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[OrderItems] (
        [Id] bigint NOT NULL IDENTITY,
        [OrderId] bigint NOT NULL,
        [ProductId] int NOT NULL,
        [ProductPriceId] bigint NULL,
        [CustomBouquetId] bigint NULL,
        [ProductCode] varchar(64) NOT NULL,
        [ProductName] nvarchar(120) NOT NULL,
        [ImageUrl] nvarchar(1000) NULL,
        [SaleType] varchar(20) NOT NULL,
        [Unit] nvarchar(10) NOT NULL,
        [UnitPrice] decimal(18,0) NOT NULL,
        [Quantity] int NOT NULL,
        [DiscountAmount] decimal(18,0) NOT NULL DEFAULT 0.0,
        [LineTotal] AS ([UnitPrice] * [Quantity] - [DiscountAmount]) PERSISTED,
        [Size] nvarchar(40) NULL,
        [Color] nvarchar(40) NULL,
        [CardMessage] nvarchar(300) NULL,
        [PersonalNote] nvarchar(300) NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OrderItems_Discount] CHECK ([DiscountAmount] >= 0 AND [DiscountAmount] <= [UnitPrice] * [Quantity]),
        CONSTRAINT [CK_OrderItems_Price] CHECK ([UnitPrice] > 0),
        CONSTRAINT [CK_OrderItems_Qty] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_OrderItems_OrderCustomBouquets_CustomBouquetId] FOREIGN KEY ([CustomBouquetId]) REFERENCES [dbo].[OrderCustomBouquets] ([Id]),
        CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ProductImages] (
        [Id] bigint NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [Url] nvarchar(1000) NULL,
        [Data] varbinary(max) NULL,
        [ContentType] varchar(40) NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        [UploadedBy] int NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ProductImages_Source] CHECK (([Url] IS NOT NULL AND [Data] IS NULL) OR ([Url] IS NULL AND [Data] IS NOT NULL AND [ContentType] IS NOT NULL)),
        CONSTRAINT [FK_ProductImages_Users_UploadedBy] FOREIGN KEY ([UploadedBy]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ProductPrices] (
        [Id] bigint NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [Price] decimal(18,0) NOT NULL,
        [PreviousPrice] decimal(18,0) NULL,
        [EffectiveFrom] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [ChangedBy] int NULL,
        [Reason] nvarchar(300) NULL,
        CONSTRAINT [PK_ProductPrices] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ProductPrices_Price] CHECK ([Price] > 0),
        CONSTRAINT [FK_ProductPrices_Users_ChangedBy] FOREIGN KEY ([ChangedBy]) REFERENCES [dbo].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[Products] (
        [Id] int NOT NULL IDENTITY,
        [Code] varchar(64) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Meaning] nvarchar(500) NULL,
        [CategoryId] int NULL,
        [SaleType] varchar(20) NOT NULL,
        [Unit] nvarchar(10) NOT NULL,
        [CurrentPrice] decimal(18,0) NOT NULL,
        [CurrentPriceId] bigint NULL,
        [Stock] int NOT NULL DEFAULT 0,
        [SameDayDelivery] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Status] varchar(20) NOT NULL DEFAULT 'active',
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [RowVer] rowversion NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Products_Price] CHECK ([CurrentPrice] > 0),
        CONSTRAINT [CK_Products_SaleType] CHECK ([SaleType] IN ('ready_made', 'flower')),
        CONSTRAINT [CK_Products_Status] CHECK ([Status] IN ('active', 'hidden', 'archived')),
        CONSTRAINT [CK_Products_Stock] CHECK ([Stock] >= 0),
        CONSTRAINT [CK_Products_Unit] CHECK (([SaleType] = 'ready_made' AND [Unit] = N'bó') OR ([SaleType] = 'flower' AND [Unit] = N'cành')),
        CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories] ([Id]),
        CONSTRAINT [FK_Products_CurrentPrice] FOREIGN KEY ([CurrentPriceId]) REFERENCES [dbo].[ProductPrices] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ProductTags] (
        [ProductId] int NOT NULL,
        [TagType] varchar(20) NOT NULL,
        [Value] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_ProductTags] PRIMARY KEY ([ProductId], [TagType], [Value]),
        CONSTRAINT [CK_ProductTags_Type] CHECK ([TagType] IN ('color', 'occasion')),
        CONSTRAINT [FK_ProductTags_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[PromotionProducts] (
        [PromotionId] int NOT NULL,
        [ProductId] int NOT NULL,
        CONSTRAINT [PK_PromotionProducts] PRIMARY KEY ([PromotionId], [ProductId]),
        CONSTRAINT [FK_PromotionProducts_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),
        CONSTRAINT [FK_PromotionProducts_Promotions_PromotionId] FOREIGN KEY ([PromotionId]) REFERENCES [dbo].[Promotions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[ReviewProducts] (
        [ReviewId] bigint NOT NULL,
        [ProductId] int NOT NULL,
        CONSTRAINT [PK_ReviewProducts] PRIMARY KEY ([ReviewId], [ProductId]),
        CONSTRAINT [FK_ReviewProducts_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),
        CONSTRAINT [FK_ReviewProducts_Reviews_ReviewId] FOREIGN KEY ([ReviewId]) REFERENCES [dbo].[Reviews] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[StockReceiptItems] (
        [Id] int NOT NULL IDENTITY,
        [ReceiptId] int NOT NULL,
        [ProductId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitCost] decimal(18,0) NOT NULL,
        CONSTRAINT [PK_StockReceiptItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockReceiptItems_Cost] CHECK ([UnitCost] >= 0),
        CONSTRAINT [CK_StockReceiptItems_Qty] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_StockReceiptItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),
        CONSTRAINT [FK_StockReceiptItems_StockReceipts_ReceiptId] FOREIGN KEY ([ReceiptId]) REFERENCES [dbo].[StockReceipts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TABLE [dbo].[WishlistItems] (
        [UserId] int NOT NULL,
        [ProductId] int NOT NULL,
        [AddedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_WishlistItems] PRIMARY KEY ([UserId], [ProductId]),
        CONSTRAINT [FK_WishlistItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]),
        CONSTRAINT [FK_WishlistItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ActorUserId] ON [dbo].[AuditLogs] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Entity] ON [dbo].[AuditLogs] ([EntityType], [EntityId], [CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CartItems_ProductId] ON [dbo].[CartItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Categories_Name] ON [dbo].[Categories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CustomerMessages_OrderId] ON [dbo].[CustomerMessages] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_DeliveryZones_Area] ON [dbo].[DeliveryZones] ([Province], [District]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DesignRequestHistory_ChangedBy] ON [dbo].[DesignRequestHistory] ([ChangedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DesignRequestHistory_RequestId] ON [dbo].[DesignRequestHistory] ([RequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DesignRequests_ConvertedOrderId] ON [dbo].[DesignRequests] ([ConvertedOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DesignRequests_DeliveryZoneId] ON [dbo].[DesignRequests] ([DeliveryZoneId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DesignRequests_Status] ON [dbo].[DesignRequests] ([Status], [CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DesignRequests_UserId] ON [dbo].[DesignRequests] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_DesignRequests_Code] ON [dbo].[DesignRequests] ([RequestCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_DesignRequestStems_ProductId] ON [dbo].[DesignRequestStems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_CreatedBy] ON [dbo].[InventoryMovements] ([CreatedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_InventoryMovements_Order] ON [dbo].[InventoryMovements] ([OrderId]) WHERE [OrderId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_Product] ON [dbo].[InventoryMovements] ([ProductId], [CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_ReceiptItemId] ON [dbo].[InventoryMovements] ([ReceiptItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_OrderCustomBouquets] ON [dbo].[OrderCustomBouquets] ([OrderId], [BouquetKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItems_CustomBouquetId] ON [dbo].[OrderItems] ([CustomBouquetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItems_Order] ON [dbo].[OrderItems] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItems_Product] ON [dbo].[OrderItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItems_ProductPriceId] ON [dbo].[OrderItems] ([ProductPriceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_DeliveryZoneId] ON [dbo].[Orders] ([DeliveryZoneId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_Phone] ON [dbo].[Orders] ([Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_PromotionId] ON [dbo].[Orders] ([PromotionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Orders_Reservation] ON [dbo].[Orders] ([ReservationExpiresAt]) WHERE [InventoryState] = ''reserved''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_Status] ON [dbo].[Orders] ([Status], [CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Orders_User] ON [dbo].[Orders] ([UserId], [CreatedAt] DESC) WHERE [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Orders_Code] ON [dbo].[Orders] ([OrderCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Orders_Idempotency] ON [dbo].[Orders] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Orders_PaymentRef] ON [dbo].[Orders] ([PaymentReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistory_ChangedBy] ON [dbo].[OrderStatusHistory] ([ChangedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistory_Order] ON [dbo].[OrderStatusHistory] ([OrderId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Payments_VerifiedBy] ON [dbo].[Payments] ([VerifiedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Payments_Attempt] ON [dbo].[Payments] ([OrderId], [Attempt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Payments_TxnRef] ON [dbo].[Payments] ([TxnRef]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Payments_OnePaid] ON [dbo].[Payments] ([OrderId]) WHERE [Status] = ''paid''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Payments_ProviderTxn] ON [dbo].[Payments] ([Provider], [ProviderTransactionNo]) WHERE [ProviderTransactionNo] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductImages_Product] ON [dbo].[ProductImages] ([ProductId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductImages_UploadedBy] ON [dbo].[ProductImages] ([UploadedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductPrices_ChangedBy] ON [dbo].[ProductPrices] ([ChangedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductPrices_Product] ON [dbo].[ProductPrices] ([ProductId], [EffectiveFrom] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Products_Category] ON [dbo].[Products] ([CategoryId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Products_CurrentPriceId] ON [dbo].[Products] ([CurrentPriceId]) WHERE [CurrentPriceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Products_SaleType] ON [dbo].[Products] ([SaleType], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Products_Code] ON [dbo].[Products] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PromotionProducts_ProductId] ON [dbo].[PromotionProducts] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Promotions_CreatedBy] ON [dbo].[Promotions] ([CreatedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_RateLimitCounters_Expires] ON [dbo].[RateLimitCounters] ([ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Refunds_PaymentId] ON [dbo].[Refunds] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Refunds_RefundedBy] ON [dbo].[Refunds] ([RefundedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReviewImages_ReviewId] ON [dbo].[ReviewImages] ([ReviewId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReviewProducts_Product] ON [dbo].[ReviewProducts] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reviews_AuthorUserId] ON [dbo].[Reviews] ([AuthorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reviews_ModeratedBy] ON [dbo].[Reviews] ([ModeratedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Reviews_OrderAuthor] ON [dbo].[Reviews] ([OrderId], [AuthorUserId]) WHERE [OrderId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StockReceiptItems_ProductId] ON [dbo].[StockReceiptItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StockReceiptItems_ReceiptId] ON [dbo].[StockReceiptItems] ([ReceiptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StockReceipts_CreatedBy] ON [dbo].[StockReceipts] ([CreatedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StockReceipts_SupplierId] ON [dbo].[StockReceipts] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Users_Email] ON [dbo].[Users] ([Email]) WHERE [Email] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Users_FirebaseUid] ON [dbo].[Users] ([FirebaseUid]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_WishlistItems_ProductId] ON [dbo].[WishlistItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_WrappingFeeTiers_Max] ON [dbo].[WrappingFeeTiers] ([MaxStems]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    ALTER TABLE [dbo].[CartItems] ADD CONSTRAINT [FK_CartItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    ALTER TABLE [dbo].[DesignRequestStems] ADD CONSTRAINT [FK_DesignRequestStems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    ALTER TABLE [dbo].[InventoryMovements] ADD CONSTRAINT [FK_InventoryMovements_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    ALTER TABLE [dbo].[InventoryMovements] ADD CONSTRAINT [FK_InventoryMovements_StockReceiptItems_ReceiptItemId] FOREIGN KEY ([ReceiptItemId]) REFERENCES [dbo].[StockReceiptItems] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    ALTER TABLE [dbo].[OrderItems] ADD CONSTRAINT [FK_OrderItems_ProductPrices_ProductPriceId] FOREIGN KEY ([ProductPriceId]) REFERENCES [dbo].[ProductPrices] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    ALTER TABLE [dbo].[OrderItems] ADD CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    ALTER TABLE [dbo].[ProductImages] ADD CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    ALTER TABLE [dbo].[ProductPrices] ADD CONSTRAINT [FK_ProductPrices_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TRIGGER dbo.TR_ProductPrices_AppendOnly ON dbo.ProductPrices
    AFTER UPDATE, DELETE AS
    BEGIN
        SET NOCOUNT ON;
        THROW 50001, 'Price history is append-only.', 1;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE PROCEDURE dbo.usp_SetProductPrice
        @ProductId INT, @NewPrice DECIMAL(18,0), @ChangedBy INT, @Reason NVARCHAR(300) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;
        DECLARE @OldPrice DECIMAL(18,0);
        SELECT @OldPrice = CurrentPrice FROM dbo.Products WITH (UPDLOCK, ROWLOCK) WHERE Id = @ProductId;
        IF @OldPrice IS NULL THROW 50002, 'Product was not found.', 1;
        INSERT INTO dbo.ProductPrices (ProductId, Price, PreviousPrice, ChangedBy, Reason)
        VALUES (@ProductId, @NewPrice, @OldPrice, @ChangedBy, @Reason);
        DECLARE @PriceId BIGINT = SCOPE_IDENTITY();
        UPDATE dbo.Products SET CurrentPrice = @NewPrice, CurrentPriceId = @PriceId, UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @ProductId;
        INSERT INTO dbo.AuditLogs (ActorUserId, Action, EntityType, EntityId, OldValue, NewValue)
        VALUES (@ChangedBy, 'product.price.update', 'Product', CAST(@ProductId AS NVARCHAR(64)),
            CONCAT('{"price":', @OldPrice, '}'), CONCAT('{"price":', @NewPrice, '}'));
        COMMIT TRANSACTION;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TRIGGER dbo.TR_OrderItems_LockPrice ON dbo.OrderItems
    AFTER UPDATE AS
    BEGIN
        SET NOCOUNT ON;
        IF UPDATE(UnitPrice) OR UPDATE(Quantity) OR UPDATE(DiscountAmount) OR UPDATE(ProductId) OR UPDATE(ProductPriceId)
            THROW 50003, 'Order item price and product fields are immutable.', 1;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE PROCEDURE dbo.usp_ReserveStock @OrderId BIGINT, @ProductId INT, @Quantity INT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        IF @Quantity <= 0 THROW 50004, 'Quantity must be greater than zero.', 1;
        DECLARE @After TABLE (Stock INT);
        UPDATE dbo.Products SET Stock = Stock - @Quantity, UpdatedAt = SYSUTCDATETIME()
        OUTPUT inserted.Stock INTO @After
        WHERE Id = @ProductId AND Stock >= @Quantity;
        IF NOT EXISTS (SELECT 1 FROM @After) THROW 50005, 'Insufficient stock.', 1;
        INSERT INTO dbo.InventoryMovements (ProductId, QuantityChange, StockAfter, MovementType, OrderId)
        SELECT @ProductId, -@Quantity, Stock, 'reserve', @OrderId FROM @After;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE PROCEDURE dbo.usp_ReleaseOrderStock @OrderId BIGINT, @NewStatus VARCHAR(20) = 'expired'
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;
        UPDATE dbo.Orders SET InventoryState = 'released', Status = @NewStatus,
            CancelledAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @OrderId AND InventoryState = 'reserved';
        IF @@ROWCOUNT = 0 BEGIN COMMIT TRANSACTION; RETURN; END;
        DECLARE @Qty TABLE (ProductId INT PRIMARY KEY, Quantity INT);
        INSERT INTO @Qty (ProductId, Quantity)
        SELECT ProductId, -SUM(QuantityChange) FROM dbo.InventoryMovements
        WHERE OrderId = @OrderId AND MovementType = 'reserve' GROUP BY ProductId;
        DECLARE @After TABLE (ProductId INT, Stock INT);
        UPDATE p SET Stock = p.Stock + q.Quantity, UpdatedAt = SYSUTCDATETIME()
        OUTPUT inserted.Id, inserted.Stock INTO @After
        FROM dbo.Products p JOIN @Qty q ON q.ProductId = p.Id;
        INSERT INTO dbo.InventoryMovements (ProductId, QuantityChange, StockAfter, MovementType, OrderId)
        SELECT a.ProductId, q.Quantity, a.Stock, 'release', @OrderId
        FROM @After a JOIN @Qty q ON q.ProductId = a.ProductId;
        INSERT INTO dbo.OrderStatusHistory (OrderId, Status, Location)
        VALUES (@OrderId, @NewStatus, N'Order cancelled and stock released');
        COMMIT TRANSACTION;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE TRIGGER dbo.TR_Refunds_NotExceedPayment ON dbo.Refunds
    AFTER INSERT, UPDATE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS (
            SELECT 1 FROM dbo.Payments p
            JOIN (SELECT DISTINCT PaymentId FROM inserted) i ON i.PaymentId = p.Id
            CROSS APPLY (SELECT SUM(r.Amount) AS Refunded FROM dbo.Refunds r WHERE r.PaymentId = p.Id) s
            WHERE p.Status <> 'paid' OR s.Refunded > p.Amount)
            THROW 50006, 'Refund exceeds paid amount or payment is not paid.', 1;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE VIEW dbo.vw_DailyRevenue AS
    SELECT CAST(SWITCHOFFSET(CAST(o.CreatedAt AS DATETIMEOFFSET), '+07:00') AS DATE) AS SalesDate,
        COUNT(*) AS OrderCount, SUM(o.Total) AS GrossRevenue,
        SUM(ISNULL(r.Refunded, 0)) AS Refunded,
        SUM(o.Total) - SUM(ISNULL(r.Refunded, 0)) AS NetRevenue
    FROM dbo.Orders o
    OUTER APPLY (SELECT SUM(rf.Amount) AS Refunded FROM dbo.Payments p
        JOIN dbo.Refunds rf ON rf.PaymentId = p.Id WHERE p.OrderId = o.Id) r
    WHERE o.Status = 'delivered'
    GROUP BY CAST(SWITCHOFFSET(CAST(o.CreatedAt AS DATETIMEOFFSET), '+07:00') AS DATE);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    CREATE VIEW dbo.vw_ActivePromotions AS
    SELECT p.* FROM dbo.Promotions p
    WHERE p.IsActive = 1
      AND (p.StartDate IS NULL OR p.StartDate <= CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+07:00') AS DATE))
      AND (p.EndDate IS NULL OR p.EndDate >= CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+07:00') AS DATE));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114150321_InitialSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114150321_InitialSchema', N'10.0.14');
END;

COMMIT;
GO


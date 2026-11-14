using FlowerFlow.Domain.Catalog;
using FlowerFlow.Domain.Commerce;
using FlowerFlow.Domain.Identity;
using FlowerFlow.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace FlowerFlow.Infrastructure.Persistence;

/// <summary>Database session for one API request; EF tracks changes and translates LINQ to SQL.</summary>
public sealed class FlowerFlowDbContext(DbContextOptions<FlowerFlowDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<StockReceiptItem> StockReceiptItems => Set<StockReceiptItem>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionProduct> PromotionProducts => Set<PromotionProduct>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<WrappingFeeTier> WrappingFeeTiers => Set<WrappingFeeTier>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderCustomBouquet> OrderCustomBouquets => Set<OrderCustomBouquet>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewProduct> ReviewProducts => Set<ReviewProduct>();
    public DbSet<ReviewImage> ReviewImages => Set<ReviewImage>();
    public DbSet<CustomerMessage> CustomerMessages => Set<CustomerMessage>();
    public DbSet<DesignRequest> DesignRequests => Set<DesignRequest>();
    public DbSet<DesignRequestStem> DesignRequestStems => Set<DesignRequestStem>();
    public DbSet<DesignRequestHistory> DesignRequestHistory => Set<DesignRequestHistory>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<RateLimitCounter> RateLimitCounters => Set<RateLimitCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureIdentity(modelBuilder);
        ConfigureCatalog(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureSales(modelBuilder);
        ConfigureEngagement(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        var users = modelBuilder.Entity<User>();
        users.ToTable("Users", "dbo", table => table.HasCheckConstraint("CK_Users_Role", "[Role] IN ('customer', 'admin', 'owner')"));
        users.HasKey(x => x.Id);
        users.Property(x => x.FirebaseUid).HasMaxLength(128).IsRequired();
        users.HasIndex(x => x.FirebaseUid).IsUnique().HasDatabaseName("UQ_Users_FirebaseUid");
        users.Property(x => x.Email).HasMaxLength(256);
        users.HasIndex(x => x.Email).HasFilter("[Email] IS NOT NULL").HasDatabaseName("IX_Users_Email");
        users.Property(x => x.DisplayName).HasMaxLength(120);
        users.Property(x => x.Phone).HasColumnType("varchar(20)").HasMaxLength(20);
        users.Property(x => x.Role).HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("customer");
        users.Property(x => x.IsActive).HasDefaultValue(true);
        users.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        users.Property(x => x.UpdatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");

        var audit = modelBuilder.Entity<AuditLog>();
        audit.ToTable("AuditLogs", "dbo", table =>
        {
            table.HasCheckConstraint("CK_AuditLogs_OldJson", "[OldValue] IS NULL OR ISJSON([OldValue]) = 1");
            table.HasCheckConstraint("CK_AuditLogs_NewJson", "[NewValue] IS NULL OR ISJSON([NewValue]) = 1");
        });
        audit.HasKey(x => x.Id);
        audit.Property(x => x.Action).HasColumnType("varchar(60)").HasMaxLength(60);
        audit.Property(x => x.EntityType).HasColumnType("varchar(40)").HasMaxLength(40);
        audit.Property(x => x.EntityId).HasMaxLength(64);
        audit.Property(x => x.OldValue).HasColumnType("nvarchar(max)");
        audit.Property(x => x.NewValue).HasColumnType("nvarchar(max)");
        audit.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        audit.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.NoAction);
        audit.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt }).IsDescending(false, false, true).HasDatabaseName("IX_AuditLogs_Entity");
    }

    private static void ConfigureCatalog(ModelBuilder modelBuilder)
    {
        var category = modelBuilder.Entity<Category>();
        category.ToTable("Categories", "dbo");
        category.HasKey(x => x.Id);
        category.Property(x => x.Name).HasMaxLength(80).IsRequired();
        category.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UQ_Categories_Name");
        category.Property(x => x.GroupKey).HasColumnType("varchar(30)").HasMaxLength(30).HasDefaultValue("flowers");
        category.Property(x => x.SortOrder).HasDefaultValue(0);
        category.Property(x => x.IsActive).HasDefaultValue(true);

        var product = modelBuilder.Entity<Product>();
        product.ToTable("Products", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Products_SaleType", "[SaleType] IN ('ready_made', 'flower')");
            table.HasCheckConstraint("CK_Products_Unit", "([SaleType] = 'ready_made' AND [Unit] = N'b\u00F3') OR ([SaleType] = 'flower' AND [Unit] = N'c\u00E0nh')");
            table.HasCheckConstraint("CK_Products_Price", "[CurrentPrice] > 0");
            table.HasCheckConstraint("CK_Products_Stock", "[Stock] >= 0");
            table.HasCheckConstraint("CK_Products_Status", "[Status] IN ('active', 'hidden', 'archived')");
        });
        product.HasKey(x => x.Id);
        product.Property(x => x.Code).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        product.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Products_Code");
        product.Property(x => x.Name).HasMaxLength(120);
        product.Property(x => x.Description).HasMaxLength(2000);
        product.Property(x => x.Meaning).HasMaxLength(500);
        product.Property(x => x.SaleType).HasColumnType("varchar(20)").HasMaxLength(20);
        product.Property(x => x.Unit).HasMaxLength(10);
        product.Property(x => x.CurrentPrice).HasPrecision(18, 0);
        product.Property(x => x.Stock).HasDefaultValue(0);
        product.Property(x => x.SameDayDelivery).HasDefaultValue(false);
        product.Property(x => x.Status).HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("active");
        product.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        product.Property(x => x.UpdatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        product.Property(x => x.RowVer).IsRowVersion();
        product.HasIndex(x => new { x.CategoryId, x.Status }).HasDatabaseName("IX_Products_Category");
        product.HasIndex(x => new { x.SaleType, x.Status }).HasDatabaseName("IX_Products_SaleType");
        product.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        product.HasOne(x => x.CurrentPriceRecord).WithOne().HasForeignKey<Product>(x => x.CurrentPriceId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Products_CurrentPrice");

        var tag = modelBuilder.Entity<ProductTag>();
        tag.ToTable("ProductTags", "dbo", table => table.HasCheckConstraint("CK_ProductTags_Type", "[TagType] IN ('color', 'occasion')"));
        tag.HasKey(x => new { x.ProductId, x.TagType, x.Value });
        tag.Property(x => x.TagType).HasColumnType("varchar(20)").HasMaxLength(20);
        tag.Property(x => x.Value).HasMaxLength(50);
        tag.HasOne(x => x.Product).WithMany(x => x.Tags).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);

        var image = modelBuilder.Entity<ProductImage>();
        image.ToTable("ProductImages", "dbo", table => table.HasCheckConstraint("CK_ProductImages_Source", "([Url] IS NOT NULL AND [Data] IS NULL) OR ([Url] IS NULL AND [Data] IS NOT NULL AND [ContentType] IS NOT NULL)"));
        image.HasKey(x => x.Id);
        image.Property(x => x.Url).HasMaxLength(1000);
        image.Property(x => x.Data).HasColumnType("varbinary(max)");
        image.Property(x => x.ContentType).HasColumnType("varchar(40)").HasMaxLength(40);
        image.Property(x => x.SortOrder).HasDefaultValue(0);
        image.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        image.HasOne(x => x.Product).WithMany(x => x.Images).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        image.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.NoAction);
        image.HasIndex(x => new { x.ProductId, x.SortOrder }).HasDatabaseName("IX_ProductImages_Product");

        var price = modelBuilder.Entity<ProductPrice>();
        price.ToTable("ProductPrices", "dbo", table => table.HasCheckConstraint("CK_ProductPrices_Price", "[Price] > 0"));
        price.HasKey(x => x.Id);
        price.Property(x => x.Price).HasPrecision(18, 0);
        price.Property(x => x.PreviousPrice).HasPrecision(18, 0);
        price.Property(x => x.EffectiveFrom).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        price.Property(x => x.Reason).HasMaxLength(300);
        price.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        price.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.NoAction);
        price.HasIndex(x => new { x.ProductId, x.EffectiveFrom }).IsDescending(false, true).HasDatabaseName("IX_ProductPrices_Product");

        var zone = modelBuilder.Entity<DeliveryZone>();
        zone.ToTable("DeliveryZones", "dbo", table => table.HasCheckConstraint("CK_DeliveryZones_Fee", "[Fee] >= 0"));
        zone.HasKey(x => x.Id);
        zone.Property(x => x.Province).HasMaxLength(120);
        zone.Property(x => x.District).HasMaxLength(120);
        zone.Property(x => x.Fee).HasPrecision(18, 0);
        zone.Property(x => x.IsEnabled).HasDefaultValue(true);
        zone.Property(x => x.UpdatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        zone.HasIndex(x => new { x.Province, x.District }).IsUnique().HasDatabaseName("UQ_DeliveryZones_Area");

        var wrapping = modelBuilder.Entity<WrappingFeeTier>();
        wrapping.ToTable("WrappingFeeTiers", "dbo", table =>
        {
            table.HasCheckConstraint("CK_WrappingFeeTiers_Max", "[MaxStems] BETWEEN 1 AND 50");
            table.HasCheckConstraint("CK_WrappingFeeTiers_Fee", "[Fee] >= 0");
        });
        wrapping.HasKey(x => x.Id);
        wrapping.Property(x => x.Fee).HasPrecision(18, 0);
        wrapping.HasIndex(x => x.MaxStems).IsUnique().HasDatabaseName("UQ_WrappingFeeTiers_Max");

        var promotion = modelBuilder.Entity<Promotion>();
        promotion.ToTable("Promotions", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Promotions_Percent", "[DiscountPercent] > 0 AND [DiscountPercent] <= 90");
            table.HasCheckConstraint("CK_Promotions_Dates", "[StartDate] IS NULL OR [EndDate] IS NULL OR [StartDate] <= [EndDate]");
        });
        promotion.HasKey(x => x.Id);
        promotion.Property(x => x.Name).HasMaxLength(120);
        promotion.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        promotion.Property(x => x.AppliesToAll).HasDefaultValue(false);
        promotion.Property(x => x.IsActive).HasDefaultValue(true);
        promotion.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        promotion.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);

        var promotionProduct = modelBuilder.Entity<PromotionProduct>();
        promotionProduct.ToTable("PromotionProducts", "dbo");
        promotionProduct.HasKey(x => new { x.PromotionId, x.ProductId });
        promotionProduct.HasOne(x => x.Promotion).WithMany(x => x.Products).HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Cascade);
        promotionProduct.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureInventory(ModelBuilder modelBuilder)
    {
        var supplier = modelBuilder.Entity<Supplier>();
        supplier.ToTable("Suppliers", "dbo");
        supplier.HasKey(x => x.Id);
        supplier.Property(x => x.Name).HasMaxLength(150);
        supplier.Property(x => x.Phone).HasColumnType("varchar(20)").HasMaxLength(20);
        supplier.Property(x => x.Address).HasMaxLength(300);
        supplier.Property(x => x.IsActive).HasDefaultValue(true);

        var receipt = modelBuilder.Entity<StockReceipt>();
        receipt.ToTable("StockReceipts", "dbo");
        receipt.HasKey(x => x.Id);
        receipt.Property(x => x.ReceivedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        receipt.Property(x => x.Note).HasMaxLength(500);
        receipt.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.NoAction);
        receipt.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);

        var receiptItem = modelBuilder.Entity<StockReceiptItem>();
        receiptItem.ToTable("StockReceiptItems", "dbo", table =>
        {
            table.HasCheckConstraint("CK_StockReceiptItems_Qty", "[Quantity] > 0");
            table.HasCheckConstraint("CK_StockReceiptItems_Cost", "[UnitCost] >= 0");
        });
        receiptItem.HasKey(x => x.Id);
        receiptItem.Property(x => x.UnitCost).HasPrecision(18, 0);
        receiptItem.HasOne(x => x.Receipt).WithMany(x => x.Items).HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        receiptItem.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);

        var movement = modelBuilder.Entity<InventoryMovement>();
        movement.ToTable("InventoryMovements", "dbo", table =>
        {
            table.HasCheckConstraint("CK_InventoryMovements_Type", "[MovementType] IN ('import', 'reserve', 'release', 'adjust', 'return')");
            table.HasCheckConstraint("CK_InventoryMovements_Qty", "[QuantityChange] <> 0");
            table.HasCheckConstraint("CK_InventoryMovements_After", "[StockAfter] >= 0");
            table.HasCheckConstraint("CK_InventoryMovements_Link", "([MovementType] IN ('reserve', 'release', 'return') AND [OrderId] IS NOT NULL) OR ([MovementType] = 'import' AND [ReceiptItemId] IS NOT NULL) OR [MovementType] = 'adjust'");
        });
        movement.HasKey(x => x.Id);
        movement.Property(x => x.MovementType).HasColumnType("varchar(20)").HasMaxLength(20);
        movement.Property(x => x.Note).HasMaxLength(300);
        movement.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        movement.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        movement.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        movement.HasOne<StockReceiptItem>().WithMany().HasForeignKey(x => x.ReceiptItemId).OnDelete(DeleteBehavior.NoAction);
        movement.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
        movement.HasIndex(x => new { x.ProductId, x.CreatedAt }).IsDescending(false, true).HasDatabaseName("IX_InventoryMovements_Product");
        movement.HasIndex(x => x.OrderId).HasFilter("[OrderId] IS NOT NULL").HasDatabaseName("IX_InventoryMovements_Order");

        var counter = modelBuilder.Entity<RateLimitCounter>();
        counter.ToTable("RateLimitCounters", "dbo", table => table.HasCheckConstraint("CK_RateLimitCounters_Count", "[Count] >= 0"));
        counter.HasKey(x => new { x.Scope, x.KeyHash });
        counter.Property(x => x.Scope).HasColumnType("varchar(30)").HasMaxLength(30);
        counter.Property(x => x.KeyHash).HasColumnType("varbinary(32)").HasMaxLength(32);
        counter.Property(x => x.ExpiresAt).HasPrecision(3);
        counter.Property(x => x.LockedUntil).HasPrecision(3);
        counter.HasIndex(x => x.ExpiresAt).HasDatabaseName("IX_RateLimitCounters_Expires");
    }

    private static void ConfigureSales(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<Order>();
        order.ToTable("Orders", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Orders_Type", "[OrderType] IN ('ready_made', 'custom_bouquet')");
            table.HasCheckConstraint("CK_Orders_Status", "[Status] IN ('pending', 'preparing', 'shipping', 'delivered', 'cancelled', 'expired')");
            table.HasCheckConstraint("CK_Orders_PayMethod", "[PaymentMethod] IN ('cod', 'qr', 'vnpay')");
            table.HasCheckConstraint("CK_Orders_PayStatus", "[PaymentStatus] IN ('cod_pending', 'awaiting', 'paid', 'failed', 'refunded', 'partially_refunded')");
            table.HasCheckConstraint("CK_Orders_Inventory", "[InventoryState] IN ('reserved', 'committed', 'released')");
            table.HasCheckConstraint("CK_Orders_Money", "[Subtotal] >= 0 AND [Savings] >= 0 AND [Savings] <= [Subtotal] AND [WrappingFee] >= 0 AND [ShippingFee] >= 0 AND [Tax] >= 0");
            table.HasCheckConstraint("CK_Orders_Total", "[Total] = [Subtotal] - [Savings] + [WrappingFee] + [ShippingFee] + [Tax]");
        });
        order.HasKey(x => x.Id);
        order.Property(x => x.OrderCode).HasColumnType("varchar(32)").HasMaxLength(32);
        order.HasIndex(x => x.OrderCode).IsUnique().HasDatabaseName("UQ_Orders_Code");
        order.Property(x => x.PaymentReference).HasColumnType("varchar(40)").HasMaxLength(40);
        order.HasIndex(x => x.PaymentReference).IsUnique().HasDatabaseName("UQ_Orders_PaymentRef");
        order.Property(x => x.IdempotencyKey).HasColumnType("varchar(80)").HasMaxLength(80);
        order.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UQ_Orders_Idempotency");
        order.Property(x => x.RequestHash).HasColumnType("varchar(64)").HasMaxLength(64);
        order.Property(x => x.OrderType).HasColumnType("varchar(20)").HasMaxLength(20);
        order.Property(x => x.CustomerName).HasMaxLength(120);
        order.Property(x => x.Phone).HasColumnType("varchar(20)").HasMaxLength(20);
        order.Property(x => x.Address).HasMaxLength(300);
        order.Property(x => x.RecipientName).HasMaxLength(120);
        order.Property(x => x.RecipientPhone).HasColumnType("varchar(20)").HasMaxLength(20);
        order.Property(x => x.DeliveryProvince).HasMaxLength(120);
        order.Property(x => x.DeliveryDistrict).HasMaxLength(120);
        order.Property(x => x.DeliveryTime).HasMaxLength(40);
        order.Property(x => x.GreetingCard).HasMaxLength(300);
        order.Property(x => x.OrderNote).HasMaxLength(500);
        order.Property(x => x.PromotionName).HasMaxLength(120);
        order.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        order.Property(x => x.Subtotal).HasPrecision(18, 0);
        order.Property(x => x.Savings).HasPrecision(18, 0).HasDefaultValue(0);
        order.Property(x => x.WrappingFee).HasPrecision(18, 0).HasDefaultValue(0);
        order.Property(x => x.ShippingFee).HasPrecision(18, 0).HasDefaultValue(0);
        order.Property(x => x.Tax).HasPrecision(18, 0).HasDefaultValue(0);
        order.Property(x => x.Total).HasPrecision(18, 0);
        order.Property(x => x.Status).HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("pending");
        order.Property(x => x.CurrentLocation).HasMaxLength(200);
        order.Property(x => x.EstimatedDelivery).HasMaxLength(100);
        order.Property(x => x.PaymentMethod).HasColumnType("varchar(10)").HasMaxLength(10);
        order.Property(x => x.PaymentStatus).HasColumnType("varchar(20)").HasMaxLength(20);
        order.Property(x => x.InventoryState).HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("reserved");
        order.Property(x => x.ReservationExpiresAt).HasPrecision(3);
        order.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        order.Property(x => x.UpdatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        order.Property(x => x.CancelledAt).HasPrecision(3);
        order.Property(x => x.RowVer).IsRowVersion();
        order.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        order.HasOne<DeliveryZone>().WithMany().HasForeignKey(x => x.DeliveryZoneId).OnDelete(DeleteBehavior.NoAction);
        order.HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.NoAction);
        order.HasIndex(x => new { x.UserId, x.CreatedAt }).IsDescending(false, true).HasFilter("[UserId] IS NOT NULL").HasDatabaseName("IX_Orders_User");
        order.HasIndex(x => new { x.Status, x.CreatedAt }).IsDescending(false, true).HasDatabaseName("IX_Orders_Status");
        order.HasIndex(x => x.ReservationExpiresAt).HasFilter("[InventoryState] = 'reserved'").HasDatabaseName("IX_Orders_Reservation");
        order.HasIndex(x => x.Phone).HasDatabaseName("IX_Orders_Phone");

        var customBouquet = modelBuilder.Entity<OrderCustomBouquet>();
        customBouquet.ToTable("OrderCustomBouquets", "dbo", table =>
        {
            table.HasCheckConstraint("CK_OrderCustomBouquets_Stems", "[StemCount] BETWEEN 1 AND 50");
            table.HasCheckConstraint("CK_OrderCustomBouquets_Fee", "[WrappingFee] >= 0");
        });
        customBouquet.HasKey(x => x.Id);
        customBouquet.Property(x => x.BouquetKey).HasColumnType("varchar(64)").HasMaxLength(64);
        customBouquet.Property(x => x.WrappingFee).HasPrecision(18, 0);
        customBouquet.HasIndex(x => new { x.OrderId, x.BouquetKey }).IsUnique().HasDatabaseName("UQ_OrderCustomBouquets");
        customBouquet.HasOne(x => x.Order).WithMany(x => x.CustomBouquets).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);

        var orderItem = modelBuilder.Entity<OrderItem>();
        orderItem.ToTable("OrderItems", "dbo", table =>
        {
            table.HasCheckConstraint("CK_OrderItems_Qty", "[Quantity] > 0");
            table.HasCheckConstraint("CK_OrderItems_Price", "[UnitPrice] > 0");
            table.HasCheckConstraint("CK_OrderItems_Discount", "[DiscountAmount] >= 0 AND [DiscountAmount] <= [UnitPrice] * [Quantity]");
        });
        orderItem.HasKey(x => x.Id);
        orderItem.Property(x => x.ProductCode).HasColumnType("varchar(64)").HasMaxLength(64);
        orderItem.Property(x => x.ProductName).HasMaxLength(120);
        orderItem.Property(x => x.ImageUrl).HasMaxLength(1000);
        orderItem.Property(x => x.SaleType).HasColumnType("varchar(20)").HasMaxLength(20);
        orderItem.Property(x => x.Unit).HasMaxLength(10);
        orderItem.Property(x => x.UnitPrice).HasPrecision(18, 0);
        orderItem.Property(x => x.DiscountAmount).HasPrecision(18, 0).HasDefaultValue(0);
        orderItem.Property(x => x.LineTotal).HasComputedColumnSql("([UnitPrice] * [Quantity] - [DiscountAmount])", stored: true);
        orderItem.Property(x => x.Size).HasMaxLength(40);
        orderItem.Property(x => x.Color).HasMaxLength(40);
        orderItem.Property(x => x.CardMessage).HasMaxLength(300);
        orderItem.Property(x => x.PersonalNote).HasMaxLength(300);
        orderItem.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        orderItem.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        orderItem.HasOne<ProductPrice>().WithMany().HasForeignKey(x => x.ProductPriceId).OnDelete(DeleteBehavior.NoAction);
        orderItem.HasOne<OrderCustomBouquet>().WithMany(x => x.Items).HasForeignKey(x => x.CustomBouquetId).OnDelete(DeleteBehavior.NoAction);
        orderItem.HasIndex(x => x.OrderId).HasDatabaseName("IX_OrderItems_Order");
        orderItem.HasIndex(x => x.ProductId).HasDatabaseName("IX_OrderItems_Product");

        var orderHistory = modelBuilder.Entity<OrderStatusHistory>();
        orderHistory.ToTable("OrderStatusHistory", "dbo");
        orderHistory.HasKey(x => x.Id);
        orderHistory.Property(x => x.Status).HasColumnType("varchar(20)").HasMaxLength(20);
        orderHistory.Property(x => x.Location).HasMaxLength(200);
        orderHistory.Property(x => x.EstimatedDelivery).HasMaxLength(100);
        orderHistory.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        orderHistory.HasOne(x => x.Order).WithMany(x => x.StatusHistory).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        orderHistory.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.NoAction);
        orderHistory.HasIndex(x => new { x.OrderId, x.CreatedAt }).HasDatabaseName("IX_OrderStatusHistory_Order");

        var payment = modelBuilder.Entity<Payment>();
        payment.ToTable("Payments", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Payments_Provider", "[Provider] IN ('cod', 'qr', 'vnpay')");
            table.HasCheckConstraint("CK_Payments_Status", "[Status] IN ('pending', 'paid', 'failed', 'expired', 'cancelled')");
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_Payments_Raw", "[RawResponse] IS NULL OR ISJSON([RawResponse]) = 1");
        });
        payment.HasKey(x => x.Id);
        payment.Property(x => x.Provider).HasColumnType("varchar(10)").HasMaxLength(10);
        payment.Property(x => x.TxnRef).HasColumnType("varchar(64)").HasMaxLength(64);
        payment.HasIndex(x => x.TxnRef).IsUnique().HasDatabaseName("UQ_Payments_TxnRef");
        payment.Property(x => x.Amount).HasPrecision(18, 0);
        payment.Property(x => x.Status).HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("pending");
        payment.Property(x => x.ProviderTransactionNo).HasColumnType("varchar(64)").HasMaxLength(64);
        payment.Property(x => x.BankCode).HasColumnType("varchar(20)").HasMaxLength(20);
        payment.Property(x => x.ResponseCode).HasColumnType("varchar(10)").HasMaxLength(10);
        payment.Property(x => x.RawResponse).HasColumnType("nvarchar(max)");
        payment.Property(x => x.ExpiresAt).HasPrecision(3);
        payment.Property(x => x.PaidAt).HasPrecision(3);
        payment.Property(x => x.ReconciliationReference).HasMaxLength(120);
        payment.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        payment.HasOne(x => x.Order).WithMany(x => x.Payments).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        payment.HasOne<User>().WithMany().HasForeignKey(x => x.VerifiedBy).OnDelete(DeleteBehavior.NoAction);
        payment.HasIndex(x => new { x.OrderId, x.Attempt }).IsUnique().HasDatabaseName("UQ_Payments_Attempt");
        payment.HasIndex(x => new { x.Provider, x.ProviderTransactionNo }).IsUnique()
            .HasFilter("[ProviderTransactionNo] IS NOT NULL").HasDatabaseName("UX_Payments_ProviderTxn");
        payment.HasIndex(x => x.OrderId).IsUnique().HasFilter("[Status] = 'paid'").HasDatabaseName("UX_Payments_OnePaid");

        var refund = modelBuilder.Entity<Refund>();
        refund.ToTable("Refunds", "dbo", table => table.HasCheckConstraint("CK_Refunds_Amount", "[Amount] > 0"));
        refund.HasKey(x => x.Id);
        refund.Property(x => x.Amount).HasPrecision(18, 0);
        refund.Property(x => x.Reference).HasMaxLength(120);
        refund.Property(x => x.Reason).HasMaxLength(300);
        refund.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        refund.HasOne(x => x.Payment).WithMany(x => x.Refunds).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
        refund.HasOne<User>().WithMany().HasForeignKey(x => x.RefundedBy).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureEngagement(ModelBuilder modelBuilder)
    {
        var review = modelBuilder.Entity<Review>();
        review.ToTable("Reviews", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Reviews_Moderation", "[ModerationStatus] IN ('pending', 'approved', 'hidden')");
        });
        review.HasKey(x => x.Id);
        review.Property(x => x.CustomerName).HasMaxLength(120);
        review.Property(x => x.Comment).HasMaxLength(1000);
        review.Property(x => x.ModerationStatus).HasColumnType("varchar(10)").HasMaxLength(10).HasDefaultValue("pending");
        review.Property(x => x.ModerationReason).HasMaxLength(500);
        review.Property(x => x.ModeratedAt).HasPrecision(3);
        review.Property(x => x.Reply).HasMaxLength(1000);
        review.Property(x => x.CompletedAt).HasPrecision(3);
        review.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        review.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.NoAction);
        review.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        review.HasOne<User>().WithMany().HasForeignKey(x => x.ModeratedBy).OnDelete(DeleteBehavior.NoAction);
        review.HasIndex(x => new { x.OrderId, x.AuthorUserId }).IsUnique().HasFilter("[OrderId] IS NOT NULL").HasDatabaseName("UX_Reviews_OrderAuthor");

        var reviewProduct = modelBuilder.Entity<ReviewProduct>();
        reviewProduct.ToTable("ReviewProducts", "dbo");
        reviewProduct.HasKey(x => new { x.ReviewId, x.ProductId });
        reviewProduct.HasOne(x => x.Review).WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        reviewProduct.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        reviewProduct.HasIndex(x => x.ProductId).HasDatabaseName("IX_ReviewProducts_Product");

        var reviewImage = modelBuilder.Entity<ReviewImage>();
        reviewImage.ToTable("ReviewImages", "dbo");
        reviewImage.HasKey(x => x.Id);
        reviewImage.Property(x => x.Data).HasColumnType("varbinary(max)");
        reviewImage.Property(x => x.ContentType).HasColumnType("varchar(40)").HasMaxLength(40);
        reviewImage.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        reviewImage.HasOne(x => x.Review).WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);

        var message = modelBuilder.Entity<CustomerMessage>();
        message.ToTable("CustomerMessages", "dbo");
        message.HasKey(x => x.Id);
        message.Property(x => x.CustomerName).HasMaxLength(120);
        message.Property(x => x.Content).HasMaxLength(1000);
        message.Property(x => x.Reply).HasMaxLength(1000);
        message.Property(x => x.CompletedAt).HasPrecision(3);
        message.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        message.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);

        var design = modelBuilder.Entity<DesignRequest>();
        design.ToTable("DesignRequests", "dbo", table =>
        {
            table.HasCheckConstraint("CK_DesignRequests_Design", "ISJSON([Design]) = 1");
            table.HasCheckConstraint("CK_DesignRequests_Status", "[Status] IN ('consulting', 'quoting', 'awaiting_customer', 'in_progress', 'completed', 'cancelled')");
            table.HasCheckConstraint("CK_DesignRequests_Money", "[EstimatedTotal] >= 0 AND ([QuotedTotal] IS NULL OR [QuotedTotal] >= 0)");
        });
        design.HasKey(x => x.Id);
        design.Property(x => x.RequestCode).HasColumnType("varchar(32)").HasMaxLength(32);
        design.HasIndex(x => x.RequestCode).IsUnique().HasDatabaseName("UQ_DesignRequests_Code");
        design.Property(x => x.AccessTokenHash).HasColumnType("varbinary(32)").HasMaxLength(32);
        design.Property(x => x.CustomerName).HasMaxLength(120);
        design.Property(x => x.Phone).HasColumnType("varchar(20)").HasMaxLength(20);
        design.Property(x => x.DeliveryProvince).HasMaxLength(120);
        design.Property(x => x.DeliveryDistrict).HasMaxLength(120);
        design.Property(x => x.Design).HasColumnType("nvarchar(max)");
        design.Property(x => x.SpecialRequest).HasMaxLength(1500);
        design.Property(x => x.EstimatedTotal).HasPrecision(18, 0).HasDefaultValue(0);
        design.Property(x => x.QuotedTotal).HasPrecision(18, 0);
        design.Property(x => x.QuoteNote).HasMaxLength(1000);
        design.Property(x => x.Status).HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("consulting");
        design.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        design.Property(x => x.UpdatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        design.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        design.HasOne<DeliveryZone>().WithMany().HasForeignKey(x => x.DeliveryZoneId).OnDelete(DeleteBehavior.NoAction);
        design.HasOne<Order>().WithMany().HasForeignKey(x => x.ConvertedOrderId).OnDelete(DeleteBehavior.NoAction);
        design.HasIndex(x => new { x.Status, x.CreatedAt }).IsDescending(false, true).HasDatabaseName("IX_DesignRequests_Status");

        var designStem = modelBuilder.Entity<DesignRequestStem>();
        designStem.ToTable("DesignRequestStems", "dbo", table => table.HasCheckConstraint("CK_DesignRequestStems_Qty", "[Quantity] > 0"));
        designStem.HasKey(x => new { x.RequestId, x.ProductId });
        designStem.Property(x => x.UnitPrice).HasPrecision(18, 0);
        designStem.HasOne<DesignRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        designStem.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);

        var designHistory = modelBuilder.Entity<DesignRequestHistory>();
        designHistory.ToTable("DesignRequestHistory", "dbo");
        designHistory.HasKey(x => x.Id);
        designHistory.Property(x => x.Status).HasColumnType("varchar(20)").HasMaxLength(20);
        designHistory.Property(x => x.Note).HasMaxLength(1000);
        designHistory.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        designHistory.HasOne<DesignRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        designHistory.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.NoAction);

        var cart = modelBuilder.Entity<CartItem>();
        cart.ToTable("CartItems", "dbo", table =>
        {
            table.HasCheckConstraint("CK_CartItems_Qty", "[Quantity] BETWEEN 1 AND 999");
            table.HasCheckConstraint("CK_CartItems_Options", "[Options] IS NULL OR ISJSON([Options]) = 1");
        });
        cart.HasKey(x => new { x.UserId, x.LineKey });
        cart.Property(x => x.LineKey).HasColumnType("varchar(100)").HasMaxLength(100);
        cart.Property(x => x.Options).HasMaxLength(1000);
        cart.Property(x => x.UpdatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        cart.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        cart.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);

        var wishlist = modelBuilder.Entity<WishlistItem>();
        wishlist.ToTable("WishlistItems", "dbo");
        wishlist.HasKey(x => new { x.UserId, x.ProductId });
        wishlist.Property(x => x.AddedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        wishlist.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        wishlist.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
    }
}

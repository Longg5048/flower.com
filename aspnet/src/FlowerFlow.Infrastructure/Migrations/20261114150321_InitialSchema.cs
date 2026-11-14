using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowerFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    GroupKey = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "flowers"),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryZones",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Province = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    District = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryZones", x => x.Id);
                    table.CheckConstraint("CK_DeliveryZones_Fee", "[Fee] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "RateLimitCounters",
                schema: "dbo",
                columns: table => new
                {
                    Scope = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    KeyHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateLimitCounters", x => new { x.Scope, x.KeyHash });
                    table.CheckConstraint("CK_RateLimitCounters_Count", "[Count] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirebaseUid = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "customer"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_Role", "[Role] IN ('customer', 'admin', 'owner')");
                });

            migrationBuilder.CreateTable(
                name: "WrappingFeeTiers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaxStems = table.Column<int>(type: "int", nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WrappingFeeTiers", x => x.Id);
                    table.CheckConstraint("CK_WrappingFeeTiers_Fee", "[Fee] >= 0");
                    table.CheckConstraint("CK_WrappingFeeTiers_Max", "[MaxStems] BETWEEN 1 AND 50");
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    EntityType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_NewJson", "[NewValue] IS NULL OR ISJSON([NewValue]) = 1");
                    table.CheckConstraint("CK_AuditLogs_OldJson", "[OldValue] IS NULL OR ISJSON([OldValue]) = 1");
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Promotions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AppliesToAll = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                    table.CheckConstraint("CK_Promotions_Dates", "[StartDate] IS NULL OR [EndDate] IS NULL OR [StartDate] <= [EndDate]");
                    table.CheckConstraint("CK_Promotions_Percent", "[DiscountPercent] > 0 AND [DiscountPercent] <= 90");
                    table.ForeignKey(
                        name: "FK_Promotions_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockReceipts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockReceipts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "dbo",
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockReceipts_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    PaymentReference = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    OrderType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RecipientPhone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    CustomerIsRecipient = table.Column<bool>(type: "bit", nullable: false),
                    DeliveryZoneId = table.Column<int>(type: "int", nullable: false),
                    DeliveryProvince = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DeliveryDistrict = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeliveryTime = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GreetingCard = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AnonymousGreetingCard = table.Column<bool>(type: "bit", nullable: false),
                    GiftWrap = table.Column<bool>(type: "bit", nullable: false),
                    OrderNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PromotionId = table.Column<int>(type: "int", nullable: true),
                    PromotionName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Savings = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false, defaultValue: 0m),
                    WrappingFee = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false, defaultValue: 0m),
                    ShippingFee = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false, defaultValue: 0m),
                    Tax = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false, defaultValue: 0m),
                    Total = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    CurrentLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EstimatedDelivery = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentMethod = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    PaymentStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    InventoryState = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "reserved"),
                    ReservationExpiresAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CancelledAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_Inventory", "[InventoryState] IN ('reserved', 'committed', 'released')");
                    table.CheckConstraint("CK_Orders_Money", "[Subtotal] >= 0 AND [Savings] >= 0 AND [Savings] <= [Subtotal] AND [WrappingFee] >= 0 AND [ShippingFee] >= 0 AND [Tax] >= 0");
                    table.CheckConstraint("CK_Orders_PayMethod", "[PaymentMethod] IN ('cod', 'qr', 'vnpay')");
                    table.CheckConstraint("CK_Orders_PayStatus", "[PaymentStatus] IN ('cod_pending', 'awaiting', 'paid', 'failed', 'refunded', 'partially_refunded')");
                    table.CheckConstraint("CK_Orders_Status", "[Status] IN ('pending', 'preparing', 'shipping', 'delivered', 'cancelled', 'expired')");
                    table.CheckConstraint("CK_Orders_Total", "[Total] = [Subtotal] - [Savings] + [WrappingFee] + [ShippingFee] + [Tax]");
                    table.CheckConstraint("CK_Orders_Type", "[OrderType] IN ('ready_made', 'custom_bouquet')");
                    table.ForeignKey(
                        name: "FK_Orders_DeliveryZones_DeliveryZoneId",
                        column: x => x.DeliveryZoneId,
                        principalSchema: "dbo",
                        principalTable: "DeliveryZones",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Orders_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalSchema: "dbo",
                        principalTable: "Promotions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Orders_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CustomerMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Reply = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerMessages_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "dbo",
                        principalTable: "Orders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DesignRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    AccessTokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    DeliveryZoneId = table.Column<int>(type: "int", nullable: true),
                    DeliveryProvince = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DeliveryDistrict = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Design = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpecialRequest = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    EstimatedTotal = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false, defaultValue: 0m),
                    QuotedTotal = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: true),
                    QuoteNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "consulting"),
                    ConvertedOrderId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignRequests", x => x.Id);
                    table.CheckConstraint("CK_DesignRequests_Design", "ISJSON([Design]) = 1");
                    table.CheckConstraint("CK_DesignRequests_Money", "[EstimatedTotal] >= 0 AND ([QuotedTotal] IS NULL OR [QuotedTotal] >= 0)");
                    table.CheckConstraint("CK_DesignRequests_Status", "[Status] IN ('consulting', 'quoting', 'awaiting_customer', 'in_progress', 'completed', 'cancelled')");
                    table.ForeignKey(
                        name: "FK_DesignRequests_DeliveryZones_DeliveryZoneId",
                        column: x => x.DeliveryZoneId,
                        principalSchema: "dbo",
                        principalTable: "DeliveryZones",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DesignRequests_Orders_ConvertedOrderId",
                        column: x => x.ConvertedOrderId,
                        principalSchema: "dbo",
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DesignRequests_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderCustomBouquets",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    BouquetKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    StemCount = table.Column<int>(type: "int", nullable: false),
                    WrappingFee = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderCustomBouquets", x => x.Id);
                    table.CheckConstraint("CK_OrderCustomBouquets_Fee", "[WrappingFee] >= 0");
                    table.CheckConstraint("CK_OrderCustomBouquets_Stems", "[StemCount] BETWEEN 1 AND 50");
                    table.ForeignKey(
                        name: "FK_OrderCustomBouquets_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "dbo",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusHistory",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EstimatedDelivery = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChangedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "dbo",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Users_ChangedBy",
                        column: x => x.ChangedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Provider = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    TxnRef = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    ProviderTransactionNo = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    BankCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    ResponseCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true),
                    RawResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    VerifiedBy = table.Column<int>(type: "int", nullable: true),
                    ReconciliationReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Payments_Provider", "[Provider] IN ('cod', 'qr', 'vnpay')");
                    table.CheckConstraint("CK_Payments_Raw", "[RawResponse] IS NULL OR ISJSON([RawResponse]) = 1");
                    table.CheckConstraint("CK_Payments_Status", "[Status] IN ('pending', 'paid', 'failed', 'expired', 'cancelled')");
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "dbo",
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payments_Users_VerifiedBy",
                        column: x => x.VerifiedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorUserId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Rating = table.Column<byte>(type: "tinyint", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ModerationStatus = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, defaultValue: "pending"),
                    ModerationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ModeratedBy = table.Column<int>(type: "int", nullable: true),
                    ModeratedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    Reply = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.CheckConstraint("CK_Reviews_Moderation", "[ModerationStatus] IN ('pending', 'approved', 'hidden')");
                    table.CheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Reviews_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "dbo",
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_Users_ModeratedBy",
                        column: x => x.ModeratedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DesignRequestHistory",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignRequestHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesignRequestHistory_DesignRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dbo",
                        principalTable: "DesignRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DesignRequestHistory_Users_ChangedBy",
                        column: x => x.ChangedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RefundedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.CheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Refunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "dbo",
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Refunds_Users_RefundedBy",
                        column: x => x.RefundedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReviewImages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReviewId = table.Column<long>(type: "bigint", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewImages_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalSchema: "dbo",
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CartItems",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LineKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Options = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItems", x => new { x.UserId, x.LineKey });
                    table.CheckConstraint("CK_CartItems_Options", "[Options] IS NULL OR ISJSON([Options]) = 1");
                    table.CheckConstraint("CK_CartItems_Qty", "[Quantity] BETWEEN 1 AND 999");
                    table.ForeignKey(
                        name: "FK_CartItems_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DesignRequestStems",
                schema: "dbo",
                columns: table => new
                {
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignRequestStems", x => new { x.RequestId, x.ProductId });
                    table.CheckConstraint("CK_DesignRequestStems_Qty", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_DesignRequestStems_DesignRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dbo",
                        principalTable: "DesignRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    QuantityChange = table.Column<int>(type: "int", nullable: false),
                    StockAfter = table.Column<int>(type: "int", nullable: false),
                    MovementType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    ReceiptItemId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.CheckConstraint("CK_InventoryMovements_After", "[StockAfter] >= 0");
                    table.CheckConstraint("CK_InventoryMovements_Link", "([MovementType] IN ('reserve', 'release', 'return') AND [OrderId] IS NOT NULL) OR ([MovementType] = 'import' AND [ReceiptItemId] IS NOT NULL) OR [MovementType] = 'adjust'");
                    table.CheckConstraint("CK_InventoryMovements_Qty", "[QuantityChange] <> 0");
                    table.CheckConstraint("CK_InventoryMovements_Type", "[MovementType] IN ('import', 'reserve', 'release', 'adjust', 'return')");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "dbo",
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductPriceId = table.Column<long>(type: "bigint", nullable: true),
                    CustomBouquetId = table.Column<long>(type: "bigint", nullable: true),
                    ProductCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SaleType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false, defaultValue: 0m),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false, computedColumnSql: "([UnitPrice] * [Quantity] - [DiscountAmount])", stored: true),
                    Size = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CardMessage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PersonalNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_Discount", "[DiscountAmount] >= 0 AND [DiscountAmount] <= [UnitPrice] * [Quantity]");
                    table.CheckConstraint("CK_OrderItems_Price", "[UnitPrice] > 0");
                    table.CheckConstraint("CK_OrderItems_Qty", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_OrderCustomBouquets_CustomBouquetId",
                        column: x => x.CustomBouquetId,
                        principalSchema: "dbo",
                        principalTable: "OrderCustomBouquets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "dbo",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ContentType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UploadedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.CheckConstraint("CK_ProductImages_Source", "([Url] IS NOT NULL AND [Data] IS NULL) OR ([Url] IS NULL AND [Data] IS NOT NULL AND [ContentType] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProductImages_Users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductPrices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    PreviousPrice = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ChangedBy = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPrices", x => x.Id);
                    table.CheckConstraint("CK_ProductPrices_Price", "[Price] > 0");
                    table.ForeignKey(
                        name: "FK_ProductPrices_Users_ChangedBy",
                        column: x => x.ChangedBy,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Meaning = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    SaleType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    CurrentPriceId = table.Column<long>(type: "bigint", nullable: true),
                    Stock = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SameDayDelivery = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_Price", "[CurrentPrice] > 0");
                    table.CheckConstraint("CK_Products_SaleType", "[SaleType] IN ('ready_made', 'flower')");
                    table.CheckConstraint("CK_Products_Status", "[Status] IN ('active', 'hidden', 'archived')");
                    table.CheckConstraint("CK_Products_Stock", "[Stock] >= 0");
                    table.CheckConstraint("CK_Products_Unit", "([SaleType] = 'ready_made' AND [Unit] = N'bó') OR ([SaleType] = 'flower' AND [Unit] = N'cành')");
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "dbo",
                        principalTable: "Categories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Products_CurrentPrice",
                        column: x => x.CurrentPriceId,
                        principalSchema: "dbo",
                        principalTable: "ProductPrices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductTags",
                schema: "dbo",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    TagType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTags", x => new { x.ProductId, x.TagType, x.Value });
                    table.CheckConstraint("CK_ProductTags_Type", "[TagType] IN ('color', 'occasion')");
                    table.ForeignKey(
                        name: "FK_ProductTags_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromotionProducts",
                schema: "dbo",
                columns: table => new
                {
                    PromotionId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionProducts", x => new { x.PromotionId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_PromotionProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromotionProducts_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalSchema: "dbo",
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewProducts",
                schema: "dbo",
                columns: table => new
                {
                    ReviewId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewProducts", x => new { x.ReviewId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_ReviewProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReviewProducts_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalSchema: "dbo",
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockReceiptItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceiptItems", x => x.Id);
                    table.CheckConstraint("CK_StockReceiptItems_Cost", "[UnitCost] >= 0");
                    table.CheckConstraint("CK_StockReceiptItems_Qty", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_StockReceiptItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockReceiptItems_StockReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "StockReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WishlistItems",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishlistItems", x => new { x.UserId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_WishlistItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WishlistItems_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                schema: "dbo",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entity",
                schema: "dbo",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ProductId",
                schema: "dbo",
                table: "CartItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UQ_Categories_Name",
                schema: "dbo",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMessages_OrderId",
                schema: "dbo",
                table: "CustomerMessages",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "UQ_DeliveryZones_Area",
                schema: "dbo",
                table: "DeliveryZones",
                columns: new[] { "Province", "District" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesignRequestHistory_ChangedBy",
                schema: "dbo",
                table: "DesignRequestHistory",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DesignRequestHistory_RequestId",
                schema: "dbo",
                table: "DesignRequestHistory",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignRequests_ConvertedOrderId",
                schema: "dbo",
                table: "DesignRequests",
                column: "ConvertedOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignRequests_DeliveryZoneId",
                schema: "dbo",
                table: "DesignRequests",
                column: "DeliveryZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignRequests_Status",
                schema: "dbo",
                table: "DesignRequests",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DesignRequests_UserId",
                schema: "dbo",
                table: "DesignRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_DesignRequests_Code",
                schema: "dbo",
                table: "DesignRequests",
                column: "RequestCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesignRequestStems_ProductId",
                schema: "dbo",
                table: "DesignRequestStems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_CreatedBy",
                schema: "dbo",
                table: "InventoryMovements",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Order",
                schema: "dbo",
                table: "InventoryMovements",
                column: "OrderId",
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Product",
                schema: "dbo",
                table: "InventoryMovements",
                columns: new[] { "ProductId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ReceiptItemId",
                schema: "dbo",
                table: "InventoryMovements",
                column: "ReceiptItemId");

            migrationBuilder.CreateIndex(
                name: "UQ_OrderCustomBouquets",
                schema: "dbo",
                table: "OrderCustomBouquets",
                columns: new[] { "OrderId", "BouquetKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_CustomBouquetId",
                schema: "dbo",
                table: "OrderItems",
                column: "CustomBouquetId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_Order",
                schema: "dbo",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_Product",
                schema: "dbo",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductPriceId",
                schema: "dbo",
                table: "OrderItems",
                column: "ProductPriceId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryZoneId",
                schema: "dbo",
                table: "Orders",
                column: "DeliveryZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Phone",
                schema: "dbo",
                table: "Orders",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PromotionId",
                schema: "dbo",
                table: "Orders",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Reservation",
                schema: "dbo",
                table: "Orders",
                column: "ReservationExpiresAt",
                filter: "[InventoryState] = 'reserved'");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                schema: "dbo",
                table: "Orders",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_User",
                schema: "dbo",
                table: "Orders",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true },
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Orders_Code",
                schema: "dbo",
                table: "Orders",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Orders_Idempotency",
                schema: "dbo",
                table: "Orders",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Orders_PaymentRef",
                schema: "dbo",
                table: "Orders",
                column: "PaymentReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_ChangedBy",
                schema: "dbo",
                table: "OrderStatusHistory",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_Order",
                schema: "dbo",
                table: "OrderStatusHistory",
                columns: new[] { "OrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_VerifiedBy",
                schema: "dbo",
                table: "Payments",
                column: "VerifiedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_Payments_Attempt",
                schema: "dbo",
                table: "Payments",
                columns: new[] { "OrderId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Payments_TxnRef",
                schema: "dbo",
                table: "Payments",
                column: "TxnRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Payments_OnePaid",
                schema: "dbo",
                table: "Payments",
                column: "OrderId",
                unique: true,
                filter: "[Status] = 'paid'");

            migrationBuilder.CreateIndex(
                name: "UX_Payments_ProviderTxn",
                schema: "dbo",
                table: "Payments",
                columns: new[] { "Provider", "ProviderTransactionNo" },
                unique: true,
                filter: "[ProviderTransactionNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_Product",
                schema: "dbo",
                table: "ProductImages",
                columns: new[] { "ProductId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_UploadedBy",
                schema: "dbo",
                table: "ProductImages",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_ChangedBy",
                schema: "dbo",
                table: "ProductPrices",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_Product",
                schema: "dbo",
                table: "ProductPrices",
                columns: new[] { "ProductId", "EffectiveFrom" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category",
                schema: "dbo",
                table: "Products",
                columns: new[] { "CategoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CurrentPriceId",
                schema: "dbo",
                table: "Products",
                column: "CurrentPriceId",
                unique: true,
                filter: "[CurrentPriceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SaleType",
                schema: "dbo",
                table: "Products",
                columns: new[] { "SaleType", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ_Products_Code",
                schema: "dbo",
                table: "Products",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionProducts_ProductId",
                schema: "dbo",
                table: "PromotionProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_CreatedBy",
                schema: "dbo",
                table: "Promotions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RateLimitCounters_Expires",
                schema: "dbo",
                table: "RateLimitCounters",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                schema: "dbo",
                table: "Refunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RefundedBy",
                schema: "dbo",
                table: "Refunds",
                column: "RefundedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewImages_ReviewId",
                schema: "dbo",
                table: "ReviewImages",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewProducts_Product",
                schema: "dbo",
                table: "ReviewProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_AuthorUserId",
                schema: "dbo",
                table: "Reviews",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ModeratedBy",
                schema: "dbo",
                table: "Reviews",
                column: "ModeratedBy");

            migrationBuilder.CreateIndex(
                name: "UX_Reviews_OrderAuthor",
                schema: "dbo",
                table: "Reviews",
                columns: new[] { "OrderId", "AuthorUserId" },
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptItems_ProductId",
                schema: "dbo",
                table: "StockReceiptItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptItems_ReceiptId",
                schema: "dbo",
                table: "StockReceiptItems",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_CreatedBy",
                schema: "dbo",
                table: "StockReceipts",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_SupplierId",
                schema: "dbo",
                table: "StockReceipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "dbo",
                table: "Users",
                column: "Email",
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Users_FirebaseUid",
                schema: "dbo",
                table: "Users",
                column: "FirebaseUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_ProductId",
                schema: "dbo",
                table: "WishlistItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UQ_WrappingFeeTiers_Max",
                schema: "dbo",
                table: "WrappingFeeTiers",
                column: "MaxStems",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_Products_ProductId",
                schema: "dbo",
                table: "CartItems",
                column: "ProductId",
                principalSchema: "dbo",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DesignRequestStems_Products_ProductId",
                schema: "dbo",
                table: "DesignRequestStems",
                column: "ProductId",
                principalSchema: "dbo",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_Products_ProductId",
                schema: "dbo",
                table: "InventoryMovements",
                column: "ProductId",
                principalSchema: "dbo",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_StockReceiptItems_ReceiptItemId",
                schema: "dbo",
                table: "InventoryMovements",
                column: "ReceiptItemId",
                principalSchema: "dbo",
                principalTable: "StockReceiptItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_ProductPrices_ProductPriceId",
                schema: "dbo",
                table: "OrderItems",
                column: "ProductPriceId",
                principalSchema: "dbo",
                principalTable: "ProductPrices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                schema: "dbo",
                table: "OrderItems",
                column: "ProductId",
                principalSchema: "dbo",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImages_Products_ProductId",
                schema: "dbo",
                table: "ProductImages",
                column: "ProductId",
                principalSchema: "dbo",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPrices_Products_ProductId",
                schema: "dbo",
                table: "ProductPrices",
                column: "ProductId",
                principalSchema: "dbo",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.TR_ProductPrices_AppendOnly ON dbo.ProductPrices
                AFTER UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 50001, 'Price history is append-only.', 1;
                END;
                """);

            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.TR_OrderItems_LockPrice ON dbo.OrderItems
                AFTER UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF UPDATE(UnitPrice) OR UPDATE(Quantity) OR UPDATE(DiscountAmount) OR UPDATE(ProductId) OR UPDATE(ProductPriceId)
                        THROW 50003, 'Order item price and product fields are immutable.', 1;
                END;
                """);

            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_ActivePromotions AS
                SELECT p.* FROM dbo.Promotions p
                WHERE p.IsActive = 1
                  AND (p.StartDate IS NULL OR p.StartDate <= CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+07:00') AS DATE))
                  AND (p.EndDate IS NULL OR p.EndDate >= CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+07:00') AS DATE));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_ActivePromotions;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_DailyRevenue;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_ReleaseOrderStock;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_ReserveStock;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_SetProductPrice;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_Refunds_NotExceedPayment;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_OrderItems_LockPrice;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_ProductPrices_AppendOnly;");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPrices_Users_ChangedBy",
                schema: "dbo",
                table: "ProductPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPrices_Products_ProductId",
                schema: "dbo",
                table: "ProductPrices");

            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CartItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CustomerMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DesignRequestHistory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DesignRequestStems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InventoryMovements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrderItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrderStatusHistory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProductImages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProductTags",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PromotionProducts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RateLimitCounters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Refunds",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ReviewImages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ReviewProducts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "WishlistItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "WrappingFeeTiers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DesignRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "StockReceiptItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrderCustomBouquets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Reviews",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "StockReceipts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DeliveryZones",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Promotions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProductPrices",
                schema: "dbo");
        }
    }
}

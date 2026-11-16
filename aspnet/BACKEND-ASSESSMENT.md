# Đánh giá backend — phần 1

Đây là báo cáo tại mốc trước triển khai. Các phát hiện được xử lý dần trong cùng backend; xem [hiện trạng backend chính](MAIN-BACKEND-DEPLOYMENT.md) để tránh hiểu danh sách thiếu ban đầu là tình trạng hiện tại.

Ngày: 16/11/2026. Nguồn chính: worktree `aspnet-upgrade`, nhánh `codex/aspnet-sql-server`, HEAD `936547a`; nguồn tham khảo: worktree `backend-layered`, nhánh `refactor/backend-layered-layout`, HEAD `4ec5101`. Đánh giá bao gồm các thay đổi chưa commit tại thời điểm đọc, không chỉ nội dung HEAD.

## Quyết định và giới hạn

- Tiếp tục phát triển trong `aspnet-upgrade/aspnet`; không tạo backend thứ ba, không merge toàn bộ worktree khác.
- Giữ Firebase Authentication và Cloud Firestore theo `MIGRATION-STATUS.md`. SQL Server/EF hiện có được giữ nguyên để bảo toàn công việc đang làm, nhưng không dùng làm đích chuyển đổi mặc định.
- Kế thừa có chọn lọc mã và kiểm thử từ `backend-layered`, sau khi đối chiếu với JavaScript của chính `aspnet-upgrade`. Hai worktree có thể khác phiên bản nghiệp vụ.
- Giữ frontend, collection/document/field, URL, method, query/action, response, lỗi, header, phân quyền và ranh giới transaction. Chưa đổi routing triển khai, chưa xóa Node.js, chưa chạy EF migration.
- Đây là đánh giá cấu trúc, mã nguồn và baseline kiểm thử. Không phải chứng nhận tương đương mọi nhánh nghiệp vụ hay sẵn sàng production. Từng nhóm cần đối chiếu hành vi và kiểm thử emulator trước khi chuyển lưu lượng.

## Phát hiện ưu tiên

| Mức | Bằng chứng | Hệ quả / xử lý |
| --- | --- | --- |
| P0 | Api/Controllers và Authentication dùng `FlowerFlowDbContext`; Infrastructure chưa có Firestore adapter | Backend SQL chưa thể thay dữ liệu Firestore. Xây adapter Firestore, không chạy chuyển database. |
| P0 | `ProductsController` trả `name`, `code`, `tags` dạng object; JS giữ nguyên field document như `title`, `image` | Cùng wrapper `{products}` không có nghĩa tương đương. Viết contract serializer trước khi thay endpoint. |
| P0 | SQL lọc cả `hidden` và `archived`, sort theo Name; JS chỉ lọc `hidden`, không sort tương ứng | Cần giữ đúng hành vi nguồn hoặc tách thành thay đổi sản phẩm có chủ đích. |
| P0 | SQL dùng `manage=1` hoặc `true`; JS chỉ dùng `manage=1`; SQL role yêu cầu user SQL + claim role; JS kiểm tra admin claim + `adminRoles` | Khác điều kiện truy cập; cần test bảng quyền anonymous/customer/admin/owner/revoked/role bị gỡ. |
| P0 | Thiếu controller đơn hàng/thanh toán/IPN trong nguồn chính | Entity Order/Payment không chứng minh có checkout, giữ kho hay thanh toán hoạt động. |
| P1 | Hai controller truy cập EF trực tiếp; Application chỉ có OrderConfig | Đưa use case vào Application; Api chỉ xử lý HTTP. |
| P1 | `FirebaseBearerHandler` gộp lỗi DB/provider thành authenticate fail | Phân biệt credential sai với dịch vụ không sẵn sàng theo hợp đồng JS; không làm lộ bí mật. |
| P1 | Bản tham khảo Application tham chiếu Infrastructure, SDK Firestore và HttpContext | Chưa đạt ranh giới 4 tầng; không sao chép nguyên project rồi coi là hoàn thành. |
| P1 | Bản tham khảo có hai host `FlowerFlow.Api` và `FlowerFlow.Backend` | Test host in-memory không đủ chứng minh host Firestore tương đương. |
| P1 | Frontend còn thao tác Firestore trực tiếp, ví dụ `js/admin/message-store.js` | Thay API Node không đồng nghĩa toàn bộ nghiệp vụ đã đi qua ASP.NET; cần kiểm kê client SDK và security rules riêng. |

## Controller và Application trong nguồn chính

| Thành phần | Phần tốt | Cần sửa / còn thiếu |
| --- | --- | --- |
| ProductsController | GET async, AsNoTracking, phân biệt cache public/manage | Thay truy cập EF bằng use case; bảo toàn field và legacy image/title repair, route flower-prices, lỗi và quyền JS. |
| AccountController | Authorize, UID/email từ token, không nhận role từ body | `/api/account/me` và PUT profile không thay `/api/login`; cần tách persistence và xử lý tạo profile đồng thời; không tự chuyển profile SQL sang Firestore. |
| Program | DI, authentication trước authorization, exception handler, OpenAPI chỉ Development | Chưa có Firestore, adapter/use case registrations; `/health` chỉ liveness, không xác nhận Firestore. |
| FirebaseTokenVerifier | SDK Admin, checkRevoked=true, không log credential | Chuyển adapter sang Infrastructure; interface ở Application; kiểm thử khởi tạo/dispose nhiều host và lỗi cấu hình. |
| FirebaseBearerHandler | Kiểm tra Bearer, tài khoản inactive, không tin role từ request | Quyền phụ thuộc SQL khác adminRoles; tách xác thực/quyền; kiểm tra 401/403/503, challenge body và service failure. |
| OrderConfig | Đã có 9 export, thời gian milliseconds, khung giờ chỉ đọc | Chưa được nối vào use case; cần test giá trị với JS, chưa phải OrdersService. |

Nguồn chính không có LoginService, CatalogService, CheckoutService, PaymentsService hoặc repository nghiệp vụ khác. Không gán trạng thái “đã chuyển” cho các tên chỉ xuất hiện trong worktree tham khảo.

## Entity nguồn chính — đánh giá từng file

Các class dưới đây là mô hình quan hệ với property và navigation; không có use case thực thi. Domain không phụ thuộc EF là điểm tốt, nhưng ID số, bảng quan hệ và RowVer không phải schema Firestore. Không tuần tự hóa trực tiếp các entity này thành response thay thế JS.

| File | Đánh giá / việc cần làm |
| --- | --- |
| Catalog/Category.cs | Có nhóm và thứ tự; đối chiếu metadata/frontend category, không tự tạo collection mới. |
| Catalog/DeliveryZone.cs | Có vùng và phí; Firestore dùng metadata deliveryZones, cần adapter giữ ID và enabled. |
| Catalog/Product.cs | Có giá/kho/row version; Code/Name và ID số khác id/title document; tách mapping và invariants. |
| Catalog/ProductImage.cs | Có URL/bytes/content type; thiếu use case kiểm tra JPEG, kích thước và quyền upload/đọc. |
| Catalog/ProductPrice.cs | Có lịch sử giá; cần map priceHistory, owner authorization và audit cùng transaction. |
| Catalog/ProductTag.cs | Tag quan hệ không bảo đảm giống field tags JS; cần contract thực tế. |
| Catalog/WrappingFeeTier.cs | Có bậc phí; map metadata bouquetWrapping, kiểm tra bậc trùng/thứ tự. |
| Commerce/CartItem.cs | Có LineKey/options; cần đối chiếu giỏ local/Firestore, không dùng giới hạn SQL thay giới hạn checkout 50. |
| Commerce/CustomerMessage.cs | Có nội dung/reply; frontend đang dùng messages trực tiếp, nằm ngoài 11 API Node. |
| Commerce/DesignRequest.cs | Có token hash và quote; chưa có kiểm tra token, trạng thái, quyền hoặc quote/reply. |
| Commerce/DesignRequestHistory.cs | Có lịch sử; cần bảo đảm cập nhật request và history nguyên tử. |
| Commerce/DesignRequestStem.cs | Có số cành/giá; cần snapshot giá và chuẩn hóa lựa chọn theo JS. |
| Commerce/Order.cs | Nhiều field snapshot và reservation; chưa có state transition, idempotency, tính tiền hoặc giữ kho. |
| Commerce/OrderCustomBouquet.cs | Có nhóm bó và phí; cần giữ cách tính tổng cành, phí và line key. |
| Commerce/OrderItem.cs | Có snapshot sản phẩm và LineTotal private setter; giá trị tính ở SQL không tự hoạt động trong Firestore. |
| Commerce/OrderStatusHistory.cs | Có trạng thái/location; chưa có kiểm soát transition và quyền xem dữ liệu khách. |
| Commerce/Payment.cs | Có attempt/txn/ref; thiếu xác minh chữ ký, amount và liên kết attempt hiện hành. |
| Commerce/Promotion.cs | Có khoảng ngày/phần trăm; cần giữ ngày Việt Nam và quy tắc chọn ưu đãi. |
| Commerce/PromotionProduct.cs | Là bảng nối SQL; chưa phải mapping activePromotions hiện tại. |
| Commerce/Refund.cs | Có reference/amount; cần chống lặp và kiểm tra số tiền còn được hoàn trong transaction. |
| Commerce/Review.cs | Có moderation/order; cần ràng buộc chủ đơn đã giao và một review mỗi đơn. |
| Commerce/ReviewImage.cs | Có bytes/type; cần giới hạn ảnh và quyền đọc theo trạng thái review. |
| Commerce/ReviewProduct.cs | Là bảng nối; đối chiếu product IDs trong review document. |
| Commerce/WishlistItem.cs | Có liên kết user/product; cần kiểm kê client shopping sync trước khi chuyển. |
| Identity/User.cs | FirebaseUid tách identity; role/activation SQL chưa tương đương custom claims + adminRoles. |
| Operations/AuditLog.cs | Có actor/action; cần giữ adminAudit fields và timestamp server, không chỉ lưu JSON tùy ý. |
| Operations/InventoryMovement.cs | Có delta/stockAfter; cần ghi cùng transaction kho, không dùng thay cờ chống hoàn kho hai lần. |
| Operations/RateLimitCounter.cs | Có hash/expiry; thiếu scope/key/lease/transaction tương đương từng flow. |
| Operations/StockReceipt.cs | Có phiếu nhập; chưa có API/use case, kiểm kê ngoài phạm vi API Node. |
| Operations/StockReceiptItem.cs | Có lượng/giá vốn; chưa có cập nhật kho nguyên tử. |
| Operations/Supplier.cs | Có thông tin nhà cung cấp; chưa có API/use case, không suy ra đã hỗ trợ quản trị. |

## Infrastructure nguồn chính

| Thành phần | Đánh giá |
| --- | --- |
| Persistence/FlowerFlowDbContext.cs | Cấu hình 31 DbSet, nhiều FK/check/index, decimal(18,0), concurrency; có ích cho hướng SQL, không phải adapter Firestore. Giữ nguyên thay đổi chưa commit. |
| Persistence/FlowerFlowDbContextFactory.cs | Endpoint fallback cố ý không dùng được, hỗ trợ tạo model thiết kế; không chạy migration/update database trong công việc này. |
| Migrations/20261114150321_InitialSchema.cs và Designer, ModelSnapshot | Chỉ mô tả schema SQL. Không phải chuyển dữ liệu hoặc nghiệp vụ; chưa được xác nhận áp dụng lên SQL Server. |
| FlowerFlow.Infrastructure.csproj | Tham chiếu Application/Domain đúng chiều; có FirebaseAdmin nhưng chưa có Google.Cloud.Firestore. |
| Firebase/Firestore, VNPay, audit repository | Chưa có trong nguồn chính; phải bổ sung adapter và kiểm thử lỗi cấu hình trước API. |

## Đối chiếu toàn bộ module JavaScript trong backend nguồn chính

Cột C# là ứng viên tương ứng trong `backend-layered/flower.com/backend`, chưa phải mã đã tích hợp vào nguồn chính. Ngoại lệ duy nhất đã có trong nguồn chính là OrderConfig.

| JS (dưới flower.com/backend) | C# ứng viên | Điều kiện nghiệm thu |
| --- | --- | --- |
| shared/firebase-admin.js | Infrastructure/Firebase/FirebaseAdminService; Security/FirebaseRequestAuthenticator | revoked token, adminRoles, owner, lỗi cấu hình 503 |
| shared/request-identity.js | Infrastructure/Security/RequestIdentityService | local session và optional identity không vượt quyền |
| shared/admin-audit.js | Infrastructure/Audit/AdminAuditRepository | actor/details/server timestamp; audit cùng transaction |
| shared/api-routes.js | Infrastructure/Http/ApiRoutes | query route, default, route lạ, method/Allow |
| accounts/password-auth.js | Application/Accounts/PasswordAuthService | provider, public/server key, chặn bypass password login |
| accounts/login-protection.js | Application/Accounts/LoginProtectionService | lease, retry/lock tăng dần, nhiều request đồng thời |
| accounts/login.js | Application/Accounts/LoginService | reset/custom token, body/lỗi không lộ credential |
| admin/roles.js | Application/Admin/RolesService | đồng bộ claims và adminRoles, lỗi một phần |
| admin/users.js | Application/Admin/UsersService | quyền, pagination, route=audit |
| admin/owner-users.js | Application/Admin/OwnerUsersService | owner-only, cấm tự đổi owner, audit |
| admin/audit.js | Application/Admin/AuditService | quyền đọc, cursor/filter, serialization |
| catalog/products.js | Application/Catalog/ProductsService | giữ document fields, title/image repair, hidden/manage |
| catalog/flower-prices.js | Application/Catalog/FlowerPricesService | giá nguyên VND, owner, priceHistory và audit nguyên tử |
| catalog/image-rules.js | Application/Catalog/ImageRulesService | byte thực, JPEG, số ảnh, kích thước |
| catalog/product-images.js | Application/Catalog/ProductImagesService | upload auth, dữ liệu ảnh và cache/Content-Type |
| catalog/review-images.js | Application/Catalog/ReviewImagesService | review public/private, author/admin, ID ảnh |
| shop-settings/delivery-zones.js | Application/ShopSettings/DeliveryZonesService | public/manage, vùng trùng và metadata transaction |
| shop-settings/bouquet-wrapping.js | Application/ShopSettings/BouquetWrappingService | thứ tự bậc, fee và audit |
| orders/config.js | Application/Orders/OrderConfig (đã có nguồn chính) | đủ 9 export, đơn vị và Unicode khung giờ |
| orders/validation.js | Application/Orders/OrderValidationService | điện thoại, ngày Việt Nam, lịch giao, giỏ rỗng/giới hạn |
| orders/pricing.js | Application/Orders/PricingService | tính lại giá server, promotion boundary, vùng giao, phí bó |
| orders/checkout.js | Application/Orders/CheckoutService | quote/create, fingerprint/idempotency, chống vượt kho, rate limit |
| orders/reservations.js | Application/Orders/ReservationsService | quá hạn/cron, hoàn kho đúng một lần |
| orders/tracking.js | Application/Orders/TrackingService | mã + điện thoại, rate limit IP, mine, che PII |
| orders/payment-admin.js | Application/Orders/PaymentAdminService | QR/tiền đến trễ/refund, quyền và audit transaction |
| orders/reviews.js | Application/Orders/ReviewsService | chủ đơn đã giao, review một lần, ảnh nguyên tử |
| orders/controller.js | Api/Controllers/OrdersController + Application/Orders/OrdersService | toàn bộ action và method dispatch, CRON_SECRET |
| payments/vnpay.js | Infrastructure/Vnpay/VnpayGateway + Application/Payments/VnpayService | canonical query, HMAC-SHA512, compare constant time |
| payments/payments.js | Application/Payments/PaymentsService | tạo URL, status, attempt/retry và expiration |
| payments/vnpay-ipn.js | Application/Payments/VnpayIpnService | signature/amount/txn, callback lặp, IPN đến sau hết hạn |
| design-requests/validation.js | Application/DesignRequests/DesignRequestValidationService | access token, colors/options/stems, text/image limits |
| design-requests/controller.js | Api/Controllers/DesignRequestsController + Application/DesignRequests/DesignRequestsService | create/read, quote/reply, quyền và history |

Phụ thuộc ngoài backend phải đối chiếu: `js/catalog/product-store.js`, `bouquet-options.js`, `bouquet-wrapping.js`, `flower.json`, `lib/local-session.js`. Giữ frontend nguyên trạng; chuyển các quy tắc server sang C# với fixture đối chiếu.

## API và rewrite cần bảo toàn

11 entry point Node: `/api/login`, `/api/products`, `/api/product-images`, `/api/review-images`, `/api/delivery-zones`, `/api/orders`, `/api/payments`, `/api/vnpay-ipn`, `/api/design-requests`, `/api/admin/users`, `/api/owner/users`.

Ba rewrite: `/api/flower-prices` → products?route=flower-prices; `/api/bouquet-wrapping` → delivery-zones?route=bouquet-wrapping; `/api/admin/audit` → admin/users?route=audit. Phải hỗ trợ cả URL riêng và query route cũ. Cron `/api/orders?action=expire` cần cơ chế chạy thay thế khi đổi hosting.

Orders cần giữ POST quote/create/lookup/review/review-get/verify-qr-payment/record-late-qr-payment/record-payment-refund; GET mine/payment-audit/expire. Không tự thay thành REST endpoints mới làm frontend mất tương thích.

## Cách sử dụng bản tham khảo

- Các controller Accounts, Admin, Owner, Products, ProductImages, ReviewImages, DeliveryZones, Orders, Payments, VnpayIpn, DesignRequests hiện chỉ chuyển `HttpContext` tới service. Có đủ route là tốt; chưa tách transport khỏi nghiệp vụ.
- HealthController chỉ kiểm tra tiến trình. Không dùng kết quả 200 làm chứng cứ Firebase hoạt động.
- `FirestoreService` chỉ bọc GetAdminFirestore; `FirestoreTransactionService` bọc SDK transaction. Cần adapter có interface theo use case, giữ transaction boundary và retry đúng chỗ.
- `AdminAuditRepository` có transaction write và server timestamp để kế thừa; kiểm tra field contract trước khi chuyển.
- `VnpayGateway` có canonical encoding, HMAC và FixedTimeEquals để kế thừa; phải chạy fixture JS và IPN transaction, không chỉ test helper chữ ký.
- Nhóm Accounts/Admin/Catalog/Orders/Payments/DesignRequests/ShopSettings đang phụ thuộc SDK/HTTP trực tiếp; chuyển DTO/identity/errors sang Application, transport sang Api và adapter sang Infrastructure theo từng module.
- Nhóm `Commerce` và host `FlowerFlow.Backend` dùng abstractions/in-memory riêng; không coi các bài test của nhóm này là bằng chứng cho host `FlowerFlow.Api` dùng Firestore.
- Các chức năng chỉ thấy ở bản tham khảo như wilting-prices/logout-all cần kiểm tra chênh lệch phiên bản trước khi mang sang; không tự thêm vào hợp đồng nguồn chính.

## Kiến trúc đích và thứ tự thực hiện

Chiều tham chiếu: Application → Domain; Infrastructure → Application/Domain; Api → Application và Infrastructure để DI. Domain/Application không phụ thuộc ASP.NET, Firebase SDK hoặc EF. Interface được đặt theo nhu cầu use case, không tạo generic repository ép mọi transaction qua CRUD rời rạc.

| Phần | Công việc cụ thể | Điều kiện hoàn thành |
| --- | --- | --- |
| 1 | Đánh giá và thống nhất nguồn/dữ liệu (tài liệu này) | Có baseline build/test, bảng thiếu/khác và kế hoạch |
| 2 | Firebase/Firestore adapters, identity/role interfaces và lỗi | Test cấu hình thiếu, token revoked, role bị gỡ, 401/403/503; Application không import Infrastructure/SDK/HTTP |
| 3 | Products và serializer, sau đó ảnh/giá/catalog | Contract JS/C# cùng fixture; hidden/manage/legacy fields/cache/route/method |
| 4 | Delivery zones/bouquet wrapping | Metadata + audit nguyên tử, kiểm thử public/manage và dữ liệu sai |
| 5 | Accounts/login protection/admin/owner/audit | Auth emulator và test lock/lease đồng thời, owner safety, pagination |
| 6 | Validation/pricing/quote/checkout/tracking/reviews | Transaction emulator: cạnh tranh mua sản phẩm cuối, idempotency cùng/khác payload, hoàn kho lặp, quyền review |
| 7 | Payments/VNPay/IPN/reconciliation/refund | Callback lặp/sai chữ ký/sai tiền/attempt cũ/hết hạn, audit nguyên tử; sandbox riêng |
| 8 | Design requests và triển khai từng route | Token/quote/reply/history; E2E frontend, routing/cron/rollback kiểm chứng |

Mỗi phần triển khai cập nhật trạng thái và kết quả kiểm thử trước khi sang phần tiếp. Chỉ bỏ API JS tương ứng sau khi contract, emulator/E2E, chuyển routing và kiểm tra vận hành đã đạt; giữ khả năng rollback trong giai đoạn chuyển.

## Baseline kiểm chứng

- Build nguồn chính: `dotnet build aspnet/FlowerFlow.sln --no-restore --nologo` tại aspnet-upgrade: PASS, 0 warning, 0 error.
- Build tham khảo: `dotnet build flower.com/FlowerFlow.sln --no-restore --nologo` tại backend-layered: PASS, 0 warning, 0 error.
- Nguồn chính chưa có project C# test trong solution. Không báo `dotnet test` không tìm thấy test là PASS nghiệp vụ.
- Kết quả chạy test thực tế được ghi tại `../docs/test-reports/2026-11-16-backend-assessment.md`.
- Chưa kết nối Firebase thật, chưa chạy emulator, SQL Server, VNPay sandbox hoặc E2E; không đọc/in credential và không thay database.

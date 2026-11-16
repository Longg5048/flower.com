# Chuyển backend JavaScript sang C# theo từng file

**Cập nhật 16/11/2026:** các nhóm API production đã được nối qua ASP.NET/Firestore; các mô tả thiếu endpoint bên dưới là mốc đánh giá ban đầu. Hiện trạng triển khai và giới hạn staging được ghi ở [MAIN-BACKEND-DEPLOYMENT.md](MAIN-BACKEND-DEPLOYMENT.md) và [MAIN-BACKEND-WORK.md](MAIN-BACKEND-WORK.md). Chưa đổi routing production hoặc xóa API Node.

## Yêu cầu áp dụng

- Nguồn đối chiếu: `../flower.com/backend/`, các entry point `../flower.com/api/`, rewrite trong `vercel.json` và các module dùng chung được backend import.
- Giữ Firebase Authentication và Cloud Firestore hiện tại. Dùng Firebase Admin SDK cho xác thực và Google.Cloud.Firestore cho truy cập Firestore trong các bước tích hợp C#.
- Không chuyển dữ liệu sang SQL Server, tạo lại database, đổi collection/document/field hoặc chạy EF migration cho công việc chuyển đổi này.
- Giữ nguyên frontend, URL, HTTP method, query, body, response, mã lỗi, thông báo lỗi, header, điều kiện phân quyền và transaction.
- Không lưu service account vào mã nguồn, frontend hoặc tài liệu. Giữ cấu hình bí mật ở phía server.
- Mỗi bước chuyển một file nghiệp vụ, đối chiếu nguồn và kiểm tra trước khi chuyển file tiếp theo. Không xóa file JS hoặc chuyển lưu lượng khi các phụ thuộc và hợp đồng API chưa được kiểm chứng.

## Hiện trạng và phần cần phân tích tiếp

Đã kiểm kê 32 file trong `backend/`, rà soát import/export, điểm vào API và các collection được tham chiếu. Đây là kiểm kê ban đầu, **chưa phải xác nhận đã phân tích đầy đủ mọi nhánh nghiệp vụ**. Cần hoàn thành phân tích chi tiết toàn bộ backend cùng các module chia sẻ ngoài thư mục trước khi chuyển các luồng thực thi/API.

ASP.NET hiện có `ProductsController` và `AccountController` dùng SQL Server. Các endpoint này chưa tương đương backend Firestore và không được coi là đã chuyển đổi xong. Các model EF và migration hiện hữu không chứng minh rằng logic nghiệp vụ đã được chuyển.

Các nhóm cần đối chiếu chi tiết:

| Nhóm | File nguồn | Điểm cần bảo toàn |
| --- | --- | --- |
| shared | firebase-admin, request-identity, admin-audit, api-routes | Token bị thu hồi, claim admin và document adminRoles, owner, lỗi 401/403/503, session local, audit trong transaction, query route |
| accounts | password-auth, login-protection, login | Firebase password provider, kiểm tra public API key bị chặn, lease transaction, khóa tăng dần, reset mật khẩu, custom token |
| admin | roles, users, owner-users, audit | Claims và adminRoles, phân trang, không tự đổi quyền owner, audit |
| catalog | image-rules, product-images, review-images, products, flower-prices | Bytes/JPEG, quyền xem ảnh, chuẩn hóa sản phẩm/ảnh cũ, giá nguyên VND, owner và lịch sử giá |
| shop-settings | delivery-zones, bouquet-wrapping | Metadata hiện tại, vùng trùng, bậc phí, public/manage, audit nguyên tử |
| orders | config, validation, pricing, checkout, reservations, tracking, payment-admin, reviews, controller | Giá/khuyến mãi/phí, lịch Việt Nam, giới hạn giỏ hàng, idempotency, chống vượt kho, thời hạn giữ hàng, hoàn kho một lần, tra cứu, đối soát/hoàn tiền, quyền đánh giá |
| payments | vnpay, payments, vnpay-ipn | Canonical query/HMAC, attempt/retry, idempotency IPN, kiểm tra tiền, hết hạn và hoàn kho |
| design-requests | validation, controller | Token truy cập, lựa chọn thiết kế, quote/reply, quyền và lịch sử |

Backend còn import module frontend dùng chung như `product-store.js`, `bouquet-options.js`, `bouquet-wrapping.js`, cùng `lib/local-session.js`. Cần chuyển phần quy tắc dùng ở server mà vẫn giữ nguyên các file frontend. Các API rewrite `/api/flower-prices`, `/api/bouquet-wrapping`, `/api/admin/audit` và dạng `?route=` cần được đối chiếu riêng.

## File 01: orders/config.js

Đích: `src/FlowerFlow.Application/Orders/OrderConfig.cs`.

Đã chuyển đủ 9 export, giữ nguyên giá trị và đơn vị milliseconds:

| JavaScript | C# | Giá trị |
| --- | --- | --- |
| MAX_QUANTITY_PER_PRODUCT | MaxQuantityPerProduct | 50 |
| QR_RESERVATION_MS | QrReservationMs | 1800000 |
| VNPAY_RESERVATION_MS | VnpayReservationMs | 900000 |
| COD_RESERVATION_MS | CodReservationMs | 1800000 |
| ORDER_LOOKUP_WINDOW_MS | OrderLookupWindowMs | 600000 |
| ORDER_LOOKUP_MAX_ATTEMPTS | OrderLookupMaxAttempts | 20 |
| DELIVERY_TIME_WINDOWS | DeliveryTimeWindows | 08:00–11:00; 11:00–14:00; 14:00–17:00; 17:00–20:00 |
| DELIVERY_TIME_ZONE | DeliveryTimeZone | Asia/Ho_Chi_Minh |
| MAX_ORDER_NOTE_LENGTH | MaxOrderNoteLength | 500 |

Danh sách khung giờ giữ đúng ký tự en dash của nguồn và được cung cấp dạng chỉ đọc. File không truy cập database, không thay đổi endpoint và chưa được nối vào các luồng C# khác. Việc chuyển file này chưa có nghĩa API đơn hàng đã chạy bằng C#.

Kiểm tra: `dotnet build aspnet-upgrade/aspnet/src/FlowerFlow.Application/FlowerFlow.Application.csproj --no-restore --nologo` thành công, 0 warning và 0 error. Đây là kiểm tra biên dịch, chưa phải kiểm tra tích hợp Firebase hoặc xác nhận tương đương API.

Bước tiếp theo tại thời điểm chuyển OrderConfig: hoàn thành phân tích chi tiết backend trước khi chuyển các file có logic thực thi; ưu tiên nền tảng Firebase/Firestore và phân quyền trước các controller.

## Cập nhật 16/11/2026 — phần 1 và phần 2

- Đã lập [đánh giá cấu trúc, entity và bảng đối chiếu 32 module JS](BACKEND-ASSESSMENT.md). Đây không phải chứng nhận tương đương mọi nhánh nghiệp vụ.
- Đã triển khai [nền tảng Firebase/Firestore và identity](FIREBASE-FOUNDATION.md): interface/quy tắc ở Application, SDK/đọc adminRoles ở Infrastructure, HTTP adapter ở Api. Verifier SDK cũ trong Api đã được thay bằng adapter qua interface.
- Có project `tests/FlowerFlow.Foundation.Tests`, contract oracle đọc module JS thật, test cấu hình/lifecycle/HTTP/host và test role reader trên Firestore Emulator.
- Chưa đổi Products/Account sang Firestore; phân quyền SQL cũ vẫn còn ở các endpoint SQL. Chưa chuyển routing hay xóa API Node.
- Kết quả và giới hạn: [báo cáo phần 2](../docs/test-reports/2026-11-16-firebase-foundation.md). Phần tiếp theo là Products/serializer, giữ nguyên hợp đồng JS trước khi triển khai.

## Cập nhật phần 3 — danh sách Products

Đã thay ProductsController SQL bằng use case Application và reader Firestore, serializer giữ field động, ID/timestamp và sửa title/image legacy theo flower.json được nhúng lúc build. Route Firestore bỏ qua bộ xác thực SQL cũ; quyền manage giữ đúng thứ tự và thông báo của products.js.

Đã thêm oracle HTTP đối chiếu 29 tình huống với module JS thật và test HTTP/reader trên Firestore Emulator. Xem [chi tiết chuyển đổi](PRODUCTS-MIGRATION.md) và [kết quả kiểm thử](../docs/test-reports/2026-11-16-products-migration.md).

Chỉ chuyển nhánh danh sách; `route=flower-prices` trả 503 api-not-migrated cho đến khi chuyển module giá hoa riêng. Chưa đổi routing production và chưa bỏ API Node. Bước tiếp theo là flower-prices: quyền Owner, transaction cập nhật giá + priceHistory + adminAudit.

## Cập nhật tiếp theo — flower-prices

Đã chuyển `catalog/flower-prices.js` sang Application/FlowerPricesService và Infrastructure/FirestoreFlowerPriceStore; Api xử lý chung cả `/api/flower-prices` và `/api/products?route=flower-prices`. Đã gỡ response api-not-migrated của phần 3. GET list/history và PATCH giữ hợp đồng JS, phân quyền Owner và ba write nguyên tử; no-op không ghi lịch sử/audit.

Đã thêm oracle 46 tình huống trên cả hai URL và kiểm thử emulator cho commit/no-op/rollback/concurrent updates/revoked role. Xem [triển khai](FLOWER-PRICES-MIGRATION.md) và [báo cáo](../docs/test-reports/2026-11-16-flower-prices-migration.md). Chưa đổi routing/xóa Node API. Tiếp theo: quy tắc ảnh và ProductImages/ReviewImages.

## Cập nhật tiếp theo — ảnh sản phẩm và ảnh đánh giá

Đã chuyển image-rules.js, ProductImages GET/POST và ReviewImages GET: quy tắc ở Application, binary persistence ở Infrastructure, HTTP ở Api. Giữ SHA-1 URL, create-only dedup, quyền upload/adminRoles và quyền đọc review theo moderation/author/admin/photoIds/reviewId.

Có oracle 17 dimension + 23 validation + 46 HTTP tình huống; emulator kiểm chứng binary, upload trùng/đồng thời, metadata không bị ghi đè và quyền ảnh review; thêm kiểm thử bounded retry ABORTED/cancellation ở SDK boundary. Xem [triển khai](IMAGES-MIGRATION.md) và [báo cáo](../docs/test-reports/2026-11-16-images-migration.md).

Chưa chuyển submit review trong Orders, chưa đổi routing production/xóa Node. Tiếp theo: DeliveryZones và BouquetWrapping, giữ metadata/audit nguyên tử.

# Phần 3 — GET Products dùng Firestore

Triển khai 16/11/2026, trong worktree aspnet-upgrade. Chỉ chuyển phần danh sách của `backend/catalog/products.js`; không chuyển `catalog/flower-prices.js` trong phần này.

## Thay đổi đã có hiệu lực trong host ASP.NET

- `ProductsController` không còn truy vấn SQL hoặc trả DTO name/code cố định. Route `/api/products` gọi Application/ProductsService, Infrastructure/FirestoreProductReader đọc collection `products`.
- Application giữ interface reader/default catalog, serializer và quy tắc danh sách; không import Firebase/Firestore/ASP.NET/EF. Api xử lý HTTP method/query/header/status; Infrastructure giữ SDK và chuyển các kiểu dữ liệu Firestore sang JSON.
- `flower.json` được nhúng vào assembly Infrastructure tại build, dùng làm nguồn sửa title/image cũ giống JS. Không cần đường dẫn frontend ngoài container sau publish. Đổi catalog cần build/publish lại backend.
- Route Products mang metadata FirestoreEndpoint. FirebaseBearerHandler bỏ qua route này trước khi đọc token hoặc SQL; service Products tự kiểm tra quyền Firestore khi `manage=1`. Account SQL vẫn dùng cơ chế xác thực cũ. `UseRouting` đặt trước authentication để handler thấy metadata.
- Giữ nguyên mọi API Node, frontend, vercel.json và routing triển khai. Chưa chuyển lưu lượng production.

## Hành vi được bảo toàn

| Trường hợp | Kết quả |
| --- | --- |
| GET public | `{products:[...]}`, giữ field document, không sort theo tên, chỉ lọc `status === hidden`; archived vẫn trả về |
| Token trên request public | Không xác thực token, không đọc role, không chạm SQL |
| manage=1 duy nhất | Xác thực token với revoked check, đọc adminRoles trước khi xét admin claim; chỉ admin boolean true + document tồn tại mới được xem toàn bộ |
| manage=true / query manage lặp | Public như JS; tên query phân biệt hoa/thường, không gộp Manage với manage |
| Thiếu/sai token | 401 với code/thông báo từ JS |
| Không có claim admin hoặc role bị gỡ | 403 với thông báo riêng của Products |
| Lỗi role/product read | 503 product-service-unavailable, không trả provider detail |
| Lỗi khởi tạo connection | 503 admin-api-not-configured trước khi xét quyền |
| Public cache | public, max-age=0, s-maxage=0, must-revalidate |
| Manage cache | private, no-store |
| Method khác GET | 405, Allow: GET, JSON code/error |
| route lạ/rỗng/lặp | 404 api-not-found, no-store, dispatch trước kiểm tra method |

Serializer giữ ID có sẵn kể cả 0; ID null/thiếu dùng document ID. Timestamp cấp đầu trả ISO UTC với milliseconds; timestamp lồng giữ JSON `_seconds/_nanoseconds` của Node. Title chứa dấu `?` và ảnh legacy chỉ được sửa khi tìm thấy sản phẩm trong catalog; chỉ lọc images legacy khi chính image đầu là legacy. Không sửa document hoặc đối tượng nguồn.

Adapter xử lý maps/arrays, null, bytes/Blob theo Buffer JSON Node, GeoPoint, số nguyên theo kiểu Number của JS và non-finite doubles thành null. DocumentReference chưa nằm trong phạm vi schema đã kiểm thử: adapter từ chối và service trả 503 thay vì tuần tự hóa object SDK. Cần kiểm kê dữ liệu thật trước chuyển lưu lượng.

## Nhánh giá hoa và giới hạn triển khai

Giới hạn 503 api-not-migrated của phần 3 đã được gỡ ở bước tiếp theo: cả `/api/products?route=flower-prices` và `/api/flower-prices` nay chạy chung use case giá hoa. Xem [chuyển API giá hoa](FLOWER-PRICES-MIGRATION.md) về quyền Owner, lịch sử giá và transaction/audit. Routing production vẫn giữ nguyên trên Node đến khi kiểm thử triển khai/frontend hoàn tất.

Chưa xác nhận E2E frontend, credential production, dữ liệu catalog thật hoặc mọi kiểu dữ liệu tùy ý của Firestore. Bộ contract so sánh trên fixtures cố định; emulator kiểm chứng adapter SDK trên dữ liệu thử. Không có database migration hoặc thao tác Firebase thật.

Kết quả: [báo cáo kiểm thử Products](../docs/test-reports/2026-11-16-products-migration.md).

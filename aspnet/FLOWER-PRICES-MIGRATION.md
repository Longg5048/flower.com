# Chuyển API giá hoa — phần tiếp theo của Catalog

Ngày triển khai: 16/11/2026. Nguồn đối chiếu: `flower.com/backend/catalog/flower-prices.js`, `shared/admin-audit.js` và `toVndPrice` trong `js/catalog/product-store.js` của chính worktree aspnet-upgrade.

## Đã triển khai

- Hai dạng URL dùng cùng HTTP adapter và use case: `/api/flower-prices` và `/api/products?route=flower-prices`.
- GET không có flowerId: đọc sản phẩm `saleType == flower`, serialize các field giá và sắp xếp tên theo tiếng Việt.
- GET có flowerId: kiểm tra ID, kiểm tra sản phẩm hoa tồn tại, đọc priceHistory của đúng hoa và trả mới nhất trước.
- PATCH: kiểm tra ID và giá nguyên VND từ 1.000 đến 1.000.000.000; tính giá cũ bằng cùng quy tắc quy đổi USD legacy của JS; từ chối giá hiện tại không hợp lệ.
- Giá không đổi trả changed=false và không ghi dữ liệu. Giá thay đổi ghi sản phẩm + một priceHistory + một adminAudit trong một Firestore transaction; timestamp cả ba dùng server timestamp.
- Application sở hữu quy tắc và ranh giới transaction qua IFlowerPriceStore/IFlowerPriceTransaction. Infrastructure triển khai SDK; Api xử lý HTTP. Domain/Application không import Firebase SDK, Firestore, EF hay ASP.NET.
- TimeProvider cho phép kiểm thử thời gian xác định; ngày lưu priceUpdatedAt tính theo Asia/Ho_Chi_Minh, response changed=true trả thời điểm request UTC millisecond như JS.

## Phân quyền và hợp đồng HTTP

1. Kiểm tra method trước xác thực; chỉ GET/PATCH. Method khác trả 405 và Allow: GET, PATCH.
2. Kiểm tra cấu hình Auth trước Bearer, như getAdminAuth của JS. Thiếu/sai cấu hình trả 503 admin-api-not-configured.
3. Token không hợp lệ/bị thu hồi trả 401. Owner cần claim admin boolean true và role owner; sai claim trả 403 với thông báo riêng quản lý giá.
4. Đọc lại document adminRoles/{uid}, yêu cầu role owner. Quyền bị gỡ/đổi trả 403; lỗi khởi tạo Firestore/đọc role trả 503 price-service-unavailable với thông báo xác minh quyền.
5. Lỗi list/history/transaction trả 503 price-service-unavailable với thông báo tải/cập nhật giá; không trả provider detail.

Mọi response của nhánh giá hoa có Cache-Control: private, no-store. Query key phân biệt hoa/thường; flowerId lặp không hợp lệ. Cả hai route bỏ qua SQL bearer handler và tự áp dụng quyền Firestore. Không dùng role SQL thay cho adminRoles.

Không thay các hành vi nguồn dễ bị hiểu nhầm: ID cho phép dấu backslash, chỉ cấm slash/control/trim và độ dài quá 128 UTF-16; giá dạng chuỗi không được dùng làm giá mới nhưng giá cũ dạng chuỗi được chuyển bằng Number/toVndPrice; không xóa field priceUpdatedByEmail cũ trên product. Email trong response thay đổi/lịch sử/audit theo actor hiện tại như JS.

## Kiểm thử

Oracle chạy module JS thật với ranh giới SDK giả và thời gian cố định. 46 tình huống chạy trên cả hai URL, so sánh status/body/headers/số lần đọc quyền-token và dữ liệu ghi dự kiến. Firestore Emulator kiểm chứng việc commit, không ghi khi no-op, rollback khi lỗi sau staging, quyền bị gỡ và hai cập nhật đồng thời tạo chuỗi giá nhất quán.

Xem [báo cáo kiểm thử](../docs/test-reports/2026-11-16-flower-prices-migration.md) để biết kết quả cuối và lệnh chạy lại.

## Giới hạn và bước tiếp theo

Chưa đổi routing triển khai, chưa xóa API Node, chưa chạy với Firebase thật hoặc E2E frontend. Kiểm thử revoked token vẫn mô phỏng ở ranh giới verifier; adapter production bật checkRevoked=true theo phần nền tảng. Collation tiếng Việt đã đối chiếu trên fixtures; cần giữ kiểm tra trên môi trường hosting và dữ liệu thực trước chuyển lưu lượng.

Quyền Owner được đọc trước transaction như JS, không được tự nâng thành bảo đảm quyền không thể bị gỡ giữa lúc transaction đang chạy. Transaction bảo đảm nguyên tử ba thay đổi dữ liệu giá, không bao gồm cập nhật Firebase custom claims.

Bước kế tiếp của Catalog: ProductImages/ReviewImages và quy tắc kiểm tra ảnh; sau đó cấu hình giao hàng/phí gói bó. Chỉ loại bỏ API Node tương ứng sau kiểm thử frontend, cấu hình hosting và chuyển routing được kiểm chứng.

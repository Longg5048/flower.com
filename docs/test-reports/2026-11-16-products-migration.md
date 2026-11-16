# Kiểm thử phần 3 — Products

Ngày: 16/11/2026. [Phạm vi triển khai và giới hạn](../../aspnet/PRODUCTS-MIGRATION.md).

## Lệnh kiểm tra

```powershell
$env:FLOWERFLOW_TEST_FIRESTORE_HOST = '127.0.0.1:8797'
dotnet test aspnet/FlowerFlow.sln --no-restore --nologo --verbosity minimal
```

Chạy tại aspnet-upgrade với Firestore Emulator v1.22.0/Java 21 trên loopback. Project Products là `demo-products-<UID ngẫu nhiên>`; tạo ba document rồi dọn cả ba trong finally. Test role của phần 2 vẫn chạy với project demo riêng. Không dùng Firebase thật.

**Kết quả cuối: PASS — 31 passed, 0 failed, 0 skipped, tổng 31; khoảng 9 giây chạy test.** Solution được biên dịch trước khi test, không báo warning/error. Bài contract Products chứa 29 tình huống bên trong một test, không cộng nhầm thành 29 bài test độc lập. Bộ test phần 2 tiếp tục đạt.

## Phạm vi

- Oracle Node thực thi module `flower.com/backend/catalog/products.js` hiện tại, dùng ranh giới SDK mô phỏng. Một bài contract so sánh **29 tình huống** với HTTP thực trong ASP.NET TestServer: status, JSON body, Cache-Control, Allow và số lần đọc token/role/products.
- Tình huống gồm public, bearer không hợp lệ trên public, manage=true, manage lặp/hoa-thường, thiếu token, revoked mô phỏng, customer, admin claim kiểu chuỗi, quyền bị gỡ, admin/owner, role outage kể cả customer, thiếu cấu hình trước kiểm tra token, product outage, method POST/DELETE/OPTIONS, query route lạ/rỗng/lặp, route dispatch trước method, giỏ catalog rỗng và Bearer tab.
- Fixtures serializer gồm field tùy biến, tags array, status hidden/archived, ID null/0/khác document ID, sửa title/image theo flower.json, images chỉ được lọc khi image legacy, timestamp cấp đầu/lồng. So sánh với nguồn JS thật thay vì viết lại expected bằng C#.
- Một bài emulator chạy HTTP public/manage qua reader SDK Firestore thật; kiểm tra document ID order không bị thay bằng sort tên, hidden/archived, timestamp/nested timestamp, bytes/Blob và GeoPoint.
- Kiểm tra serializer không thay đối tượng nguồn; host không có credential trả đúng JSON cấu hình 503; nhánh flower-prices trả lỗi chưa chuyển đổi và không đọc Products.
- Fixture HTTP cố ý cấu hình SQL không dùng được; đếm token calls để bắt việc middleware SQL cũ chạy nhầm trên route Products.

## Vấn đề phát hiện và đã sửa

1. Authentication middleware tự động của WebApplication vẫn chạy khi chỉ gọi UseAuthentication trong nhánh UseWhen. Đã đăng ký middleware rõ ràng và kiểm tra metadata ngay trong bearer handler; public Products không đọc token/SQL, manage chỉ xác minh một lần.
2. SDK Firestore trả Blob cho bytes. Đã bổ sung mapping Blob → Buffer JSON như Node và kiểm chứng bằng emulator.
3. ASP.NET Query collection gộp tên query không phân biệt hoa/thường. Controller đọc query qua QueryStringEnumerable để giữ đúng phân biệt chữ hoa/thường và query lặp của JS.
4. HttpClient có thể chuẩn hóa thứ tự Cache-Control khi đọc typed headers. Contract đọc NonValidated header để so sánh giá trị gốc; không đổi header runtime chỉ để làm test xanh.

## Giới hạn

Phần danh sách đã kiểm thử; nghiệp vụ flower-prices **chưa chuyển**, trả 503 api-not-migrated ở nhánh query. Chưa chạy E2E frontend, Firebase thật hoặc đối chiếu toàn bộ dữ liệu production; DocumentReference chưa được hỗ trợ như dữ liệu sản phẩm. Không xóa Node API, sửa vercel.json hay chuyển lưu lượng triển khai.

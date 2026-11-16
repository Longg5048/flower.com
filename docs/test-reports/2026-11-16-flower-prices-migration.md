# Kiểm thử chuyển API giá hoa

Ngày: 16/11/2026. Worktree: aspnet-upgrade. [Chi tiết triển khai](../../aspnet/FLOWER-PRICES-MIGRATION.md).

## Cách chạy

```powershell
$env:FLOWERFLOW_TEST_FIRESTORE_HOST = '127.0.0.1:8797'
dotnet test aspnet/FlowerFlow.sln --no-restore --nologo --verbosity minimal
```

Firestore Emulator v1.22.0, Java 21, endpoint loopback. Các test giá hoa dùng project demo-prices-<UID> riêng, không có credential thật; dọn products/adminRoles/priceHistory/adminAudit trong finally/DisposeAsync. Các bài nền tảng và Products tiếp tục chạy cùng solution.

**Kết quả cuối: PASS — 37 passed, 0 failed, 0 skipped, tổng 37; khoảng 28 giây chạy test.** Lần chạy cuối biên dịch đủ năm project, không báo warning/error. Bài oracle giá hoa chứa 92 lượt so sánh bên trong một test; không cộng chúng vào số 37.

## Contract JavaScript/C#

Một bài test thực thi 46 tình huống qua cả URL `/api/flower-prices` và `/api/products?route=flower-prices` (92 lượt so sánh HTTP), không phải 92 bài test độc lập.

- Quyền: thiếu token, token revoked mô phỏng, admin thường, Owner thiếu admin, claim admin dạng chuỗi, quyền Owner bị gỡ/đổi; đếm token/role reads để bắt xác thực SQL chạy nhầm.
- Lỗi: cấu hình Auth, khởi tạo store, role outage, list outage, transaction outage; method không hợp lệ được xử lý trước Auth.
- List: chỉ flower, giá cũ USD/string/null, tên tiếng Việt, timestamp và field fallback.
- History: lọc đúng hoa, sắp xếp mới nhất trước, email/UID, numeric conversion; ID thiếu/rỗng/lặp/slash/control/BOM/quá dài và giới hạn 128, backslash, ký tự NEL không thuộc whitespace JS; query key hoa/thường.
- PATCH: changed/no-op, legacy USD đổi/không đổi, giá hiện tại sai, hoa không tồn tại/bouquet, body rỗng/null, giá min/max/chuỗi/bool/lẻ/ngoài giới hạn, email rỗng/quá 320.
- So sánh toàn bộ status, JSON body, Cache-Control, Allow và ba bản ghi dự kiến (product/history/audit). Source oracle dùng module JS thật, admin-audit thật với FieldValue giả; không gọi Firebase thật.
- Đồng hồ cố định 2026-11-16T18:30:00.123Z kiểm tra ngày Việt Nam chuyển sang 2026-11-17. SDK transaction vẫn dùng server timestamp khi chạy emulator.

Hai bài HTTP riêng kiểm tra JSON hỏng: trả lỗi 400 đã làm sạch cho Owner, vẫn trả 401 khi chưa đăng nhập, không ghi dữ liệu. Đây là quy tắc transport của ASP.NET; không khẳng định response platform parser của Vercel giống hệt khi JSON đầu vào bị hỏng.

## Transaction trên emulator

1. HTTP PATCH qua URL gộp cập nhật giá, giữ quantity, có đúng một history và audit; server timestamps bằng nhau, actor/details đúng. PATCH cùng giá qua URL riêng không đổi UpdateTime, không thêm history/audit; GET history đọc được dữ liệu; xóa Owner role khiến PATCH tiếp theo bị 403 và giá không đổi.
2. SDK transaction đọc sản phẩm, stage cả ba write rồi ném lỗi trước commit: giá cũ giữ nguyên, history và audit đều rỗng.
3. Hai HTTP PATCH đồng thời trên cùng sản phẩm: cả hai thành công; có hai history và hai audit; oldPrice của lần sau bằng newPrice của lần trước, giá cuối khớp chuỗi lịch sử, không có history/audit thừa do retry.

## Giới hạn

Chưa chạy Firebase production, Auth Emulator, E2E frontend, hosting hoặc đổi routing. Không chạy EF migration, không sửa schema dữ liệu và không loại bỏ Node API. Không kiểm chứng mọi chuỗi ngày/locale tùy ý ngoài fixtures; dữ liệu mới dùng server timestamp chuẩn. Quyền Owner nằm ngoài transaction như source JS, không bảo đảm chặn một thay đổi quyền xảy ra giữa lúc transaction đang chạy.

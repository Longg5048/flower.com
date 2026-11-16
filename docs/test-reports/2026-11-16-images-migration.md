# Kiểm thử quy tắc ảnh và API ảnh

Ngày: 16/11/2026. [Chi tiết triển khai](../../aspnet/IMAGES-MIGRATION.md).

## Cách chạy

```powershell
$env:FLOWERFLOW_TEST_FIRESTORE_HOST = '127.0.0.1:8797'
dotnet test aspnet/FlowerFlow.sln --no-restore --nologo --verbosity minimal
```

Firestore Emulator v1.22.0, Java 21, loopback. Nhóm ảnh dùng project demo-images-<UID> riêng và dọn adminRoles/productImages/reviews/reviewImages khi test kết thúc. Không có credential thật hoặc thao tác lên Firebase production.

**Kết quả cuối: PASS — 50 passed, 0 failed, 0 skipped, tổng 50; khoảng 36 giây chạy test.** Solution biên dịch đủ năm project trước khi chạy, không báo warning/error. Các tình huống oracle nằm trong hai bài test, không cộng 17/23/46 vào số 50. Các bài nền tảng, Products và giá hoa tiếp tục đạt.

## Đối chiếu JavaScript

- Một bài kiểm thử chạy module image-rules.js thật: **17 trường hợp dimension** và **23 trường hợp validation**. Có ảnh start.jpeg thật; baseline/progressive/extended SOF; APP/standalone/repeated markers; SOI/EOI/segment sai; base64 thiếu/thừa padding/newline; kiểu dữ liệu sai; 0–3–4 ảnh; đúng 120 KiB và vượt một byte; 1600 và 1601 px; zero dimension. So sánh dimension, kết quả null/exception, số byte và SHA-256 của ảnh được chấp nhận.
- Một bài HTTP chạy **46 tình huống** qua Products/Review image handlers JS thật: status, JSON lỗi/URL hoặc binary base64, Content-Type media type, Content-Length ảnh, cache, Allow, nosniff, số lần đọc token/role/review/photo, số create và dữ liệu ghi.
- Bao phủ public bỏ qua token lỗi; upload auth/claim/role/config/outage/ảnh sai/trùng; review approved/author/admin/anonymous, revoked token mô phỏng, role bị gỡ/outage, photoIds sai/không chứa ảnh, photo.reviewId sai, ảnh thiếu và binary sai.
- Oracle res.json mô phỏng đúng việc đặt lại Content-Type JSON trong lib/local-server.js: tránh so sánh nhầm header image/jpeg còn sót từ response đang lỗi khi fixture json giả không cập nhật header. Không sửa runtime chỉ để làm xanh test.
- Hai bài JSON hỏng bảo đảm auth trước validation, không lộ raw input và không ghi ảnh. Hai bài request cancellation bảo đảm không đổi việc hủy request thành lỗi ảnh 503.

## Kiểm thử Firestore SDK/emulator

1. Upload ảnh start.jpeg thật; ID đúng SHA-1 và URL tương thích. Đọc Blob bytes/size/uploadedBy/server timestamp trong Firestore. Tạo trùng với uploader khác và hai POST đồng thời vẫn trả URL thành công, chỉ một document, UpdateTime không đổi. GET trả đúng bytes/header, bỏ qua token/Range như source. Gỡ role khiến POST tiếp theo trả 403, document giữ nguyên.
2. Review pending: anonymous 404, tác giả không cần adminRoles vẫn 200; đổi tác giả khiến admin đã bị gỡ quyền nhận 404; cấp lại role đọc được. Liên kết reviewId sai trả 404; approved công khai nhưng private,no-store và không đọc token; bỏ photoIds hoặc xóa review làm ảnh không còn truy cập được.

## Kiểm thử retry ở ranh giới SDK

Năm bài dùng FirestoreClient Commit giả, không gọi mạng: ABORTED→thành công; ABORTED→ALREADY_EXISTS; PermissionDenied không retry/không nuốt; ABORTED kéo dài dừng sau năm lần; cancellation trong backoff dừng lần create tiếp theo. Kiểm tra request giữ precondition exists=false và payload không đổi.

SDK Node có retry ABORTED trong WriteBatch.commit; đã bổ sung backoff tương ứng ở adapter C#. Một lần thử upload đồng thời trước đó có response 503; sau thay đổi bộ concurrency và retry được chạy lại. Không coi mọi lỗi ghi là duplicate.

## Giới hạn

Chưa kiểm chứng Firebase thật, revoked token thật, browser E2E và cấu hình hosting. Không chuyển nghiệp vụ tạo review, không chạy migration và không đổi routing/xóa API Node. Parser JPEG chỉ tương đương kiểm tra marker/dimension của nguồn, không xác nhận ảnh luôn giải mã được trên mọi trình duyệt.

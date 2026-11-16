# Chuyển quy tắc ảnh và API ảnh

Triển khai ngày 16/11/2026, worktree aspnet-upgrade. Nguồn đối chiếu: `backend/catalog/image-rules.js`, `product-images.js`, `review-images.js` và helper xác thực hiện có.

## Phạm vi đã chuyển

- `Application/Catalog/ImageRules`: đọc marker JPEG, kiểm tra kích thước, data URL/base64 chuẩn, tối đa 120 KiB và 1600×1600; helper ảnh đánh giá tối đa ba ảnh. Default bỏ qua tham số trả mảng rỗng; truyền null/non-array bị từ chối, như JS.
- `ProductImagesService`: quyền upload, kiểm tra ảnh, tạo ID SHA-1 tương thích URL cũ, public GET theo ID. Đây là định danh nội dung có sẵn của hệ thống, không dùng làm thuật toán mật khẩu.
- `ReviewImagesService`: kiểm tra moderation, tác giả/admin, danh sách photoIds và quan hệ reviewId trước khi trả ảnh.
- `Infrastructure/Firebase/FirestoreImageStore`: đọc/ghi binary Firestore, create-only, server timestamp; không dùng Cloud Storage và không đổi collection/field.
- Api có `/api/product-images` (GET/POST), `/api/review-images` (GET), trả binary JPEG hoặc JSON lỗi. Hai route mang metadata FirestoreEndpoint để bỏ qua xác thực SQL cũ.

Application không tham chiếu SDK/HTTP/EF. Các interface và model lưu trữ ảnh ở Application; Firestore adapter ở Infrastructure; cache/method/headers/body parsing ở Api. Không sửa frontend, không chuyển hosting/routing production và không xóa Node API.

## Quyền và response

| Nhánh | Hành vi |
| --- | --- |
| Product GET | ID 40 ký tự hex thường; công khai, không xác thực token kể cả header token không hợp lệ; ảnh không có trả 404 |
| Product POST | Auth trước; đọc adminRoles trước khi kiểm tra admin boolean true; role document phải tồn tại, không bắt buộc role string owner |
| Upload sai dữ liệu | Sau khi đủ quyền mới kiểm tra ảnh; invalid-photo 400, giữ thông báo cũ JPG/PNG/WebP dù dữ liệu gửi lên phải là JPEG đã chuyển đổi ở client |
| Upload trùng | 201 và URL cũ; không ghi đè bytes/uploadedBy/createdAt/size của ảnh đã tồn tại |
| Review approved | Không đọc token/role; vẫn yêu cầu photoIds chứa ID và photo.reviewId khớp document review |
| Review chưa approved | Tác giả được xem; người khác cần admin boolean true và adminRoles tồn tại. Thiếu quyền trả 404 để không lộ dữ liệu |
| Review token lỗi | Với review chưa approved và có Bearer, trả 401 unauthenticated, “Phiên đăng nhập hết hạn.”; lỗi cấu hình Auth ở nhánh này cũng theo hợp đồng đó |
| Lỗi store/role | 503 photo-unavailable với thông báo riêng Product/Review; không đưa provider detail vào response |

Cả hai API đặt X-Content-Type-Options: nosniff. Product GET thành công có Content-Type image/jpeg, Content-Length theo bytes và cache public 1 năm immutable. Review luôn private, no-store, kể cả approved. GET không bật xử lý Range (giữ trả toàn bộ ảnh như JS). Method sai trả 405 và Allow tương ứng. Query id phân biệt hoa/thường; query lặp chuyển thành chuỗi có dấu phẩy như String(array) của JS và bị từ chối bởi định dạng ID.

## Lưu trữ, đồng thời và retry

Product upload tạo document `productImages/{sha1}` gồm bytes, size, uploadedBy, createdAt server timestamp. Adapter dùng CreateAsync với precondition document chưa tồn tại; chỉ ALREADY_EXISTS được coi là upload trùng thành công. Không chuyển sang Set/upsert.

SDK Node hiện tại thêm ABORTED vào retry của WriteBatch.commit (`node_modules/@google-cloud/firestore/build/src/write-batch.js`). Adapter C# bổ sung tối đa năm lần Create, delay 50/100/200/400ms cho ABORTED, tôn trọng cancellation. Hết retry hoặc lỗi khác vẫn thất bại; không tự trả 201. Kiểm thử RPC giả xác nhận giữ cùng request/precondition, không nuốt PermissionDenied và dừng khi request hủy.

Ảnh review được tạo bởi nghiệp vụ gửi đánh giá trong Orders của Node hiện tại. Phần này chỉ chuyển GET ảnh review và helper validation; **chưa chuyển nghiệp vụ submit review hoặc API Orders**.

## Kiểm chứng và giới hạn

Xem [báo cáo kiểm thử ảnh](../docs/test-reports/2026-11-16-images-migration.md). Có đối chiếu module JS thật, ảnh JPEG thật, giới hạn byte/dimension, HTTP binary/headers/quyền và SDK Firestore trên emulator.

Parser JPEG giữ đúng kiểm tra marker/dimension cũ, không phải bộ giải mã JPEG đầy đủ. Dữ liệu ảnh trong Firestore được kỳ vọng là binary do các API hiện tại tạo; Blob/byte[]/ByteString được đọc thành bytes, dữ liệu lưu sai kiểu trả lỗi đã làm sạch. Chưa chạy Firebase thật, browser E2E hoặc chuyển routing. Chưa thể bỏ API Node chỉ dựa vào bộ test này.

Bước tiếp theo: DeliveryZones/BouquetWrapping, giữ metadata hiện có, public/manage, kiểm tra dữ liệu và audit nguyên tử.

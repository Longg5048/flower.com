# Tiến độ nâng ASP.NET thành backend chính

Yêu cầu đang thực hiện: hoàn thiện toàn bộ backend ASP.NET, kiểm thử trước khi chuyển routing và loại bỏ từng API Node. Nguồn chính là aspnet-upgrade; giữ Firebase Authentication/Firestore, không chạy EF migration. Các thay đổi DbContext/migration tồn tại trước công việc được giữ nguyên.

## Đã triển khai và kiểm chứng theo nhóm

- Firebase/identity; Products; flower-prices; image rules/ProductImages/ReviewImages: xem các báo cáo trước, baseline 50 test đạt.
- DeliveryZones/BouquetWrapping: cấu hình public/manage, upsert/delete, duplicate areas kể cả disabled, tier validation, metadata và audit cùng transaction; hỗ trợ cả rewrite. Oracle 47 trường hợp và hai bài emulator commit/concurrent/rollback đạt. Ba bài kiểm thử nhóm cấu hình đạt.
- Login/password protection: API /api/login; provider server OAuth/password, kiểm tra khóa API public ở hai endpoint trước đăng nhập; khóa tăng dần, lease Firestore, cấp custom token sau kiểm tra policy. Oracle sáu chuỗi tình huống HTTP/state; 15 bài provider dùng HTTP giả và hai bài emulator leases/HTTP lockout đạt. Nhóm login/password/settings tổng 21 bài đạt.
- Admin/Owner/Audit: POST users/page, owner grant/remove, role mirror/backfill, revoke session, audit và lỗi từng phần; hỗ trợ audit rewrite. Oracle 35 tình huống cùng ba bài Firestore SDK/emulator đã đạt. Chưa gọi Firebase Admin Auth thật trong đợt kiểm thử.
- Orders: đã nối checkout/quote, idempotency, giới hạn tạo/tra cứu đơn, giữ/hoàn kho, theo dõi và đơn tài khoản, đánh giá kèm ảnh, đối soát QR/thanh toán trễ/hoàn tiền/audit. Oracle 34 chuỗi tình huống so cả HTTP và toàn bộ dữ liệu lưu đã đạt. Adapter Firestore đã sửa đường dẫn SDK tuyệt đối thành đường dẫn tài liệu tương đối; kiểm thử SDK thật bắt được lỗi này.
- Payments: tạo/tái dùng/retry link VNPay, trạng thái và IPN; oracle 25 chuỗi kiểm tra HTTP và state, canonical query/signature/URL đã đạt, gồm IPN lặp/sai tiền/chữ ký/đến trễ và nhiều lần thanh toán.
- DesignRequests: create, capability lookup, quản trị danh sách/báo giá, accept/reject; oracle HTTP/state 24 chuỗi đã đạt. Giữ hash của access token, không giữ token gốc hoặc trả hash ra public.
- Commerce SDK/emulator: kiểm tra đồng thời tranh kho, cùng idempotency key, dọn đơn hết hạn, IPN/refund, quote acceptance và review/photos đã đạt theo nhóm. Trình duyệt Chromium đã chạy frontend order-store hiện tại qua Kestrel và SDK Firestore cho quote/create/replay/lookup/link VNPay; Firebase Auth phía trình duyệt được thay bằng guest stub, không phải xác thực production.
- Host: phục vụ frontend trong publish; security headers; proxy tin cậy; worker mỗi phút bật theo cấu hình; readiness kiểm tra Firestore/login/VNPay/config. Release publish và smoke HTTP local đã đạt, readiness trả 503 đúng khi thiếu cấu hình. Có Dockerfile, env template, publish/smoke script và hướng dẫn chuyển/quay lại.
- Bộ JavaScript hiện hữu: `npm.cmd test` đạt 233/233 trên Node 26.13.0; engines của dự án yêu cầu Node 24.x, cần lặp lại trong CI Node 24 khi chốt staging.

## Kết quả cuối và phần phụ thuộc staging

1. Đã chốt ngày 17/11: toàn solution **97/97 đạt, 0 bỏ qua**; Release publish và smoke artifact đạt. Báo cáo: [2026-11-17-main-backend.md](../docs/test-reports/2026-11-17-main-backend.md). Lỗi contention được tái hiện, sửa và chạy lại; không dùng kết quả xanh cũ để che lượt lỗi.
2. Cấp secret/domain/proxy trên staging; triển khai composite indexes và chờ READY.
3. Kiểm thử Firebase Auth thật và VNPay sandbox callback thật; xác nhận frontend và scheduler trên host đã chọn.
4. Chỉ chuyển traffic thật khi cấu hình và kiểm thử staging đạt; chưa có API Node nào bị xóa hoặc rewrite production bị đổi. Quy trình ở [MAIN-BACKEND-DEPLOYMENT.md](MAIN-BACKEND-DEPLOYMENT.md).

## Quy tắc cần giữ

- Không sao chép nguyên Application của backend-layered: nhiều service của bản đó phụ thuộc HttpContext/Firestore và chứa wilting-pricing không có ở nguồn chính.
- Application/Domain không tham chiếu Infrastructure/SDK/ASP.NET. Dùng interface theo use case, giao transaction boundary cho nghiệp vụ.
- Giữ JS error body/status/header/query/action, quy tắc server price, idempotency, atomic stock/payment/audit. Dùng oracle JS thật và Firestore Emulator project demo riêng.
- SDK .NET/Node khác retry ABORTED: ảnh đã có retry bounded; tuyệt đối không thay create bằng upsert để né lỗi trùng.
- Cập nhật 17/11: đã bắt được `RpcException Aborted / Transaction lock timeout` trong commit login lease. Bốn transaction adapter (login, commerce, cấu hình, giá hoa) dùng `FirestoreTransactions`: tối đa ba vòng SDK, backoff có jitter và hủy theo request; chỉ thử lại ABORTED, chạy lại cả đọc/kiểm tra/ghi. Không thử lại Unavailable/AlreadyExists/PermissionDenied hoặc ghi lại commit không rõ kết quả. Password provider và custom token vẫn nằm ngoài transaction.
- Các callback chỉ đọc và không stage ghi kết thúc bằng rollback của SDK rồi trả kết quả đã đọc. Tránh commit rỗng tranh khóa khi nhiều yêu cầu cùng trả login-in-progress, order replay, IPN replay hoặc giá không đổi; không thêm ghi giả, không sửa timestamp và không bỏ transaction cho những thay đổi thật.
- Login store có 128 semaphore stripe cố định để hạn chế tranh khóa của cùng tài khoản trong một adapter. Gate chỉ bao quanh transaction, không giữ trong lúc kiểm tra mật khẩu/gọi provider. Firestore lease vẫn quyết định quyền đăng nhập giữa các adapter/instance; bài kiểm thử lease dùng hai adapter độc lập để xác nhận điều này.
- Không log body/password/token/provider response. Password HttpClient đã bỏ loggers và redirect; production không bật Auth Emulator.
- IP limiter của ASP.NET phải dùng RemoteIpAddress sau trusted-proxy middleware, không tin X-Forwarded-For do caller tự đặt.

Emulator loopback 127.0.0.1:8797 được dùng với project demo riêng và dọn tài liệu kiểm thử. Credential production chưa được dùng và không có deployment nào được thực hiện.

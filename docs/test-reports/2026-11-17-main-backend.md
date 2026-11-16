# Hoàn thiện ASP.NET cho vai trò backend chính

Ngày kiểm chứng cuối: 17/11/2026, múi giờ Asia/Bangkok. Worktree: `aspnet-upgrade`. Giữ Firebase Authentication/Firestore, frontend và schema hiện tại; không chạy EF migration hoặc chuyển routing production.

## Phần đã triển khai

- Đã nối toàn bộ nhóm API production hiện hữu: login, admin/owner/audit, sản phẩm/giá hoa/ảnh, khu vực giao/phí bó, đơn hàng/đánh giá/đối soát, VNPay/IPN và yêu cầu thiết kế. Bảng endpoint và class nằm trong [hướng dẫn backend chính](../../aspnet/MAIN-BACKEND-DEPLOYMENT.md).
- Application/Domain độc lập HTTP/Firestore/SDK. Infrastructure thực thi port nghiệp vụ và transaction; Api chuyển request/response và cấu hình host. Các model/DbContext/migration SQL cũ được bảo toàn, không dùng làm dữ liệu cho những API đã chuyển.
- Đơn hàng giữ giá server, hạn giữ kho, idempotency, rate limit và hoàn kho một lần. VNPay kiểm tra chữ ký, tiền/tài khoản giao dịch, xử lý callback lặp, giao dịch cũ và thanh toán trễ. Đánh giá cùng ảnh ghi nguyên tử; yêu cầu thiết kế chỉ lưu hash mã truy cập và không giữ tồn kho.
- Host phục vụ static frontend cùng origin, security headers, proxy IP tường minh, worker hết hạn theo cấu hình và readiness. Đã có Release artifact, Dockerfile, template secret, script publish/smoke và workflow CI riêng.
- Bổ sung composite index `orderPaymentAudit(orderId, createdAt desc)`; chưa triển khai index bên ngoài.

## Lỗi phát hiện và sửa

1. SDK trả document reference path đầy đủ; truyền trực tiếp vào `FirestoreDb.Document` gây lỗi tạo đơn. Adapter Commerce chuyển sang đường dẫn tương đối. Bộ giả lập ban đầu không bắt được; kiểm thử SDK/Emulator đã bắt được.
2. Giao dịch đồng thời có thể hết retry của SDK và trả `ABORTED / Transaction lock timeout`. Tái hiện được bằng 12 yêu cầu đăng nhập cùng tài khoản và yêu cầu tạo đơn lặp. Chỉ tăng retry chưa đủ: phần sửa cuối gồm kết thúc callback không ghi bằng rollback, retry ABORTED có giới hạn và gate có 128 stripe cố định trong login store. Gate không giữ khi gọi password provider, không thay lease Firestore giữa các adapter/host.
3. Thử lại luôn chạy lại đọc/kiểm tra/ghi, không commit lại dữ liệu cũ; lớp bổ sung không thử lại Unavailable/AlreadyExists/PermissionDenied và tôn trọng cancellation. Không ghi giả, cập nhật timestamp hoặc thay create bằng upsert để làm test đạt.
4. API thiết kế khi thiếu cấu hình trả lỗi JSON 503 theo hợp đồng nguồn; các lỗi provider/SDK không được trả nguyên nội dung ra client. Quan sát stack trong quá trình chẩn đoán chỉ nằm ở test với dữ liệu demo.

Phân tích cơ chế dựa trên [tài liệu contention của Firestore](https://firebase.google.com/docs/firestore/transaction-data-contention), [hướng dẫn ABORTED](https://docs.cloud.google.com/firestore/native/docs/use-rest-api) và [mã nguồn SDK .NET về rollback khi callback kết thúc bằng exception](https://github.com/googleapis/google-cloud-dotnet/blob/main/apis/Google.Cloud.Firestore/Google.Cloud.Firestore/FirestoreDb.cs). Đây là căn cứ cho xử lý adapter; kết quả local không khẳng định mọi mức tải production đều không có contention.

## Kiểm chứng

- Oracle gọi trực tiếp JavaScript hiện tại, thay ranh giới Firebase/time/random; các chuỗi kiểm tra cả HTTP và dữ liệu lưu, không chỉ snapshot response: Orders 34, Payments 25, DesignRequests 24; cùng các oracle identity/catalog/images/settings/login/admin và pure rules.
- SDK Firestore Emulator với project demo tách biệt: tranh sản phẩm cuối; 12 request cùng idempotency key; expiry chạy đồng thời; IPN/refund lặp; accept báo giá đồng thời; review/ảnh; giá hoa/history/audit; settings; login lease và khóa mật khẩu. Bài lease chạy hai adapter độc lập, không dựa vào một gate local để chứng minh nguyên tử.
- Chromium dùng frontend `order-store.js` hiện tại → HTTP Kestrel → SDK Firestore cho quote/create/replay/tracking/link VNPay. Auth phía browser là guest stub, token phía test host là fixture; không có đăng nhập Firebase production trong bài này.
- Có kiểm tra ranh giới assembly, proxy tin cậy, trạng thái thiếu cấu hình, retry đúng loại lỗi và cancellation.
- Bộ JavaScript hiện hữu đã đạt **233/233** trong lượt 16/11. Máy local dùng Node 26.13.0 trong khi engines yêu cầu 24.x; workflow mới dùng Node 24 nhưng chưa được chạy trên GitHub.
- `dotnet publish -c Release` đã tạo `artifacts/main-backend`; không có mã server, `.env`, rules hoặc node_modules trong wwwroot. Smoke Kestrel xác nhận asset/header và đường dẫn nội bộ bị 404; readiness phải 503 khi chưa cấp secret.

**Lượt cuối toàn solution: 97/97 đạt, 0 thất bại, 0 bỏ qua; thời gian test 1 phút 5 giây.** TRX tại `artifacts/test-results/main-backend.trx`. Lệnh chạy:

```powershell
$env:FLOWERFLOW_TEST_FIRESTORE_HOST='127.0.0.1:8797'
dotnet test aspnet/FlowerFlow.sln --no-restore --nologo --verbosity minimal --logger 'trx;LogFileName=main-backend.trx' --results-directory artifacts/test-results
```

Release được publish lại sau phần sửa contention. Smoke artifact cuối đạt; `/health/ready` trả 503 đúng do không cấp secret. Kiểm tra YAML của workflow, cấu hình emulator JSON, cấu trúc publish và `git diff --check` đã đạt. Emulator/host kiểm thử được dừng sau lượt xác minh.

## Giới hạn trước khi chuyển traffic

Chưa dùng credential production; chưa gọi Firebase Admin Auth thật, gateway VNPay thật hoặc callback sandbox của nhà cung cấp. Emulator không xác nhận composite index production đã READY. Docker image và workflow GitHub mới chưa được build/chạy trên hạ tầng bên ngoài trong phiên này. `/api/local-auth/*` vẫn thuộc môi trường phát triển Node, không phải hệ thống tài khoản Firebase production được chuyển đổi.

Cần cấp cấu hình/domain/proxy và index trên staging, đạt readiness, thử Firebase Auth thật, frontend đầy đủ và VNPay sandbox trọn vòng. Chỉ sau đó mới chuyển orders/payments/IPN cùng nhau, giữ routing Node để quay lại, rồi loại bỏ API Node sau giai đoạn theo dõi. Quy trình cụ thể ở [MAIN-BACKEND-DEPLOYMENT.md](../../aspnet/MAIN-BACKEND-DEPLOYMENT.md).

# Phần 2 — Nền tảng Firebase/Firestore và phân quyền

Triển khai ngày 16/11/2026, tiếp theo [đánh giá phần 1](BACKEND-ASSESSMENT.md).

## Những gì đã được nối vào ứng dụng

- `FirebaseBearerHandler` dùng `IFirebaseTokenVerifier` của Application thay vì một class SDK trong Api. Đã chuyển phần xác minh token sang `Infrastructure/Firebase/FirebaseIdentityAdapter.cs` và bỏ package FirebaseAdmin trực tiếp khỏi project Api.
- `FirebaseConnection` quản lý một FirebaseApp riêng cho mỗi connection, khởi tạo lazy, dùng chung credential/project cho Auth và Firestore. Không đọc/ghi document khi khởi động host. Dispose hai host độc lập không xung đột tên FirebaseApp.
- `Program.cs` đăng ký connection, verifier, Firestore role reader, Application identity service và HTTP adapter qua DI.
- Hai endpoint SQL hiện tại vẫn dùng `FirebaseBearer` và các điều kiện SQL cũ; không thay việc khóa user SQL bằng quyền Firestore. Đây chưa phải chuyển Products/Account sang Firestore.

## Quy tắc cho các API Firestore sẽ chuyển tiếp theo

`Application/Identity/RequestIdentityService` không nhận HttpContext hay SDK object. Interface verifier và role reader cũng ở Application; Infrastructure triển khai chúng. Api gọi `RequestIdentityHttpService.RequireAsync(context, RequiredAccess.Admin/Owner/Customer)` rồi dừng action nếu kết quả null. Adapter HTTP ghi `{code,error}`, status và `Cache-Control: no-store` khi thất bại; thành công không ghi response.

Service mới đã đăng ký nhưng **chưa thay phân quyền của controller SQL và chưa có API nghiệp vụ nào được chuyển**. Khi chuyển từng controller phải kiểm tra cả authentication middleware hiện tại để tránh truy vấn SQL không cần thiết trên route Firestore; không dùng role SQL làm bằng chứng quyền Firestore.

| Yêu cầu | Điều kiện |
| --- | --- |
| Customer | Bearer token hợp lệ, SDK `checkRevoked: true`; không đọc role |
| Admin | Token có `admin === true` (không nhận chuỗi hoặc số), document `adminRoles/{uid}` tồn tại |
| Owner | Điều kiện Admin + claim `role == owner` + document `role == owner` |
| Quyền bị gỡ | Không cache document quyền; request tiếp theo đọc lại và trả 403 |
| Token sai/bị thu hồi | 401 `unauthenticated` |
| Thiếu/sai cấu hình connection | 503 `admin-api-not-configured` |
| Đọc quyền thất bại | 503 `authorization-service-unavailable` |
| Request bị hủy | Truyền cancellation, không đổi thành lỗi nghiệp vụ |

Theo JS hiện tại, một document quyền tồn tại nhưng thiếu field role vẫn đủ cho Admin, **không đủ cho Owner**. Token/provider verification lỗi trả 401 theo JS; không tự đổi thành 503. Contract tests thực thi module JS thật để giữ cả thông báo tiếng Việt và số lần đọc quyền.

## Cấu hình và bí mật

- Ưu tiên `FIREBASE_SERVICE_ACCOUNT_JSON` trong secret store phía server. `Firebase:ProjectId` hoặc `FIREBASE_PROJECT_ID` có thể ghi đè project từ service account; nếu không có thì lấy `project_id` của JSON.
- Host tiếp tục cho phép Application Default Credentials khi không có JSON và đã cấu hình project, như verifier ASP.NET cũ. JSON sai không tự fallback sang ADC. ADC hoạt động thực tế chưa được kiểm thử trong đợt này.
- Không đặt credential trong frontend/appsettings/tài liệu. Options đã che nội dung khi gọi ToString; exception cấu hình không giữ inner exception từ JSON/SDK.
- Connection chạy production-only cho Firestore; `FIRESTORE_EMULATOR_HOST` không tự chuyển nó sang emulator. Firebase Auth từ chối biến `FIREBASE_AUTH_EMULATOR_HOST` để tránh vô tình nhận token emulator không ký. Test emulator dùng adapter reader với connection thử nghiệm riêng, không bật bỏ xác thực trong API production.
- Không tạo endpoint debug danh tính hay public database probe mới. `/health` vẫn chỉ là liveness.

## Kiểm thử và cách chạy lại

```powershell
dotnet restore aspnet/FlowerFlow.sln
dotnet test aspnet/FlowerFlow.sln --no-restore --nologo
```

Chạy tại worktree aspnet-upgrade, cần Node để chạy oracle JavaScript. Oracle kế thừa từ bản backend-layered nhưng đọc **module JavaScript của aspnet-upgrade**, không phụ thuộc worktree tham khảo khi chạy. Test dùng UTF-8 khi đọc stdout/stderr trên Windows.

Test Firestore được đánh dấu skip rõ ràng nếu không có emulator. Khởi động Firestore Emulator riêng bằng công cụ Firebase hiện có, sau đó:

```powershell
$env:FLOWERFLOW_TEST_FIRESTORE_HOST = '127.0.0.1:8797'
dotnet test aspnet/FlowerFlow.sln --no-restore --nologo
```

Test chỉ nhận địa chỉ loopback, dùng project `demo-flowerflow-foundation` và document UID ngẫu nhiên; dọn document sau test. Không dùng service account thật cho emulator.

Chi tiết kết quả: [báo cáo phần 2](../docs/test-reports/2026-11-16-firebase-foundation.md).

## Phần còn lại

Chưa kiểm chứng token bị thu hồi trên Firebase thật; trường hợp đó được mô phỏng ở ranh giới verifier, còn production adapter bật kiểm tra revoked của SDK. Có test SDK từ chối token sai định dạng mà không truy cập mạng. Chưa chạy Auth Emulator, ADC thật hoặc kiểm thử vận hành production.

Chưa chuyển login/password protection, transaction checkout, audit, VNPay hoặc dữ liệu SQL. Phần tiếp theo: Products/serializer với contract response, cache, method, legacy title/image và quyền manage trước khi đổi routing hoặc loại API Node.

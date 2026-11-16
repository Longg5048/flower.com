# Kiểm thử phần 2 — Firebase/Firestore foundation

Ngày: 16/11/2026, worktree aspnet-upgrade. [Thay đổi và hướng dẫn](../../aspnet/FIREBASE-FOUNDATION.md).

## Kết quả cuối cùng

```powershell
$env:FLOWERFLOW_TEST_FIRESTORE_HOST = '127.0.0.1:8797'
dotnet test aspnet/FlowerFlow.sln --no-restore --nologo --verbosity minimal
```

**PASS: 26 passed, 0 failed, 0 skipped, tổng 26; thời gian test khoảng 5 giây.** Lệnh biên dịch cả Domain, Application, Infrastructure, Api và test project trước khi chạy. Không có cảnh báo/lỗi biên dịch được báo trong lần chạy này.

Restore solution thành công, sử dụng các version đã có trong dự án/bản tham khảo: FirebaseAdmin 3.7.0, Google.Cloud.Firestore 4.6.0, Microsoft.AspNetCore.Mvc.Testing 10.0.14, xUnit 2.9.3.

## Phạm vi đã kiểm chứng

| Nhóm | Bằng chứng |
| --- | --- |
| JS/C# contract | Một bài chạy 24 tình huống từ module `flower.com/backend/shared/firebase-admin.js` thật: thiếu/sai Bearer, customer, token revoked mô phỏng, admin bool/string/number, role mất/thiếu/outage, Owner claim/document mismatch. So sánh UID/status/body/code/thông báo, token truyền cho verifier và số lần đọc role. |
| Role revocation | Unit test đọc lại role ở request thứ hai sau khi gỡ quyền, không cache. |
| Firestore adapter thật | Emulator: document chưa có → admin → owner → document thiếu field role → xóa. Reader dùng SDK Firestore thực, dữ liệu test trong project demo và UID ngẫu nhiên, dọn document sau test. |
| Cấu hình | JSON thiếu/rỗng/sai/null/thiếu field/sai loại credential trả lỗi đã làm sạch, không có inner exception chứa dữ liệu bí mật. |
| Connection lifecycle | Hai FirebaseApp độc lập trong cùng process, tái sử dụng connection, dispose idempotent. Key RSA tự sinh trong bộ nhớ, không dùng credential thật. |
| SDK token | Firebase SDK thực từ chối chuỗi token sai định dạng, không cần mạng. Production adapter gọi VerifyIdTokenAsync với checkRevoked=true. |
| Môi trường Auth | Connection production từ chối FIREBASE_AUTH_EMULATOR_HOST được kế thừa, tránh nhận unsigned token ngoài ý muốn. Biến môi trường test được khôi phục. |
| HTTP adapter | 401/403/503, JSON đúng hai field code/error, Content-Type application/json, no-store; thành công không ghi response. |
| Host thật trong TestServer | API khởi động không cần database/credential; /health trả 200; /api/account/me không token/token revoked mô phỏng trả 401, token hợp lệ mô phỏng nhưng thiếu SQL trả 503. Verifier được thay ở ranh giới DI để không truy cập Firebase thật. |
| Kiến trúc | Application không tham chiếu Infrastructure, FirebaseAdmin, Google.Cloud.Firestore, ASP.NET Http abstractions hoặc EF. |
| Cancellation | Request cancellation trong verifier/role reader được truyền ra, không gộp thành lỗi truy cập. |

## Môi trường emulator

Firestore Emulator v1.22.0 đã có sẵn trên máy, chạy bằng Java 21, host 127.0.0.1:8797, project `demo-flowerflow-foundation`. Test nhận endpoint qua biến riêng `FLOWERFLOW_TEST_FIRESTORE_HOST`; chỉ cho phép loopback. Production FirebaseConnection không dùng cấu hình emulator của test. Không ghi/xóa document của Firebase thật.

Lần chạy trước khi bật emulator hiển thị skip rõ ràng cho bài emulator; kết quả cuối cùng ở trên đã bật emulator và không có bài bị skip.

## Vấn đề phát hiện và đã sửa

Lần contract đầu tiên đọc stdout Node theo encoding mặc định Windows, khiến tiếng Việt bị sai và bài so sánh thất bại. Đã đặt StandardOutputEncoding/StandardErrorEncoding=UTF8; bộ contract hiện so sánh đúng chuỗi gốc. Không sửa thông báo JS để làm test xanh.

## Giới hạn

- Token revoked được mô phỏng ở interface verifier; chưa thu hồi một user/token trên Firebase thật hoặc Auth Emulator. Test malformed-token không thay cho test revoked trên dịch vụ thật.
- Chưa kiểm thử ADC thật, network outage thực hoặc vận hành production. Trường hợp role outage được mô phỏng trong contract/HTTP tests.
- Chưa chuyển nghiệp vụ Products/Account khỏi SQL, chưa đổi authentication của các controller SQL sang Firestore. Các test host giữ guard thiếu SQL có chủ đích.
- Chưa kiểm thử transaction checkout/audit/VNPay/E2E và không có Node API nào được xóa hoặc chuyển routing trong phần này.
- Các thay đổi DbContext/migration/OrderConfig tồn tại trước phần này được giữ nguyên; không chạy EF migration hay đổi dữ liệu.

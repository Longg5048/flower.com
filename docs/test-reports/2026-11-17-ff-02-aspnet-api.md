# FF-02 — Kiểm thử API ASP.NET Core C#

Issue: #157. Ngày: 17/11/2026, Asia/Bangkok.
Baseline: `c38c2f8bd6119267e87c3f06bd65cf4d5bd7db4f` cùng thay đổi contract test trong nhánh này.
Thực hiện sau khi xác minh Docker/Render ở [FF-01](2026-11-17-ff-01-docker-render.md).

## Kết quả

| Bộ kiểm tra | Kết quả |
| --- | --- |
| Build solution Release | Đạt, 0 warning, 0 error |
| FlowerFlow.Backend.Tests (host độc lập `/api/v1`, in-memory) | 58/58 đạt, 0 bỏ qua; TRX local `artifacts/ff-02/ff-02-independent.trx` |
| FlowerFlow.Contracts + Firestore Emulator | Exit 0; toàn bộ nhóm contract/configuration, HTTP host và SDK transaction đạt |
| test-api.cjs → HTTP → FlowerFlow.Api → Auth/Firestore Emulator | 103/103 đạt, 0 bỏ qua; 62,94 giây |

103 test gồm cả Rules/SDK/backup, không phải 103 endpoint C#. `FLOWER_API_URL`
được runner truyền cho các bài dùng `h.callApi`, chuyển request qua HTTP tới DLL C#;
không chạy nhầm handler Node cho các request này. Test migration cố ý gọi Node để
tạo đơn trước rồi replay sang C# nhằm kiểm tra tương thích.

## Ma trận phạm vi host Firebase đang dùng cho frontend

| Endpoint | Nhóm kiểm chứng |
| --- | --- |
| `/health` | Khởi động host và smoke Render |
| `/api/login` | PasswordContracts, FirestoreContracts: validation, khóa/lease, provider error và custom token; route HTTP |
| `/api/auth/logout-all` | firebase-logout, security-api; contract lỗi token/thu hồi và tách tài khoản |
| `/api/products` | product-management; catalog defaults, public/manage, lưu sửa và xung đột |
| `/api/flower-prices` | flower-prices; Owner, giá hợp lệ, lịch sử/audit nguyên tử |
| `/api/wilting-prices` | wilting-prices; quyền, cấu hình, precedence, stale checkout, snapshot |
| `/api/product-images` | product-images; upload, validation, quyền, nội dung ảnh, deduplication |
| `/api/review-images` | review-moderation + FirestoreContracts; quyền, bytes, headers |
| `/api/delivery-zones` | delivery-zones; public/manage, validation, CRUD/audit |
| `/api/bouquet-wrapping` | aspnet-migration + FirestoreContracts; mặc định, validation và atomic audit |
| `/api/orders` | feature-orders, system, aspnet-migration; quote/create/replay, tracking/review, cron, tồn kho/expiry |
| `/api/payments` | vnpay + PaymentsContracts; link, retry, reuse, capabilities, stock restoration |
| `/api/vnpay-ipn` | vnpay + IpnContracts; chữ ký/số tiền, lặp/đến muộn và trạng thái giao dịch |
| `/api/design-requests` | FirestoreContracts + system/security suites; estimate, token, quote, accept, không giữ kho |
| `/api/admin/users` | feature-admin + FirestoreContracts; quyền, pagination, whitelist |
| `/api/admin/audit` | FirestoreContracts; quyền, giới hạn, serialization và alias |
| `/api/owner/users` | feature-admin + FirestoreContracts; grant/remove, live role, owner protection và partial failure |

ApiHostContracts kiểm tra dependency graph/JSON response của 16 route API trên
host thật. Bổ sung route wilting-prices bị thiếu và không chấp nhận 404 hoặc 500.
503 là kết quả hợp lệ trong bài host không có Firebase credential này; kiểm tra
nghiệp vụ có emulator được chạy riêng bằng bộ HTTP integration.

Trong quá trình bổ sung assertion, điều kiện thử nghiệm `<500` đã làm nhóm
`--api-host` thất bại ở design-requests vì thiếu credential trả 503 đúng thiết kế.
Đã sửa assertion theo hợp đồng trên và chạy lại `--api-host`: exit 0. Đây là lỗi
assertion trong lúc chỉnh test, không thay đổi API để che kết quả.
Ma trận này mô tả phạm vi endpoint; không tuyên bố bao phủ mọi tổ hợp đầu vào.

## Tái chạy

Từ gốc repository, có .NET 10, Node, Java 21 và dependency cài bằng
`npm ci --prefix flower.com` cùng `npm ci --prefix flower.com/tests`:

```sh
dotnet build flower.com/FlowerFlow.sln --configuration Release
dotnet test flower.com/tests/UnitTests/FlowerFlow.Backend.Tests --configuration Release --no-restore --logger "trx;LogFileName=ff-02-independent.trx" --results-directory artifacts/ff-02
node flower.com/node_modules/firebase-tools/lib/bin/firebase.js emulators:exec --only auth,firestore --project demo-flowerflow --config flower.com/firebase.json "node flower.com/tests/IntegrationTests/test-api.cjs"
```

Contract runner cần một Firestore Emulator riêng trên `127.0.0.1:8787`, project
`demo-flowerflow-csharp` (giống job build trong `.github/workflows/aspnet.yml`):

```sh
dotnet run --project flower.com/tests/UnitTests/FlowerFlow.Contracts --configuration Release -- --firestore-emulator
```

Runner contract là chương trình console với assertion, không được tính vào số
xUnit 58. Nó còn kiểm tra alias legacy, JSON/CSP, host/origin, IP forwarding,
local SQLite/session, audit rollback, concurrent checkout, VNPay và backup.

## Giới hạn

- Local dùng Windows, Node 26.13.0 thay vì engines 24.x. CI trên baseline dùng Node 24;
  [job HTTP integration](https://github.com/Longg5048/flower.com/actions/runs/43951755187/job/133595773013)
  cũng đạt 103/103. Không che cảnh báo engine bằng sửa package yêu cầu.
- Lỗi provider, password và merchant được giả lập trong contract; không gọi đăng nhập
  Firebase production hay giao dịch VNPay thật. Sandbox/IPN thật thuộc #160.
- Auth Emulator có thể cấp ID token unsigned mới từ refresh grant cũ sau revoke;
  test ghi rõ giới hạn. Không dùng kết quả đó chứng minh refresh revocation thật.
- Một số test cố ý tạo lỗi xác thực/provider và tranh khóa transaction; log warning
  có thể xuất hiện dù assertion cuối cùng đạt. Không có test fail/skip trong kết quả trên.
- Bộ 97 test trên nhánh `codex/aspnet-sql-server` là mã khác chưa merge vào main;
  kết quả ở đây không giải quyết hay thay thế test concurrent idempotency bị lỗi của nhánh đó.
- Không kiểm thử ghi dữ liệu trên production. Emulator đã dừng sau khi chạy.

Kết luận: các nhóm API trên main đã được kiểm thử tự động theo ma trận; những giới
hạn môi trường thật được ghi riêng, không có lỗi test tồn đọng trong lượt này.

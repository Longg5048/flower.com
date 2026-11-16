# ASP.NET làm backend chính

Ứng dụng được phát triển tại worktree `aspnet-upgrade`, giữ Firebase Authentication và Firestore. Bốn project có chiều phụ thuộc: Api → Application/Infrastructure; Infrastructure → Application/Domain; Application → Domain. SQL/EF là phần khung cũ được giữ lại, không phải nguồn dữ liệu của các API đã chuyển. Không chạy migration SQL cho quá trình này.

## Phạm vi đã nối API

| API | Application | Infrastructure |
| --- | --- | --- |
| `/api/login` | LoginService, LoginProtectionService | FirebasePasswordAuth, FirestoreLoginPolicyStore, FirebaseCustomTokenIssuer |
| `/api/admin/users`, `/api/owner/users`, `/api/admin/audit` | AdminService | FirebaseAccountsAdminGateway, FirestoreAdminStore |
| `/api/products`, `/api/flower-prices` | ProductsService, FlowerPricesService | FirestoreProductReader, FirestoreFlowerPriceStore |
| `/api/product-images`, `/api/review-images` | ProductImagesService, ReviewImagesService, ImageRules | FirestoreImageStore |
| `/api/delivery-zones`, `/api/bouquet-wrapping` | SettingsService, SettingsRules | FirestoreSettingsStore |
| `/api/orders` | CheckoutService, PricingService, ReservationsService, TrackingService, ReviewsService, PaymentAdminService | FirestoreCommerceStore |
| `/api/payments`, `/api/vnpay-ipn` | PaymentsService, VnpayIpnService | VnpayGatewayAdapter, FirestoreCommerceStore |
| `/api/design-requests` | DesignRequestsService, DesignRequestValidationService | FirestoreCommerceStore |

Các alias `?route=flower-prices`, `?route=bouquet-wrapping`, `?route=audit` tiếp tục được hỗ trợ. Controller chỉ chuyển HTTP sang use case; transaction, giá tính từ server, capability, phân quyền và trạng thái thanh toán được xử lý ở Application. Domain chứa model và quy tắc tương thích dữ liệu độc lập SDK.

Frontend hiện tại vẫn dùng Firebase SDK cho các thao tác Firestore trực tiếp của nó. Việc thay các API Node không tự động thay toàn bộ những thao tác đó. Security Rules hiện hữu vẫn cần thiết. `/api/local-auth/*` thuộc máy chủ phát triển Node local, không phải API Firebase production; ASP.NET không phát hành cookie tài khoản local. Dùng tên miền staging khi thử frontend Firebase; `localhost`/`127.0.0.1` hiện được frontend nhận diện là chế độ local nếu không có runtime override.

## Chạy bản đóng gói

Từ thư mục gốc worktree:

```powershell
dotnet publish aspnet/src/FlowerFlow.Api/FlowerFlow.Api.csproj -c Release -o artifacts/main-backend
```

Có script `deploy/Publish-MainBackend.ps1` kiểm tra các tài sản cần thiết và việc không xuất bản mã nguồn server. Nếu máy chặn chạy `.ps1`, dùng lệnh `dotnet publish` ở trên; không cần đổi Execution Policy của máy.

Bản publish chứa HTML/CSS/JS/ảnh của frontend trong `wwwroot` cùng API. Nó không chứa `backend`, `lib`, API Node, rules Firestore, `.env` hoặc `node_modules` trong thư mục web. CSP và security headers lấy từ cấu hình Vercel hiện tại, áp dụng cả khi chạy Kestrel. Static asset dùng revalidation để phiên bản JS mới không bị cache immutable nhầm.

Cấp cấu hình từ secret manager theo `deploy/deployment.env.example`, sau đó chạy:

```powershell
Set-Location artifacts/main-backend
dotnet FlowerFlow.Api.dll --urls http://127.0.0.1:5087
```

Kiểm tra HTTP không ghi dữ liệu: `node aspnet/deploy/smoke-main-backend.cjs http://127.0.0.1:5087`. Trên staging dùng `node aspnet/deploy/smoke-main-backend.cjs https://your-staging-host --require-ready`; tùy chọn này yêu cầu readiness 200.

Hoặc build container từ thư mục gốc: `docker build -f aspnet/Dockerfile -t flowerflow-api .`. Container chạy dưới user không phải root, cổng 8080 và bật worker hết hạn giữ hàng. Dockerfile chưa thay cấu hình domain, TLS hay secret của nhà cung cấp hosting.

## Các cấu hình quyết định việc vận hành

- Firebase: project đúng và service account JSON phía server. Giữ nguyên chuỗi JSON đang dùng nếu cần duy trì HMAC limiter qua thời điểm chuyển đổi. ADC cũng được adapter hỗ trợ, nhưng các khóa HMAC/login hiện cần secret cấu hình tương ứng như backend Node.
- Login: public API key phải khớp frontend; server API key là khóa riêng được hạn chế ở Identity Toolkit. Hai phương thức đăng nhập bằng mật khẩu trên public API key phải bị chặn. Provider xác minh điều này trước khi đăng nhập, không cấp custom token khi lease/policy chưa cho phép.
- VNPay: `VNPAY_ENV=sandbox` cho staging, TMN/hash secret đúng môi trường, `PUBLIC_APP_URL` là origin HTTPS của frontend. Đăng ký IPN trỏ đến `/api/vnpay-ipn`; không dùng trang return của trình duyệt để xác nhận đã thu tiền.
- Proxy: đặt `Hosting__KnownProxies__0`, `__1`… bằng IP thực của proxy ngay trước ứng dụng. Chỉ khi đã cấu hình proxy tin cậy, ứng dụng mới dùng X-Forwarded-For/Proto. Chuỗi nhiều proxy cần cấu hình gateway phù hợp với ForwardLimit=1. Không bật biến môi trường tin mọi forwarded header.
- Hết hạn: `Reservations__WorkerEnabled=true` chạy dọn giữ hàng mỗi phút. Các instance có thể cùng chạy vì việc hoàn kho có transaction. Nếu host scale về 0 hoặc chỉ dùng API serverless, cần scheduler ngoài gọi `GET /api/orders?action=expire` với `Authorization: Bearer <CRON_SECRET>`. Không đặt secret trên URL. Cron Vercel cũ chưa được đổi.
- Firestore: triển khai index trong `flower.com/firestore.indexes.json` và chờ READY, gồm index `orderPaymentAudit(orderId, createdAt desc)` bổ sung. Emulator không xác nhận khả năng sẵn sàng của composite index production.

`/health` chỉ xác nhận process đang chạy. `/health/ready` trả 503 khi cấu hình/Firestore/login gate/VNPay chưa sẵn sàng; phản hồi không chứa chi tiết secret. Probe này có thể gọi Firebase và được giới hạn 12 giây. SQL `/health/database` chỉ phục vụ phần khung SQL cũ, không dùng làm probe cho backend Firestore.

Transaction adapter thử lại có giới hạn khi SDK đã trả `ABORTED`, luôn chạy lại cả phần đọc/kiểm tra/ghi. Những lỗi chưa biết commit thành công hay chưa không được thử lại tự động ở lớp này. Khi contention kéo dài vẫn có thể nhận lỗi dịch vụ; client đặt hàng phải giữ nguyên idempotency key khi thử lại.

Nhánh transaction không có ghi trả snapshot sau khi SDK rollback để giải phóng khóa đọc; không commit rỗng hoặc cập nhật giả để thoát khóa. Các nhánh thay đổi thật vẫn commit nguyên tử qua SDK.

Workflow `.github/workflows/aspnet-main-backend.yml` đã được bổ sung để chạy .NET, oracle Node 24, Chromium và Firestore Emulator với project demo, rồi publish/smoke/upload artifact. Cấu hình emulator riêng ở `deploy/firebase.test.json`; không dùng file rules test để deploy production. Workflow chưa được chạy trên GitHub trong phiên này.

## Chuyển lưu lượng và quay lại

1. Đưa artifact lên staging cùng cấu hình sandbox; giữ Node production hoạt động.
2. Đạt `/health/ready`; kiểm tra Firebase Auth thật: login, tài khoản bị khóa, admin bị thu hồi, owner; xác nhận public password endpoint không thể đi vòng qua bảo vệ.
3. Thử frontend trên domain staging: đặt hàng, giỏ có hoa tự chọn, theo dõi khách/tài khoản, đánh giá có ảnh, thiết kế/báo giá, cấu hình cửa hàng, giá hoa và quản trị người dùng.
4. Thử VNPay sandbox trọn vòng từ trình duyệt và callback nhà cung cấp thật, kể cả hủy/retry/IPN lặp/đến trễ. Bộ local chỉ kiểm chứng HMAC và xử lý IPN bằng dữ liệu mô phỏng, không thay thế bước này.
5. Chuyển toàn bộ nhóm orders/payments/IPN cùng nhau tới ASP.NET để giữ một nơi chịu trách nhiệm transaction. Cùng domain frontend/API tránh thay frontend và tránh CORS. Khi giữ frontend ở Vercel, cấu hình reverse proxy `/api/*` sang ASP.NET; phải kiểm chứng đường truyền IP tin cậy trước khi bật limiter. Chưa có rewrite production nào được đổi trong worktree.
6. Theo dõi 5xx, latency, đơn giữ kho quá hạn, paymentAttention/latePayment, audit và đăng nhập. Duy trì Node để quay về routing cũ khi cần. Dữ liệu không đổi schema nên không cần import lại.
7. Chỉ xóa API Node tương ứng sau khi staging và lưu lượng thật đã ổn định, mọi callback VNPay đang còn hiệu lực được xử lý đúng. Giữ các file JS nghiệp vụ làm oracle kiểm thử cho đến khi có kế hoạch thay thế rõ ràng.

Chưa dùng credential production, chưa cập nhật Firebase/VNPay/hosting bên ngoài và chưa chuyển traffic. Phần còn phụ thuộc môi trường là cấp secret, domain/proxy, index, staging và callback VNPay thật.

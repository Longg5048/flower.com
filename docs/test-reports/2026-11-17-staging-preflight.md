# Kiểm tra điều kiện triển khai staging và chuyển routing

Yêu cầu: cấu hình secret/domain/proxy/indexes, kiểm thử staging và VNPay sandbox, sau đó chuyển routing sang ASP.NET và giữ Node để quay lại.

## Những gì đã xác minh

- Truy cập GitHub repo `Longg5048/flower.com` được sau khi kiểm tra ngoài sandbox mạng. Có deployment từ Vercel và hai environment `Preview`, `Production`; metadata này chưa chứng minh có ASP.NET host.
- Repository variable `FLOWER_APP_URL` là `https://flower-com-six.vercel.app`. Danh sách tên repository secret chỉ có `CRON_SECRET`; không thể đọc giá trị secret từ API này, và chưa ghi/thay secret nào.
- Firebase CLI có credential store nhưng **chưa có tài khoản đăng nhập** hoặc tài khoản bổ sung. Chỉ kiểm tra boolean, không in thông tin tài khoản/token.
- Không có `.env` đã điền trong worktree; không có biến Firebase/VNPay/domain/proxy liên quan trong process hiện tại. Không tìm thấy liên kết Vercel project ở các đường dẫn `.vercel/project.json` của worktree/root frontend.
- Artifact và hướng dẫn triển khai đã có từ giai đoạn trước; chúng chưa phải bằng chứng triển khai trên hạ tầng thật. Không sử dụng kết quả emulator thay cho kết quả staging.
- Không xác minh được website công khai qua công cụ web trong lần kiểm tra này. Không suy ra website đang hoạt động hoặc đang lỗi từ kết quả đó.

## Trạng thái yêu cầu

| Công việc | Trạng thái | Thông tin/quyền còn thiếu |
| --- | --- | --- |
| Secret, domain, proxy | Chưa cấu hình trên host | Nhà cung cấp ASP.NET, quyền quản trị/deploy, origin staging và topology proxy |
| Firestore indexes | File cấu hình đã có, chưa deploy | Firebase CLI đăng nhập và xác định project đích được phép triển khai |
| Frontend/Auth/phân quyền staging | Chưa chạy trên staging thật | URL staging hoạt động và tài khoản thử customer/admin/owner |
| VNPay sandbox trọn vòng | Chưa chạy với nhà cung cấp | Merchant sandbox, TMN/hash secret trong secret manager, IPN URL được đăng ký |
| Chuyển routing và rollback | Chưa áp dụng | ASP.NET origin đã kiểm chứng, quyền deploy Vercel/domain và kết quả kiểm thử các bước trước |

Đã hỏi người dùng về hosting/origin staging, quyền truy cập hiện có, merchant VNPay sandbox và Firebase project staging. Không yêu cầu gửi secret trong chat. Việc chọn nhà cung cấp/gói trả phí, project thử nghiệm và domain chưa được tự suy đoán.

Không đổi routing, không sửa DNS, không triển khai rules/indexes và không tạo đơn/thanh toán trên dữ liệu thật trong giai đoạn kiểm tra này. Khi đủ quyền/thông tin sẽ tiếp tục theo [quy trình triển khai](../../aspnet/MAIN-BACKEND-DEPLOYMENT.md), chỉ chuyển routing sau khi các bước kiểm chứng đạt.

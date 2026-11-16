# FF-01 — Docker và Render deployment

Issue: #156. Ngày xác minh: 17/11/2026 (Asia/Bangkok).
Mã nguồn kiểm tra: `c38c2f8bd6119267e87c3f06bd65cf4d5bd7db4f` trên `main`.

## Docker

Máy local không có Docker CLI. Bằng chứng build/chạy container lấy từ
[job container](https://github.com/Longg5048/flower.com/actions/runs/43951755187/job/133596544702)
trên Ubuntu của GitHub Actions, kết luận `success`:

- Kiểm tra Docker context không chứa file env, credential, publish profile và bin canary.
- Build image bằng `flower.com/backend/Dockerfile`, tag theo SHA trên.
- Chạy container production không có credential thật, cổng 8080.
- Health, frontend, Firebase config, kiểm tra Origin và lỗi thiếu credential đạt.
- API local-auth trả 404; UID khác root, GID 1000 và đọc được secret mount thử nghiệm.

Workflow: `.github/workflows/aspnet.yml`; context build là gốc repository.
Image runtime .NET 10, `AUTH_MODE=firebase`. Không cài Docker lên máy local.

## Render đang hoạt động

Kiểm tra HTTP chỉ đọc trên `https://flower-com.onrender.com`:

| Request | Kết quả |
| --- | --- |
| GET /health | 200, JSON `status=ok`, `service=flowerflow-api` |
| GET / | 200, text/html |
| GET /api/products | 200, application/json |
| GET /api/local-auth/session | 404 |

Ba response 200 có Content-Security-Policy và HSTS
`max-age=31536000; includeSubDomains`.

Đã chạy script tái kiểm chứng `node flower.com/scripts/verify-render.mjs` thành
công; script còn xác nhận JSON catalog có mảng `products`. Thời điểm kiểm tra
script: 03:52 ngày 17/11/2026 (Asia/Bangkok).

`render.yaml` trỏ branch main, Dockerfile trên, region Singapore, healthCheckPath
`/health`, auto deploy sau checksPass. Không thay đổi dashboard, secret, DNS hoặc
khởi động một deployment mới trong lần xác minh này.

## Giới hạn và vận hành

- `/health` là liveness; không chứng minh mọi cấu hình login/VNPay đã sẵn sàng.
- Endpoint công khai chưa trả build SHA. Không khẳng định Render đang chạy đúng
  SHA `c38c2f8`; muốn đối chiếu chính xác cần xem commit ở Render Dashboard → Deploys.
- Metadata deployment GitHub ghi Vercel; không dùng metadata đó làm bằng chứng Render.
- Không thực hiện giao dịch, ghi dữ liệu hay kiểm thử tải production.
- Khi cần rollback, chọn deployment đã kiểm chứng ở Render Dashboard và chạy lại
  smoke test; không restore database hoặc phát lại callback thanh toán.

Kết quả: build/chạy Docker trên CI và hoạt động HTTP của Render đã được xác minh.
Kiểm thử đầy đủ API là công việc riêng của #157; VNPay thực tế thuộc #160.

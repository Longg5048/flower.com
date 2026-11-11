# Báo cáo kiểm thử — 2026-11-11 — khôi phục PIN bằng mật khẩu

## Thông tin đợt chạy

- **Ngày:** 2026-11-11, Asia/Bangkok.
- **Nhánh/commit nền:** `fix/hide-signup-password-policy`, `3ced6e5`.
- **Môi trường:** Windows, PowerShell, Node.js v24.22.1, Playwright và Google Chrome đã cài.
- **Thay đổi:** bỏ câu hỏi bảo mật khỏi đăng ký, thiết lập PIN và khôi phục PIN local; xác nhận bằng mật khẩu hiện tại khi quên PIN. Firebase Authentication giữ nguyên.

## Lệnh và kết quả

Các lệnh npm chạy từ `flower.com`; trình duyệt dùng `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` trỏ tới Google Chrome đã cài.

| Lệnh / đợt chạy | Đạt | Trượt | Bỏ qua | Ghi chú |
| --- | ---: | ---: | ---: | --- |
| `node --test tests/pin-password-recovery.test.js` từ gốc repo, trước khi sửa backend | 0 | 3 | 0 | Red: backend cũ còn yêu cầu câu hỏi/đáp án |
| Các unit test local-accounts, local-auth-http, session-security, security-remediation và PIN mới, trong quá trình sửa | 35 | 1 | 0 | Fixture phiên còn gửi mật khẩu cũ sau khi đổi mật khẩu; đã sửa để kiểm tra mật khẩu cũ thất bại, mật khẩu hiện tại thành công |
| `npm.cmd test`, lượt cuối | 215 | 0 | 0 | Bao gồm 4 ca mới về bỏ câu hỏi, mật khẩu chính xác, giới hạn thử sai và tài khoản cũ |
| `npm.cmd run test:e2e:local`, lượt đầu | 23 | 1 | 0 | Một ca desktop quay lại form bị thanh liên hệ che nút sau khi input mất focus |
| Chạy lại 6 ca khôi phục PIN liên quan sau khi sửa CSS, lệnh bên dưới | 6 | 0 | 0 | Desktop/mobile: quên PIN, sai PIN 5 lần, sai PIN cũ trên trang tài khoản |
| `npm.cmd run test:auth-visibility` | 28 | 0 | 0 | Hồi quy nút mật khẩu, vùng chọn, focus, bộ đếm và chuyển form/tab |
| `node --check` các file JavaScript thay đổi; `git diff --check` | — | 0 | 0 | Đạt |

Lệnh chạy lại các ca PIN sau khi sửa:

```powershell
npm.cmd run test:e2e:local -- --grep 'forgotten PIN|fifth wrong PIN|fifth wrong old PIN'
```

Không chạy lại toàn bộ 24 ca local sau sửa CSS: 6 ca liên quan đã chạy lại và đạt; 18 ca còn lại đạt trong lượt đầu. Không chạy kiểm thử Firebase Emulator hoặc Firebase production trong đợt này.

## Đã xác minh

- Đăng ký và thiết lập PIN không cần câu hỏi/đáp án; PIN vẫn là 4–6 chữ số ASCII, có xác nhận và được băm với salt.
- Quên PIN cần đúng thông tin tài khoản và mật khẩu hiện tại. Mật khẩu phân biệt hoa/thường; thiếu mật khẩu, sai mật khẩu hoặc chỉ có đáp án cũ đều không đủ để đặt lại PIN.
- Đặt lại PIN không đổi mật khẩu hoặc xóa khóa đăng nhập. PIN mới phải khác PIN cũ; PIN cũ không còn dùng được sau thay đổi.
- Năm lần xác nhận sai bị khóa 15 phút. Bộ đếm PIN cũ và thời hạn khóa khôi phục trên dữ liệu cũ được giữ.
- Đặt lại PIN thành công thu hồi các phiên cũ. Kiểm thử HTTP kiểm tra cả cookie của tài khoản khác đang được xóa khi thực hiện khôi phục.
- Tài khoản cũ có dữ liệu câu hỏi vẫn dùng được PIN và mật khẩu đã có. Câu hỏi không còn được dùng làm bằng chứng; endpoint lấy câu hỏi trả 404.
- Giao diện không còn trường đặt/trả lời câu hỏi. Sau khi quay lại và mở lại form khôi phục, mật khẩu xác nhận được xóa.
- Mật khẩu mới vẫn theo chính sách hiện hành; luồng đăng nhập/đăng ký Firebase không đổi.

## Lỗi đã xử lý và phạm vi dữ liệu

- Thanh liên hệ có thể xuất hiện khi focus chuyển từ input sang nút ngoài form và chặn nút quay lại. CSS khôi phục nay giữ thanh này ẩn khi focus còn trong vùng khôi phục; ca desktop đã đạt khi chạy lại.
- Kiểm thử dùng tài khoản và phiên trong SQLite cô lập/in-memory, cùng file SQLite kiểm thử trong `.tools/local-account-tests/` bị Git bỏ qua. Không mở hoặc sửa cơ sở dữ liệu tài khoản đang sử dụng.
- Kiểm thử local chặn tải SDK Firebase. Các thông báo thiếu cấu hình Firebase Admin khi trang catalog tải dữ liệu là do môi trường kiểm thử không có credentials; không tạo tài khoản hoặc ghi Firebase production.
- Bộ `test:auth-visibility` dùng mock Firebase và chặn API/mạng bên ngoài như cấu hình hiện có.
- Không thay đổi thông tin xác thực, `.env` hoặc dữ liệu người dùng trong commit. Cần khởi động lại máy chủ dev đang chạy để backend Node nhận mã mới.

# Tài khoản tự tạo cho bản chạy thử

Trong `flower.com`, chạy `npm run dev` rồi mở `http://127.0.0.1:3000/login.html`.
Mặc định `AUTH_MODE=local`, không cần Firebase, service account hoặc email thật.
Tên đăng nhập cần có `@`, ví dụ `ban@demo`. Ứng dụng không gửi thư hoặc xác minh hộp thư.
Mật khẩu mới cần 8–4096 ký tự, có chữ thường (a–z) và số (0–9); đăng ký phải có họ tên và số điện thoại. Chính sách mới không chặn đăng nhập bằng mật khẩu cũ.

Tài khoản local không yêu cầu PIN hoặc câu hỏi bảo mật. Chức năng tự khôi phục/đặt lại mật khẩu
không khả dụng cho tài khoản local vì tên đăng nhập không phải email đã xác minh. Nếu quên mật khẩu,
hãy tạo một tài khoản local mới. Trang quên mật khẩu vẫn gửi liên kết đặt lại cho tài khoản Firebase.

## Giới hạn đăng nhập

- Sai lần 1–4: báo sai thông tin.
- Sai lần 5: khóa 15 phút.
- Hết thời gian khóa, lần sai tiếp theo tăng thời gian khóa lần lượt lên 30, 60, 120 và 240 phút.
- Sau đó, mỗi lần đăng nhập sai tiếp tục khóa tối đa 240 phút; không có trạng thái khóa vĩnh viễn.
- Đăng nhập đúng khi hết hạn khóa sẽ xóa lịch sử thất bại.
- Trong thời gian khóa không kiểm tra mật khẩu, kể cả mật khẩu đúng; yêu cầu thêm không kéo dài hạn khóa.

Bộ đếm theo tài khoản được lưu ở máy chủ. Đổi IP, tab, xóa local storage hoặc khởi động lại máy chủ
không tạo thêm lượt thử. SQLite transaction giữ việc kiểm tra/cập nhật nguyên tử. Tài khoản cũ từng
bị yêu cầu đặt lại mật khẩu sẽ được chuyển sang một thời gian khóa tối đa 240 phút khi nâng cấp.

## Lưu trữ và phạm vi

- Dữ liệu ở `.tools/local-auth/accounts.sqlite`, bị Git bỏ qua và không được phục vụ qua HTTP.
- Mật khẩu được băm scrypt với salt riêng; mã phiên chỉ lưu SHA-256.
- Cookie phiên `HttpOnly`, `SameSite=Strict`, sống 12 giờ. Local storage chỉ chứa bản sao hồ sơ,
  không phải bằng chứng xác thực hoặc nguồn cấp quyền. Tài khoản local chỉ có quyền khách hàng.
- Khi máy chủ mở cơ sở dữ liệu cũ, PIN, mã khôi phục và câu hỏi/đáp án cũ được xóa; mật khẩu,
  hồ sơ, phiên và dữ liệu khóa đăng nhập còn lại được giữ.
- Các trang tài khoản local hoạt động mà không tải SDK Firebase cho xác thực.
- Đây là máy chủ Node trên loopback cho bản thử. Catalog, đơn hàng và quản trị vẫn theo kiến trúc
  riêng của dự án. Tài khoản Firebase thuộc kho riêng; bản Vercel hiện có vẫn dùng chế độ Firebase.

Không xóa file SQLite nếu muốn giữ tài khoản và trạng thái khóa. Đổi giữa `localhost` và `127.0.0.1`
có thể cần đăng nhập lại do cookie gắn với hostname. Live Server chỉ phục vụ HTML, không chạy API này.

## Kiểm thử

`npm test` kiểm tra đăng ký không cần thông tin khôi phục, khóa đăng nhập có thời hạn, xóa dữ liệu
khôi phục cũ, lưu tài khoản sau khởi động, phiên và API HTTP. `npm run test:e2e:local` kiểm tra
đăng ký/đăng nhập/đăng xuất, hồ sơ và thông báo khôi phục không khả dụng cho tài khoản local,
không cần Firebase Emulator hoặc Java.

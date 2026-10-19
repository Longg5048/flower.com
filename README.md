<div align="center">

<img src="flower.com/images/flowerflow-logo.png" alt="FlowerFlow" width="120">

# FlowerFlow

**Website cửa hàng hoa tươi trực tuyến: danh mục sản phẩm, giỏ hàng, thanh toán QR và trang quản trị.**

[Xem bản demo](https://flower-com-six.vercel.app) · [Kiến trúc kỹ thuật](flower.com/README.md) · [Cấu hình quản trị](ADMIN-SETUP.md) · [Ghi chú phát hành](RELEASE_NOTES_v0.1.0.md)

![HTML5](https://img.shields.io/badge/HTML5-E34F26?logo=html5&logoColor=white)
![CSS3](https://img.shields.io/badge/CSS3-1572B6?logo=css3&logoColor=white)
![JavaScript](https://img.shields.io/badge/JavaScript-F7DF1E?logo=javascript&logoColor=black)
![Firebase](https://img.shields.io/badge/Firebase-FFCA28?logo=firebase&logoColor=black)
![Vercel](https://img.shields.io/badge/Vercel-000000?logo=vercel&logoColor=white)
![Node.js](https://img.shields.io/badge/Node.js-24.x-339933?logo=nodedotjs&logoColor=white)

</div>

---

## Mục lục

- [Giới thiệu](#giới-thiệu)
- [Tính năng](#tính-năng)
- [Công nghệ](#công-nghệ)
- [Cấu trúc thư mục](#cấu-trúc-thư-mục)
- [Bắt đầu nhanh](#bắt-đầu-nhanh)
- [Cấu hình](#cấu-hình)
- [Triển khai](#triển-khai)
- [Kiểm thử](#kiểm-thử)
- [Phân quyền](#phân-quyền)
- [Hạn chế đã biết](#hạn-chế-đã-biết)
- [Lộ trình](#lộ-trình)
- [Đóng góp](#đóng-góp)

## Giới thiệu

FlowerFlow là website bán hoa viết bằng HTML, CSS và JavaScript thuần, không dùng framework và không cần bước build. Phía khách hàng gồm các trang cửa hàng tĩnh; phần dữ liệu và xác thực dùng Firebase (Authentication, Firestore); các API quản trị cần quyền cao chạy dưới dạng Vercel Functions với Firebase Admin SDK.

## Tính năng

### Khách hàng

| Tính năng | Mô tả |
| --- | --- |
| Danh mục sản phẩm | Duyệt, tìm kiếm, lọc theo danh mục, sắp xếp; hỗ trợ sản phẩm giao trong ngày và đang giảm giá |
| Chi tiết sản phẩm | Thư viện ảnh, giá VND, tồn kho, chọn số lượng, gợi ý sản phẩm liên quan |
| Đánh giá sản phẩm | Khách đã đăng nhập gửi đánh giá 1–5 sao; đánh giá hiển thị công khai trên trang sản phẩm |
| Giỏ hàng và yêu thích | Đồng bộ giữa các trang, gộp sản phẩm trùng, giới hạn theo tồn kho |
| Ưu đãi | Chọn chương trình khuyến mãi áp dụng cho các sản phẩm phù hợp khi thanh toán |
| Thanh toán | Thu thập thông tin giao hàng, tạo mã QR chuyển khoản Vietcombank (VietQR) |
| Theo dõi đơn | Tra cứu bằng mã đơn và số điện thoại; tự hiển thị đơn của tài khoản đang đăng nhập |
| Tài khoản | Đăng ký, đăng nhập bằng email/mật khẩu hoặc Google, quên mật khẩu, trang hồ sơ |
| Liên hệ | Thanh liên hệ nhanh qua Facebook và Zalo |

### Quản trị

| Tính năng | Mô tả |
| --- | --- |
| Sản phẩm | Thêm, sửa, xóa, cập nhật tồn kho; đồng bộ lên Firestore theo thời gian thực |
| Danh mục và ưu đãi | Quản lý danh mục hoa và chương trình khuyến mãi |
| Đơn hàng | Cập nhật trạng thái: Chờ xác nhận, Đang giao, Đã giao, Đã hủy |
| Doanh thu | Báo cáo doanh thu theo hôm nay, theo tháng, toàn thời gian hoặc khoảng ngày tùy chọn |
| Đánh giá và tin nhắn | Xem và quản lý phản hồi của khách |
| Người dùng | Danh sách tài khoản Firebase và nhà cung cấp đăng nhập (qua API bảo vệ) |
| Owner | Cấp hoặc gỡ quyền admin cho từng tài khoản tại `/owner.html` |

## Công nghệ

| Lớp | Công nghệ |
| --- | --- |
| Giao diện | HTML5, CSS3, JavaScript (ES modules), không framework |
| Xác thực | Firebase Authentication (Email/Password, Google) |
| Dữ liệu | Cloud Firestore (sản phẩm, đánh giá), có cache `localStorage` |
| API quản trị | Vercel Functions và `firebase-admin` |
| Thanh toán | VietQR (mã QR chuyển khoản ngân hàng) |
| Kiểm thử | `node:assert` có sẵn của Node.js, không cần thư viện ngoài |
| Hosting | Vercel |

## Cấu trúc thư mục

```text
.
├── flower.com/                 # Gốc dự án Vercel (Root Directory)
│   ├── api/
│   │   ├── admin/users.js      # API liệt kê người dùng (yêu cầu quyền admin)
│   │   └── owner/users.js      # API cấp/gỡ quyền admin (yêu cầu quyền owner)
│   ├── lib/firebase-admin.js   # Khởi tạo Firebase Admin, xác minh ID token
│   ├── scripts/                # Script gán quyền owner/admin lần đầu
│   ├── images/                 # Ảnh sản phẩm và giao diện
│   ├── flower.json             # Catalog sản phẩm mặc định
│   ├── firebase-config.js      # Cấu hình Firebase Web App (công khai)
│   ├── firestore.rules         # Quy tắc bảo mật Firestore
│   ├── firebase.json           # Cấu hình Firebase CLI
│   ├── product-store.js        # Đồng bộ catalog giữa Firestore và localStorage
│   ├── shopping-store.js       # Logic giỏ hàng và danh sách yêu thích
│   ├── review-store.js         # Đọc/ghi đánh giá sản phẩm trên Firestore
│   ├── store-header.js, store-footer.js   # Header/footer dùng chung
│   ├── *.html, *.css, *.js     # Các trang: chủ, sản phẩm, giỏ hàng, thanh toán, quản trị...
│   └── package.json
├── tests/                      # Bộ kiểm thử đơn vị
├── ADMIN-SETUP.md              # Hướng dẫn cấu hình quản trị trên Vercel
└── RELEASE_NOTES_v0.1.0.md
```

Mô tả chi tiết kiến trúc và luồng dữ liệu nằm trong [`flower.com/README.md`](flower.com/README.md).

## Bắt đầu nhanh

### Yêu cầu

- [Node.js](https://nodejs.org/) 24.x (để chạy kiểm thử và script quản trị)
- Python 3 hoặc bất kỳ máy chủ HTTP tĩnh nào
- (Tùy chọn) [Vercel CLI](https://vercel.com/docs/cli) để chạy các API quản trị ở máy cục bộ

### Chạy giao diện cửa hàng

```sh
git clone https://github.com/longg5048/flower.com.git
cd flower.com/flower.com
python -m http.server 8000
```

Mở <http://localhost:8000> trong trình duyệt.

> [!NOTE]
> Không mở trực tiếp file HTML bằng `file://`: website tải `flower.json` bằng `fetch`, nên bắt buộc phải chạy qua HTTP.

### Chạy kèm API quản trị

Máy chủ tĩnh không chạy được `/api/*`. Để thử trang quản trị đầy đủ:

```sh
cd flower.com
npm install
vercel link
vercel dev
```

Cần cấu hình biến môi trường như mô tả ở mục [Cấu hình](#cấu-hình).

## Cấu hình

### Firebase Web App

1. Tạo project trong [Firebase Console](https://console.firebase.google.com/) và đăng ký một Web App.
2. Vào **Authentication → Sign-in method**, bật **Email/Password** và **Google**.
3. Vào **Authentication → Settings → Authorized domains**, thêm domain triển khai và `localhost`.
4. Dán cấu hình Web App vào [`flower.com/firebase-config.js`](flower.com/firebase-config.js).

> [!IMPORTANT]
> Cấu hình Firebase Web App là thông tin công khai, không phải bí mật. Dữ liệu được bảo vệ bằng **Firestore Security Rules**, Authorized domains và custom claims.

### Firestore Security Rules

Quy tắc bảo mật được quản lý trong [`flower.com/firestore.rules`](flower.com/firestore.rules):

| Collection | Đọc | Ghi |
| --- | --- | --- |
| `products` | Công khai | Chỉ admin |
| `reviews` | Công khai | Tạo: người dùng đã đăng nhập (kiểm tra schema, 1–5 sao, tối đa 1000 ký tự); sửa/xóa: chỉ admin |
| `metadata` | Chỉ admin | Chỉ admin |
| Còn lại | Từ chối | Từ chối |

Triển khai quy tắc bằng [Firebase CLI](https://firebase.google.com/docs/cli):

```sh
cd flower.com
firebase deploy --only firestore:rules
```

### Biến môi trường (Vercel)

| Biến | Bắt buộc | Mô tả |
| --- | :---: | --- |
| `FIREBASE_SERVICE_ACCOUNT_JSON` | ✅ | Toàn bộ nội dung file JSON của service account có vai trò *Firebase Authentication Admin* |
| `FIREBASE_PROJECT_ID` | ➖ | Project ID của Firebase; mặc định lấy từ service account |

> [!CAUTION]
> Không bao giờ commit file service account vào repository. `.gitignore` đã chặn các mẫu tên file key phổ biến; chỉ lưu key trong biến môi trường của Vercel.

## Triển khai

1. Import repository vào [Vercel](https://vercel.com/new).
2. Đặt **Root Directory** là `flower.com`.
3. Thêm các biến môi trường ở trên cho môi trường **Production** (và **Preview** nếu cần).
4. Deploy, sau đó gán quyền Owner đầu tiên bằng script `npm run set-owner`.
5. Triển khai Firestore Security Rules bằng `firebase deploy --only firestore:rules`.

Hướng dẫn từng bước, bao gồm cách gán quyền Owner bằng Application Default Credentials mà không cần tải key, có trong [`ADMIN-SETUP.md`](ADMIN-SETUP.md).

## Kiểm thử

```sh
cd flower.com
npm test
```

Bộ kiểm thử dùng `node:assert` có sẵn, không cần cài thư viện:

| File | Phạm vi |
| --- | --- |
| `product-page.test.js` | Hiển thị và tương tác trang sản phẩm |
| `product-related.test.js` | Gợi ý sản phẩm liên quan, loại bỏ trùng lặp |
| `product-categories.test.js` | Phân loại sản phẩm theo danh mục |
| `product-store-sync.test.js` | Gộp catalog từ Firestore với dữ liệu mặc định |
| `shopping-store.test.js` | Giỏ hàng, yêu thích, giới hạn tồn kho |
| `store-footer.test.js` | Footer dùng chung |

## Phân quyền

| Vai trò | Custom claims | Quyền hạn |
| --- | --- | --- |
| Khách hàng | (không có) | Mua hàng, theo dõi đơn, quản lý hồ sơ |
| Admin | `admin: true` | Truy cập trang quản trị và API `/api/admin/users` |
| Owner | `role: "owner"`, `admin: true` | Mọi quyền của Admin, cấp/gỡ quyền admin qua `/owner.html` |

Mỗi lần đổi vai trò, refresh token của người dùng bị thu hồi để quyền mới có hiệu lực ngay. Owner chỉ được tạo bằng script tin cậy chạy ở máy cục bộ, không tạo được qua giao diện web.

## Hạn chế đã biết

Dự án đang ở giai đoạn **v0.1.0 (chưa phát hành)**. Trước khi vận hành thật, cần lưu ý:

- **Đơn hàng, tin nhắn, danh mục và ưu đãi đang lưu trong `localStorage`** của trình duyệt, nên không đồng bộ giữa các thiết bị. Báo cáo doanh thu chỉ phản ánh dữ liệu của trình duyệt đang dùng.
- **Thanh toán QR chưa có bước đối soát tự động.** Admin cần tự kiểm tra giao dịch ngân hàng trước khi xác nhận đơn.
- **Chế độ tài khoản demo** (dùng khi Firebase chưa bật Email/Password) lưu mật khẩu không mã hóa trong `localStorage`. Không dùng mật khẩu thật ở chế độ này.
- Giỏ hàng và danh sách yêu thích chỉ lưu cục bộ theo trình duyệt.

## Lộ trình

- [x] Lưu đánh giá sản phẩm trên Firestore
- [x] Quản lý Firestore Security Rules trong repository
- [ ] Chuyển đơn hàng, tin nhắn và ưu đãi lên Firestore
- [ ] Đồng bộ trang quản lý đánh giá của admin với Firestore
- [ ] Tích hợp xác nhận thanh toán tự động (webhook ngân hàng hoặc cổng thanh toán)
- [ ] Thiết lập CI chạy `npm test` trên GitHub Actions
- [ ] Tách `admin.js` thành các module theo chức năng

## Đóng góp

1. Tạo nhánh mới từ `main`: `git checkout -b feature/ten-tinh-nang`.
2. Commit với thông điệp rõ ràng, mô tả thay đổi.
3. Chạy `npm test` trong `flower.com` và đảm bảo tất cả đều đạt.
4. Mở Pull Request mô tả thay đổi và cách kiểm tra.

---

<div align="center">
Ứng dụng Django quản lý kho trước đây được lưu tại nhánh <code>django-backend</code>.
</div>

# Hướng dẫn kiểm thử

## Yêu cầu

- Node.js 24.x (kiểm tra bằng `node --version`).
- Không cần cài thư viện riêng cho bộ test; các test dùng `node:assert` và Node.js Test Runner.

## Chạy kiểm thử

Từ thư mục gốc của repository, chạy toàn bộ test:

```sh
cd flower.com
npm test
```

Để chạy một file test, dùng Node.js Test Runner từ thư mục `flower.com`:

```sh
node --test ../tests/product-page.test.js
```

## Các phạm vi đang được kiểm thử

| File | Phạm vi |
| --- | --- |
| `product-page.test.js` | Hiển thị và tương tác trang sản phẩm |
| `product-related.test.js` | Gợi ý sản phẩm liên quan, loại bỏ trùng lặp |
| `product-categories.test.js` | Phân loại sản phẩm theo danh mục |
| `product-store-sync.test.js` | Gộp catalog từ Firestore với dữ liệu mặc định |
| `shopping-store.test.js` | Giỏ hàng, yêu thích, giới hạn tồn kho |
| `store-footer.test.js` | Footer dùng chung |

## Báo cáo kết quả

Sau mỗi đợt chạy test, sao chép `docs/test-reports/TEMPLATE.md` thành `docs/test-reports/YYYY-MM-DD.md` (nếu cùng ngày đã có báo cáo, thêm hậu tố như `-ci` hoặc `-2`). Ghi lại commit/nhánh nếu biết, môi trường, từng lệnh đã chạy, kết quả đạt/trượt, thông báo lỗi và các bước tái hiện tối thiểu. Nếu không có test nào trượt, ghi rõ `Không có` ở mục lỗi và tái hiện. Không đưa token, mật khẩu, thông tin khách hàng hoặc bí mật môi trường vào báo cáo.

Khi một test thất bại, giữ nguyên đầu ra lỗi liên quan, xác định test/file bị ảnh hưởng và mô tả các bước cần thiết để chạy lại. Không đánh dấu đợt kiểm thử là đạt nếu có test bị bỏ qua hoặc không chạy; hãy nêu rõ trạng thái thực tế.

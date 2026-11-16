# Baseline backend trước tái cấu trúc

Ngày: 16/11/2026. Báo cáo đi kèm [đánh giá backend](../../aspnet/BACKEND-ASSESSMENT.md).

## Phạm vi và môi trường

- Nguồn chính: `aspnet-upgrade`; tham khảo: `backend-layered`. Không thay runtime, database, route hay frontend.
- Node hiện có: v26.13.0, npm 11.20.0; package yêu cầu Node 24.x. Kết quả trên Node 26 là baseline môi trường này, cần chạy lại Node 24 trong CI trước chuyển lưu lượng.
- Hai worktree đều có thay đổi chưa commit trước khi đánh giá. Các thay đổi đó được giữ nguyên.

## C#

| Lệnh / thư mục | Kết quả |
| --- | --- |
| `dotnet build aspnet/FlowerFlow.sln --no-restore --nologo` / aspnet-upgrade | PASS; 0 warning, 0 error |
| `dotnet build flower.com/FlowerFlow.sln --no-restore --nologo` / backend-layered | PASS; 0 warning, 0 error |
| `dotnet test flower.com/tests/UnitTests/FlowerFlow.Backend.Tests/FlowerFlow.Backend.Tests.csproj --no-restore --nologo --verbosity quiet` / backend-layered | FAIL; 57 passed, 1 failed, 0 skipped, tổng 58 |

Test lỗi: `ArchitectureTests.Independent_backend_does_not_reference_legacy_infrastructure_or_database_providers`.

Chạy lại riêng bài lỗi với `--no-build --no-restore --filter FullyQualifiedName~Independent_backend_does_not_reference_legacy_infrastructure_or_database_providers --logger 'console;verbosity=normal'` xác nhận:

```text
FlowerFlow.Application references Google.Cloud.Firestore
ArchitectureTests.cs:20
```

Không tắt hay sửa test để làm xanh baseline. Lỗi này là bằng chứng ranh giới Application/adapter chưa đạt mục tiêu. 57 test còn lại thuộc project tham chiếu host `FlowerFlow.Backend` dùng in-memory; không đại diện toàn bộ host Firebase `FlowerFlow.Api`.

Nguồn chính chưa có project C# test. Contract runner `FlowerFlow.Contracts` trong bản tham khảo đã build nhưng chưa chạy trong đợt này; không tính build runner là pass contract.

## JavaScript

Chọn 13 file test nghiệp vụ: firebase-admin, api-handlers, api-orders, order-validation, order-pricing, order-inventory, vnpay, password-auth, login-protection, design-requests-api, delivery-zones-api, flower-prices, review-photos.

Lần đầu chạy `node --test --test-reporter=dot tests/<file>.test.js ...` tại aspnet-upgrade thất bại vì thiếu `firebase-admin/firestore`. Đây là lỗi môi trường dependency, không kết luận nghiệp vụ hỏng.

Khôi phục dependency bằng `npm.cmd ci --ignore-scripts --no-audit --no-fund` trong flower.com. Lệnh `npm` bị PowerShell ExecutionPolicy chặn wrapper npm.ps1, nên dùng npm.cmd. Lần trong sandbox bị EACCES khi fetch npm registry; đã yêu cầu chạy lại có quyền truy cập mạng. Không đổi package.json/package-lock, không bật lifecycle scripts.

Lần cài lại thành công: 687 packages. Chạy lại đúng 13 file trên với `--test-reporter=tap`: **91 passed, 0 failed, 0 skipped**, tổng 91, khoảng 3,2 giây. Log đầy đủ: [backend-baseline-node.tap](../../aspnet/backend-baseline-node.tap).

```powershell
node --test --test-reporter=tap tests/firebase-admin.test.js tests/api-handlers.test.js tests/api-orders.test.js tests/order-validation.test.js tests/order-pricing.test.js tests/order-inventory.test.js tests/vnpay.test.js tests/password-auth.test.js tests/login-protection.test.js tests/design-requests-api.test.js tests/delivery-zones-api.test.js tests/flower-prices.test.js tests/review-photos.test.js
```

Đây là subset phục vụ baseline backend, không phải toàn bộ test của dự án và không phải phép so sánh C#/JS trên cùng endpoint.

## Chưa kiểm chứng

Chưa chạy SQL Server, Auth/Firestore Emulator, Firebase thật, VNPay sandbox hoặc E2E. Chưa xác nhận cạnh tranh kho, transaction retry, role claims thật và callbacks thực tế. Chưa có API Node nào đủ điều kiện loại bỏ chỉ dựa vào kết quả trên.

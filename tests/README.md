# Kiểm thử FlowerFlow

| Loại | Ở đâu | Chạy |
|---|---|---|
| Unit test | `tests/*.test.js` | `cd flower.com && npm test` |
| Unit test + coverage | như trên | `cd flower.com && npm run test:coverage` |
| Feature test (đặt hàng, đăng nhập, quản lý) | `tests/integration/feature-*.test.js` | `cd tests && npm ci && npm run integration` |
| System test (frontend SDK – API – Firestore) | `tests/integration/system.test.js` | như trên |
| Security test (Firestore rules, quyền API) | `tests/integration/security-*.test.js` | như trên |
| Smoke test sau khi deploy | `flower.com/scripts/smoke-test.js` | `cd flower.com && SMOKE_URL=https://... npm run smoke` |

Test tích hợp chạy API thật (`flower.com/api/*.js`) và `firestore.rules` thật trên **Firebase Emulator**
với project `demo-flowerflow`, nên không đụng tới dữ liệu thật. Cần Java 11+ (Firebase Emulator dùng Java).

Mỗi tính năng mới viết test trước (xem `CONTRIBUTING.md`).

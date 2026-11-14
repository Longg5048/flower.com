# ASP.NET Core backend migration

## Scope and rollout rules

The production backend is the Node.js code in `flower.com/backend/`, exposed by
the Vercel Functions in `flower.com/api/`. This migration adds ASP.NET Core
alongside it. The JavaScript backend, frontend, Firestore data and current
production routing stay unchanged until API parity tests pass in staging.

The selected deployment target is a Vercel Container. The container must serve
the existing static frontend and the API on the same origin. Do not switch
production traffic or remove the Vercel Functions before the rollback path has
been tested.

## API and implementation mapping

All rows below are production APIs. Target routes retain the existing URL,
HTTP method, query parameters, request fields, response JSON and status codes.
No API has been ported yet.

| Existing URL | Node.js implementation | Required C# implementation | Status |
| --- | --- | --- | --- |
| `/api/orders` | `api/orders.js`; `backend/orders/controller.js`, `checkout.js`, `pricing.js`, `validation.js`, `reservations.js`, `tracking.js`, `payment-admin.js`, `reviews.js` | Orders endpoints and services; keep quote, create, lookup, account orders, expiry, payment reconciliation and review actions | Not started |
| `/api/payments` | `api/payments.js`; `backend/payments/payments.js` | VNPay link creation, retry and status lookup | Not started |
| `/api/vnpay-ipn` | `api/vnpay-ipn.js`; `backend/payments/vnpay-ipn.js`, `vnpay.js` | Signed VNPay callback with idempotent order/payment updates | Not started |
| `/api/products` | `api/products.js`; `backend/catalog/products.js` | Public catalog and authorized management view | Not started |
| `/api/flower-prices` | Rewrite to `backend/catalog/flower-prices.js` | Owner-only flower price updates and history | Not started |
| `/api/product-images` | `backend/catalog/product-images.js` | Authenticated image upload/read with existing validation | Not started |
| `/api/review-images` | `backend/catalog/review-images.js` | Authorized review image reads | Not started |
| `/api/delivery-zones` | `backend/shop-settings/delivery-zones.js` | Public zone list and authorized zone management | Not started |
| `/api/bouquet-wrapping` | Rewrite to `backend/shop-settings/bouquet-wrapping.js` | Public wrapping tiers and authorized management | Not started |
| `/api/design-requests` | `backend/design-requests/controller.js`, `validation.js` | Create, capability-token lookup/response and authorized quote updates | Not started |
| `/api/login` | `backend/accounts/login.js`, `login-protection.js`, `password-auth.js` | Firebase password login and existing lockout behavior | Not started |
| `/api/admin/users` | `backend/admin/users.js` | Admin user listing | Not started |
| `/api/admin/audit` | Rewrite to `backend/admin/audit.js` | Admin audit listing | Not started |
| `/api/owner/users` | `backend/admin/owner-users.js`, `roles.js` | Owner-only role grant/removal and Firebase custom-claim synchronization | Not started |

`flower.com/lib/` and `/api/local-auth/*` support the local Node development
server, SQLite accounts and local PIN/recovery flows; they are not Vercel
production functions. Keep that development path available until the C# local
workflow is explicitly covered and tested.

## Data and behavior that must remain unchanged

- Firebase Authentication remains the identity provider. Verify Firebase ID
  tokens with revocation checking. Admin access requires both the existing
  custom claim and `adminRoles/{uid}`; owner operations also require the owner
  role in both places.
- Keep the existing Firestore collections and documents, including `orders`,
  `products`, `metadata`, `orderRequests`, `orderCreationLimits`,
  `orderLookupLimits`, `paymentTransactions`, `reviews`, `reviewImages`,
  `productImages`, `designRequests`, `adminRoles`, `adminAudit`,
  `orderPaymentAudit`, `priceHistory` and `loginProtection`.
- Order prices are calculated on the server in whole VND. Keep promotion date
  checks, delivery-zone fees, custom bouquet wrapping, stock checks and the
  quote-vs-create total comparison.
- Create orders with Firestore transactions, stock reservations, idempotency
  and existing rate limits. Preserve QR/COD reservation durations (30 minutes)
  and VNPay reservation duration (15 minutes).
- Preserve VNPay HMAC-SHA512 verification, retry rules, IPN idempotency and
  late-payment handling.
- Preserve the shared error JSON shape `{ "code": "...", "error": "..." }`
  and endpoint-specific success response shapes.
- Keep `/api/orders?action=expire` available for the existing scheduled job;
  its current schedule is 19:07 UTC daily.

## Verification gates

1. Port a module without changing its Node implementation or frontend caller.
2. Add focused C# tests for the port and compare requests/responses and
   Firestore effects with the JavaScript behavior.
3. Run Firestore Emulator integration tests for transactions, stock contention,
   authentication/authorization and payment state transitions.
4. Deploy the container to staging, run the existing frontend against it, and
   verify rollback to the current Node deployment.
5. Only after these gates pass, change production routing.

The existing JavaScript unit and emulator suites are the behavioral reference.
They were not runnable in the current environment because `npm` is unavailable.

## Current implementation status

- Added the .NET 10 API host, Firebase Admin/Firestore package references,
  health endpoint, and static-file publishing for the existing frontend.
- Ported the initial pure order-pricing slice: legacy VND conversion,
  promotion validation and cart totals. It is not yet connected to Firestore or
  exposed as `/api/orders`.
- ASP.NET tests currently cover those pricing rules. Authentication, Firestore
  transactions, payment handling and all production API routes remain on the
  Node.js backend and are not yet migrated.

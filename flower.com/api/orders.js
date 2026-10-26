const { createHmac } = require("node:crypto");
const { FieldValue, Timestamp } = require("firebase-admin/firestore");
const { getAdminAuth, getAdminFirestore, sendError } = require("../lib/firebase-admin");

const VND_PER_USD = 25949.123093;
const MAX_QUANTITY_PER_PRODUCT = 50;
const QR_RESERVATION_MS = 30 * 60 * 1000;
const COD_RESERVATION_MS = 24 * 60 * 60 * 1000;
const ORDER_LOOKUP_WINDOW_MS = 10 * 60 * 1000;
const ORDER_LOOKUP_MAX_ATTEMPTS = 20;

function normalizePhone(value) {
    return String(value || "").replace(/\D/g, "");
}

function vnd(value) {
    return Math.round(Number(value) * VND_PER_USD);
}

function priceInUsd(value) {
    const price = Number(value);
    return price > 10000 ? price / VND_PER_USD : price;
}

function aggregateItems(items) {
    const quantities = new Map();
    (Array.isArray(items) ? items : []).forEach((item) => {
        const id = String(item?.id ?? "");
        quantities.set(id, (quantities.get(id) || 0) + Math.max(1, Number(item?.quantity) || 1));
    });
    return Array.from(quantities, ([id, quantity]) => ({ id, quantity }));
}

async function expireOneReservation(db, orderRef, now) {
    return db.runTransaction(async (transaction) => {
        const orderSnapshot = await transaction.get(orderRef);
        if (!orderSnapshot.exists) return false;
        const order = orderSnapshot.data();
        const expiresAt = order.reservationExpiresAt;
        // Only orders still waiting for the shop's confirmation expire; once an admin has
        // confirmed or shipped an order, its stock stays reserved. Older confirmed orders may
        // still carry a hold timer: drop it so they stop matching the cleanup query.
        if (order.inventoryReserved === true && order.status !== "Chờ xác nhận" && expiresAt) {
            transaction.update(orderRef, { reservationExpiresAt: FieldValue.delete() });
            return false;
        }
        if (order.inventoryReserved !== true || typeof expiresAt?.toMillis !== "function"
            || expiresAt.toMillis() > now.toMillis()
            || (order.paymentMethod === "qr" && order.paymentVerified === true)) return false;

        const items = aggregateItems(order.items);
        const shouldRestoreStock = order.status !== "Đã giao";
        const productRefs = shouldRestoreStock
            ? items.map((item) => db.collection("products").doc(item.id))
            : [];
        const productSnapshots = [];
        for (const productRef of productRefs) productSnapshots.push(await transaction.get(productRef));

        const expiredAt = new Date(now.toMillis()).toISOString();
        const status = shouldRestoreStock ? "Đã hủy" : order.status;
        const location = shouldRestoreStock ? "Đơn hàng hết thời hạn giữ sản phẩm" : order.currentLocation || "";
        productSnapshots.forEach((snapshot, index) => {
            if (!snapshot.exists) return;
            const currentQuantity = Number(snapshot.data().quantity) || 0;
            transaction.update(snapshot.ref, { quantity: currentQuantity + items[index].quantity });
        });

        transaction.update(orderRef, {
            status,
            currentLocation: location,
            inventoryReserved: false,
            inventoryState: shouldRestoreStock ? "released" : "sold",
            reservationExpiredAt: Timestamp.fromMillis(now.toMillis()),
            ...(order.paymentMethod === "qr" && order.paymentVerified !== true
                ? { paymentStatus: "Hết hạn chờ thanh toán" }
                : {}),
            trackingUpdatedAt: Timestamp.fromMillis(now.toMillis()),
            trackingHistory: [...(Array.isArray(order.trackingHistory) ? order.trackingHistory : []), {
                status,
                location,
                estimatedDelivery: "",
                updatedAt: expiredAt
            }]
        });
        return true;
    });
}

async function expireReservations(db, maxOrders = 500) {
    let released = 0;
    const batchSize = 100;
    while (released < maxOrders) {
        const now = Timestamp.now();
        const snapshot = await db.collection("orders")
            .where("inventoryReserved", "==", true)
            .where("paymentVerified", "==", false)
            .where("reservationExpiresAt", "<=", now)
            .orderBy("reservationExpiresAt", "asc")
            .limit(Math.min(batchSize, maxOrders - released))
            .get();
        if (snapshot.empty) break;
        const results = await Promise.all(snapshot.docs.map((document) => expireOneReservation(db, document.ref, now)));
        const batchReleased = results.filter(Boolean).length;
        released += batchReleased;
        if (batchReleased === 0) break;
    }
    return released;
}

// Cleanup must never block quoting or ordering: log and continue.
async function releaseExpiredReservations(db) {
    try {
        await expireReservations(db);
    } catch (error) {
        console.error("Reservation cleanup failed:", error.code || error.message);
    }
}

function serializeTimestamp(value) {
    if (value && typeof value.toDate === "function") return value.toDate().toISOString();
    return typeof value === "string" ? value : "";
}

function publicTrackingOrder(id, data) {
    return {
        id,
        status: data.status || "Chờ xác nhận",
        estimatedDelivery: data.estimatedDelivery || "",
        trackingHistory: Array.isArray(data.trackingHistory)
            ? data.trackingHistory.map((event) => ({
                status: event.status || data.status || "Chờ xác nhận",
                estimatedDelivery: event.estimatedDelivery || "",
                updatedAt: serializeTimestamp(event.updatedAt) || event.updatedAt || ""
            }))
            : [],
        privacyLimited: true
    };
}

async function checkOrderLookupLimit(db, req) {
    const forwardedFor = String(req.headers["x-forwarded-for"] || req.headers["x-real-ip"] || req.socket?.remoteAddress || "");
    const clientIp = forwardedFor.split(",")[0].trim();
    if (!clientIp) throw new Error("Client IP is unavailable for order lookup rate limiting.");

    const secret = process.env.FIREBASE_SERVICE_ACCOUNT_JSON;
    if (!secret) throw new Error("Firebase service credentials are required for order lookup rate limiting.");
    const key = createHmac("sha256", secret).update(clientIp).digest("hex");
    const limitRef = db.collection("orderLookupLimits").doc(key);
    const now = Timestamp.now();

    return db.runTransaction(async (transaction) => {
        const snapshot = await transaction.get(limitRef);
        const record = snapshot.exists ? snapshot.data() : {};
        const windowStartedAt = record.windowStartedAt?.toMillis?.() || 0;
        const sameWindow = now.toMillis() - windowStartedAt < ORDER_LOOKUP_WINDOW_MS;
        const attempts = sameWindow ? Number(record.attempts) || 0 : 0;
        if (attempts >= ORDER_LOOKUP_MAX_ATTEMPTS) {
            return {
                allowed: false,
                retryAfter: Math.max(1, Math.ceil((windowStartedAt + ORDER_LOOKUP_WINDOW_MS - now.toMillis()) / 1000))
            };
        }

        transaction.set(limitRef, {
            windowStartedAt: sameWindow ? record.windowStartedAt : now,
            attempts: attempts + 1,
            expiresAt: Timestamp.fromMillis(now.toMillis() + 24 * 60 * 60 * 1000)
        });
        return { allowed: true, retryAfter: 0 };
    });
}

function validateCheckout(body = {}) {
    const customerName = String(body.customerName || "").trim();
    const phone = String(body.phone || "").trim();
    const address = String(body.address || "").trim();
    const paymentMethod = String(body.paymentMethod || "");
    const promotionId = String(body.promotionId || "").trim();
    const idempotencyKey = String(body.idempotencyKey || "").trim();
    const items = body.items;
    const expectedTotals = body.expectedTotals;

    if (!customerName || customerName.length > 100
        || !address || address.length > 500
        || normalizePhone(phone).length < 8 || normalizePhone(phone).length > 15
        || !["qr", "cod"].includes(paymentMethod)
        || !/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(idempotencyKey)
        || promotionId.length > 100
        || !expectedTotals
        || !["subtotal", "savings", "tax", "total"].every((key) => (
            Number.isSafeInteger(expectedTotals[key]) && expectedTotals[key] >= 0
        ))
        || !Array.isArray(items) || items.length < 1 || items.length > 50) {
        return null;
    }

    const normalizedItems = [];
    const seenIds = new Set();
    for (const item of items) {
        const id = String(item?.id ?? "").trim();
        const quantity = Number(item?.quantity);
        if (!id || id.length > 128 || id.includes("/") || seenIds.has(id)
            || !Number.isInteger(quantity) || quantity < 1 || quantity > MAX_QUANTITY_PER_PRODUCT) {
            return null;
        }
        seenIds.add(id);
        normalizedItems.push({ id, quantity });
    }

    return {
        customerName,
        phone,
        address,
        paymentMethod,
        promotionId,
        idempotencyKey,
        paymentReference: String(body.paymentReference || "").replace(/[^A-Za-z0-9_-]/g, "").slice(0, 50),
        expectedTotals,
        items: normalizedItems
    };
}

function validateQuote(body = {}) {
    const promotionId = String(body.promotionId || "").trim();
    if (promotionId.length > 100 || !Array.isArray(body.items) || body.items.length < 1 || body.items.length > 50) return null;
    const seenIds = new Set();
    const items = [];
    for (const item of body.items) {
        const id = String(item?.id ?? "").trim();
        const quantity = Number(item?.quantity);
        if (!id || id.length > 128 || id.includes("/") || seenIds.has(id)
            || !Number.isInteger(quantity) || quantity < 1 || quantity > MAX_QUANTITY_PER_PRODUCT) return null;
        seenIds.add(id);
        items.push({ id, quantity });
    }
    return { items, promotionId };
}

async function quoteOrder(req, res) {
    const quote = validateQuote(req.body);
    if (!quote) return sendError(res, 400, "invalid-checkout", "Danh sách sản phẩm không hợp lệ.");
    try {
        const db = getAdminFirestore();
        await releaseExpiredReservations(db);
        const productRefs = quote.items.map((item) => db.collection("products").doc(item.id));
        const [products, promotionsSnapshot] = await Promise.all([
            db.getAll(...productRefs),
            db.collection("metadata").doc("activePromotions").get()
        ]);
        // Report every product that cannot be sold in the asked quantity, with what is left, so the
        // checkout can tell the customer exactly what to change.
        const shortages = [];
        const cart = quote.items.map((item, index) => {
            const snapshot = products[index];
            const data = snapshot.exists ? snapshot.data() : {};
            const price = priceInUsd(data.price);
            const stock = Number(data.quantity);
            if (!snapshot.exists || !Number.isFinite(price) || price <= 0 || !Number.isInteger(stock) || stock < item.quantity) {
                shortages.push({
                    id: item.id,
                    title: String(data.title || "").slice(0, 120),
                    available: snapshot.exists && Number.isInteger(stock) && Number.isFinite(price) && price > 0 ? Math.max(0, stock) : 0
                });
            }
            return { ...item, data, price, stock };
        });
        if (shortages.length) {
            const message = shortages.map((item) => item.available
                ? `“${item.title || "Một sản phẩm"}” chỉ còn ${item.available} sản phẩm`
                : `“${item.title || "Một sản phẩm"}” đã hết hàng`).join("; ");
            return res.status(409).json({
                code: "insufficient-stock",
                error: `${message}. Hãy giảm số lượng hoặc xóa sản phẩm đó khỏi giỏ.`,
                items: shortages
            });
        }
        const promotions = Array.isArray(promotionsSnapshot.data()?.items) ? promotionsSnapshot.data().items : [];
        const promotion = quote.promotionId
            ? promotions.find((item) => String(item.id) === quote.promotionId && item.active === true)
            : null;
        if (quote.promotionId && !promotion) {
            const error = new Error("Ưu đãi đã hết hạn hoặc không hợp lệ. Hãy tải lại trang thanh toán.");
            error.code = "invalid-promotion";
            throw error;
        }
        const discountPercent = promotion ? Number(promotion.discountPercent) : 0;
        if (promotion && (!Number.isFinite(discountPercent) || discountPercent <= 0 || discountPercent > 100)) {
            const error = new Error("Ưu đãi hiện không hợp lệ.");
            error.code = "invalid-promotion";
            throw error;
        }
        const eligibleIds = new Set(Array.isArray(promotion?.productIds) ? promotion.productIds.map(String) : []);
        const appliesToAll = promotion?.allProducts === true;
        const subtotalUsd = cart.reduce((sum, item) => sum + item.price * item.quantity, 0);
        const savingsUsd = cart.reduce((sum, item) => sum + (promotion && (appliesToAll || eligibleIds.has(item.id))
            ? item.price * item.quantity * discountPercent / 100
            : 0), 0);
        const subtotal = vnd(subtotalUsd);
        const savings = Math.min(subtotal, vnd(savingsUsd));
        return res.status(200).json({
            totals: { subtotal, savings, tax: 0, total: subtotal - savings },
            items: cart.map((item) => ({
                id: item.id,
                title: String(item.data.title || "Sản phẩm").slice(0, 120),
                image: String(item.data.image || "").slice(0, 1000),
                quantity: item.quantity,
                price: vnd(item.price),
                stock: item.stock
            }))
        });
    } catch (error) {
        if (["invalid-promotion", "insufficient-stock", "product-unavailable"].includes(error.code)) {
            return sendError(res, 409, error.code, error.message);
        }
        console.error("Order quote failed:", error.code || error.message);
        return sendError(res, 503, "order-service-unavailable", "Chưa thể kiểm tra giá đơn hàng. Vui lòng thử lại sau.");
    }
}

async function optionalAuthenticatedUser(req) {
    const authorization = req.headers.authorization || "";
    const match = authorization.match(/^Bearer\s+(.+)$/i);
    if (!match) return null;
    return getAdminAuth().verifyIdToken(match[1], true);
}

async function createOrder(req, res) {
    const checkout = validateCheckout(req.body);
    if (!checkout) {
        return sendError(res, 400, "invalid-checkout", "Thông tin đặt hàng hoặc danh sách sản phẩm không hợp lệ.");
    }

    try {
        const user = await optionalAuthenticatedUser(req);
        const db = getAdminFirestore();
        await releaseExpiredReservations(db);
        const idempotencyRef = db.collection("orderRequests").doc(checkout.idempotencyKey);
        const productRefs = checkout.items.map((item) => db.collection("products").doc(item.id));
        const promotionsRef = db.collection("metadata").doc("activePromotions");
        const now = new Date();

        const order = await db.runTransaction(async (transaction) => {
            const previousRequest = await transaction.get(idempotencyRef);
            if (previousRequest.exists) {
                const previousOrderRef = db.collection("orders").doc(String(previousRequest.data().orderId || ""));
                const previousOrder = await transaction.get(previousOrderRef);
                if (!previousOrder.exists) {
                    const error = new Error("Yêu cầu đặt hàng này đã được xử lý nhưng đơn không còn tồn tại.");
                    error.code = "idempotency-conflict";
                    throw error;
                }
                const previousData = previousOrder.data();
                return {
                    id: previousOrder.id,
                    subtotal: previousData.subtotal,
                    savings: previousData.savings,
                    total: previousData.total
                };
            }

            const [productSnapshots, promotionSnapshot] = await Promise.all([
                transaction.getAll(...productRefs),
                transaction.get(promotionsRef)
            ]);

            const products = checkout.items.map((item, index) => {
                const snapshot = productSnapshots[index];
                if (!snapshot.exists) {
                    const error = new Error("Một sản phẩm không còn bán.");
                    error.code = "product-unavailable";
                    throw error;
                }
                const data = snapshot.data();
                const price = priceInUsd(data.price);
                const stock = Number(data.quantity);
                if (!Number.isFinite(price) || price <= 0 || !Number.isInteger(stock) || stock < item.quantity) {
                    const error = new Error("Sản phẩm không đủ tồn kho hoặc có giá chưa hợp lệ.");
                    error.code = "insufficient-stock";
                    throw error;
                }
                return { ...item, ref: productRefs[index], data, price, stock };
            });

            const promotions = Array.isArray(promotionSnapshot.data()?.items)
                ? promotionSnapshot.data().items
                : [];
            const promotion = checkout.promotionId
                ? promotions.find((item) => String(item.id) === checkout.promotionId && item.active === true)
                : null;
            if (checkout.promotionId && !promotion) {
                const error = new Error("Ưu đãi đã hết hạn hoặc không hợp lệ. Hãy tải lại trang thanh toán.");
                error.code = "invalid-promotion";
                throw error;
            }

            const discountPercent = promotion ? Number(promotion.discountPercent) : 0;
            if (promotion && (!Number.isFinite(discountPercent) || discountPercent <= 0 || discountPercent > 100)) {
                const error = new Error("Ưu đãi hiện không hợp lệ.");
                error.code = "invalid-promotion";
                throw error;
            }
            const eligibleIds = new Set(Array.isArray(promotion?.productIds) ? promotion.productIds.map(String) : []);
            const appliesToAll = promotion?.allProducts === true;
            const subtotalUsd = products.reduce((sum, item) => sum + item.price * item.quantity, 0);
            const savingsUsd = products.reduce((sum, item) => (
                sum + (promotion && (appliesToAll || eligibleIds.has(item.id))
                    ? item.price * item.quantity * discountPercent / 100
                    : 0)
            ), 0);
            const subtotal = vnd(subtotalUsd);
            const savings = Math.min(subtotal, vnd(savingsUsd));
            const total = subtotal - savings;
            if (checkout.expectedTotals.subtotal !== subtotal
                || checkout.expectedTotals.savings !== savings
                || checkout.expectedTotals.tax !== 0
                || checkout.expectedTotals.total !== total) {
                const error = new Error("Giá hoặc tồn kho vừa thay đổi. Hãy tải lại báo giá trước khi xác nhận.");
                error.code = "price-changed";
                throw error;
            }
            const orderRef = db.collection("orders").doc();
            const paymentReference = checkout.paymentReference || `FF${orderRef.id.slice(-12).toUpperCase()}`;
            const items = products.map((item) => ({
                id: item.id,
                title: String(item.data.title || "Sản phẩm").slice(0, 120),
                image: String(item.data.image || "").slice(0, 1000),
                quantity: item.quantity,
                price: vnd(item.price),
                stock: item.stock
            }));
            const initialStatus = "Chờ xác nhận";
            const initialLocation = "Đơn hàng đã được gửi đến cửa hàng";
            const orderData = {
                uid: user?.uid || "",
                email: String(user?.email || "").slice(0, 254),
                customerName: checkout.customerName,
                phone: checkout.phone,
                address: checkout.address,
                createdAt: FieldValue.serverTimestamp(),
                status: initialStatus,
                currentLocation: initialLocation,
                estimatedDelivery: "",
                trackingHistory: [{ status: initialStatus, location: initialLocation, estimatedDelivery: "", updatedAt: now.toISOString() }],
                subtotal,
                savings,
                tax: 0,
                total,
                paymentMethod: checkout.paymentMethod,
                paymentStatus: checkout.paymentMethod === "qr" ? "Chờ xác minh thanh toán" : "Thanh toán khi nhận hàng",
                paymentReference,
                paidAt: "",
                paymentVerified: false,
                promotionId: promotion ? checkout.promotionId : "",
                orderNote: String(req.body.orderNote || "").trim().slice(0, 500),
                items,
                inventoryReserved: true,
                inventoryState: "reserved",
                reservationExpiresAt: Timestamp.fromMillis(now.getTime() + (
                    checkout.paymentMethod === "qr" ? QR_RESERVATION_MS : COD_RESERVATION_MS
                ))
            };

            products.forEach((item) => transaction.update(item.ref, { quantity: item.stock - item.quantity }));
            transaction.create(orderRef, orderData);
            transaction.create(idempotencyRef, {
                orderId: orderRef.id,
                uid: user?.uid || "",
                createdAt: FieldValue.serverTimestamp()
            });
            return { ...orderData, id: orderRef.id, createdAt: now.toISOString() };
        });

        return res.status(201).json({ order: { id: order.id }, totals: {
            subtotal: order.subtotal,
            savings: order.savings,
            total: order.total
        } });
    } catch (error) {
        if (String(error.code || "").startsWith("auth/")) {
            return sendError(res, 401, "unauthenticated", "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại rồi đặt hàng.");
        }
        if (["invalid-checkout", "invalid-promotion", "insufficient-stock", "product-unavailable", "price-changed", "idempotency-conflict"].includes(error.code)) {
            const status = error.code === "invalid-checkout" ? 400 : 409;
            return sendError(res, status, error.code, error.message);
        }
        console.error("Order creation failed:", error.code || error.message);
        return sendError(res, 503, "order-service-unavailable", "Chưa thể xác nhận đơn hàng. Vui lòng thử lại sau.");
    }
}

async function lookupOrder(req, res) {
    try {
        const db = getAdminFirestore();
        const limit = await checkOrderLookupLimit(db, req);
        if (!limit.allowed) {
            res.setHeader("Retry-After", String(limit.retryAfter));
            return sendError(res, 429, "rate-limited", "Bạn đã tra cứu quá nhiều lần. Vui lòng thử lại sau.");
        }

        const id = String(req.body?.id || "").trim().replace(/^#/, "");
        const phone = normalizePhone(req.body?.phone);
        if (!id || id.length > 128 || id.includes("/") || phone.length < 8 || phone.length > 15) {
            return sendError(res, 400, "invalid-argument", "Mã đơn hàng hoặc số điện thoại không hợp lệ.");
        }

        const snapshot = await db.collection("orders").doc(id).get();
        if (!snapshot.exists || normalizePhone(snapshot.get("phone")) !== phone) {
            return sendError(res, 404, "order-not-found", "Không tìm thấy đơn hàng. Hãy kiểm tra lại mã đơn và số điện thoại.");
        }
        return res.status(200).json({ order: publicTrackingOrder(snapshot.id, snapshot.data()) });
    } catch (error) {
        console.error("Order lookup failed:", error.code || error.message);
        return sendError(res, 503, "order-service-unavailable", "Chưa thể tra cứu đơn hàng. Vui lòng thử lại sau.");
    }
}

module.exports = async function handler(req, res) {
    res.setHeader("Cache-Control", "no-store");
    if (req.method === "POST") {
        if (req.body?.action === "quote") return quoteOrder(req, res);
        if (req.body?.action === "lookup") return lookupOrder(req, res);
        return createOrder(req, res);
    }
    if (req.method !== "GET") {
        res.setHeader("Allow", "GET, POST");
        return sendError(res, 405, "method-not-allowed", "API đơn hàng chỉ chấp nhận phương thức GET hoặc POST.");
    }

    if (req.query?.action === "expire") {
        const match = String(req.headers.authorization || "").match(/^Bearer\s+(.+)$/i);
        let authorized = Boolean(process.env.CRON_SECRET && match?.[1] === process.env.CRON_SECRET);
        if (!authorized && match?.[1]) {
            try {
                const claims = await getAdminAuth().verifyIdToken(match[1], true);
                authorized = claims.admin === true;
            } catch (error) {
                console.error("Reservation cleanup authentication failed:", error.code || error.message);
            }
        }
        if (!authorized) return sendError(res, 401, "unauthenticated", "Quyền dọn đơn hết hạn không hợp lệ.");
        try {
            const released = await expireReservations(getAdminFirestore());
            return res.status(200).json({ released });
        } catch (error) {
            console.error("Reservation cleanup failed:", error.code || error.message);
            return sendError(res, 503, "order-service-unavailable", "Chưa thể giải phóng tồn kho đơn hết hạn.");
        }
    }

    return sendError(res, 405, "method-not-allowed", "Tra cứu đơn hàng cần dùng phương thức POST.");
};

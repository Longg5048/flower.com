const { getAdminFirestore, sendError } = require("../lib/firebase-admin");

function normalizePhone(value) {
    return String(value || "").replace(/\D/g, "");
}

function serializeTimestamp(value) {
    return value && typeof value.toDate === "function"
        ? value.toDate().toISOString()
        : "";
}

function publicOrder(id, data) {
    return {
        id,
        customerName: data.customerName || "",
        phone: data.phone || "",
        address: data.address || "",
        createdAt: serializeTimestamp(data.createdAt),
        status: data.status || "Chờ xác nhận",
        currentLocation: data.currentLocation || "",
        estimatedDelivery: data.estimatedDelivery || "",
        trackingHistory: Array.isArray(data.trackingHistory)
            ? data.trackingHistory.map((event) => ({
                ...event,
                updatedAt: serializeTimestamp(event.updatedAt) || event.updatedAt || ""
            }))
            : [],
        subtotal: Number(data.subtotal) || 0,
        savings: Number(data.savings) || 0,
        tax: Number(data.tax) || 0,
        total: Number(data.total) || 0,
        paymentMethod: data.paymentMethod || "",
        paymentStatus: data.paymentStatus || "Chưa xác nhận",
        paidAt: data.paidAt || "",
        items: Array.isArray(data.items) ? data.items : []
    };
}

module.exports = async function handler(req, res) {
    res.setHeader("Cache-Control", "no-store");
    if (req.method !== "GET") {
        res.setHeader("Allow", "GET");
        return sendError(res, 405, "method-not-allowed", "Use GET for this endpoint.");
    }

    const id = String(req.query?.id || "").trim();
    const phone = normalizePhone(req.query?.phone);
    if (!id || id.length > 128 || id.includes("/") || phone.length < 8 || phone.length > 15) {
        return sendError(res, 400, "invalid-argument", "A valid order number and phone number are required.");
    }

    try {
        const snapshot = await getAdminFirestore().collection("orders").doc(id).get();
        if (!snapshot.exists || normalizePhone(snapshot.get("phone")) !== phone) {
            return sendError(res, 404, "order-not-found", "Order was not found.");
        }
        return res.status(200).json({ order: publicOrder(snapshot.id, snapshot.data()) });
    } catch (error) {
        console.error("Order lookup failed:", error.code || error.message);
        return sendError(res, 503, "order-service-unavailable", "Order lookup is temporarily unavailable.");
    }
};

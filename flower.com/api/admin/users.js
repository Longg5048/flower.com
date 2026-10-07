const { getApps, initializeApp, cert } = require("firebase-admin/app");
const { getAuth } = require("firebase-admin/auth");

function sendError(res, status, code, message) {
    return res.status(status).json({ code, error: message });
}

function getAdminAuth() {
    const serviceAccountJson = process.env.FIREBASE_SERVICE_ACCOUNT_JSON;
    if (!serviceAccountJson) {
        const error = new Error("FIREBASE_SERVICE_ACCOUNT_JSON is not configured.");
        error.code = "admin-api-not-configured";
        throw error;
    }

    let serviceAccount;
    try {
        serviceAccount = JSON.parse(serviceAccountJson);
    } catch {
        const error = new Error("FIREBASE_SERVICE_ACCOUNT_JSON is not valid JSON.");
        error.code = "admin-api-not-configured";
        throw error;
    }

    const app = getApps().length
        ? getApps()[0]
        : initializeApp({
            credential: cert(serviceAccount),
            projectId: process.env.FIREBASE_PROJECT_ID || serviceAccount.project_id
        });
    return getAuth(app);
}

module.exports = async function handler(req, res) {
    res.setHeader("Cache-Control", "no-store");

    if (req.method !== "POST") {
        res.setHeader("Allow", "POST");
        return sendError(res, 405, "method-not-allowed", "Use POST for this endpoint.");
    }

    const authorization = req.headers.authorization || "";
    const match = authorization.match(/^Bearer ([^\s]+)$/i);
    if (!match) {
        return sendError(res, 401, "unauthenticated", "A Firebase ID token is required.");
    }

    let adminAuth;
    try {
        adminAuth = getAdminAuth();
    } catch (error) {
        console.error("Firebase Admin API configuration error:", error.message);
        return sendError(
            res,
            503,
            "admin-api-not-configured",
            "The Firebase Admin API is not configured."
        );
    }

    let decodedToken;
    try {
        decodedToken = await adminAuth.verifyIdToken(match[1]);
    } catch (error) {
        console.error("Firebase ID token verification failed:", error.code || error.message);
        return sendError(res, 401, "unauthenticated", "The Firebase ID token is invalid or expired.");
    }

    if (decodedToken.admin !== true) {
        return sendError(res, 403, "permission-denied", "Administrator access is required.");
    }

    const pageToken = req.body && req.body.pageToken;
    if (pageToken !== undefined && pageToken !== null &&
        (typeof pageToken !== "string" || pageToken.length > 2048)) {
        return sendError(res, 400, "invalid-argument", "Invalid pagination token.");
    }

    try {
        const result = await adminAuth.listUsers(100, pageToken || undefined);
        return res.status(200).json({
            users: result.users.map((user) => ({
                uid: user.uid,
                email: user.email || "",
                displayName: user.displayName || "",
                providers: user.providerData.map((provider) => provider.providerId),
                creationTime: user.metadata.creationTime || "",
                lastSignInTime: user.metadata.lastSignInTime || "",
                disabled: user.disabled
            })),
            nextPageToken: result.pageToken || null
        });
    } catch (error) {
        console.error("Firebase user listing failed:", error.code || error.message);
        return sendError(res, 500, "user-list-failed", "Could not load Firebase users.");
    }
};

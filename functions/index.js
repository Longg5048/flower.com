const { initializeApp, getApps } = require("firebase-admin/app");
const { getAuth } = require("firebase-admin/auth");
const { HttpsError, onCall } = require("firebase-functions/v2/https");

if (!getApps().length) {
    initializeApp();
}

exports.listFirebaseUsers = onCall(async (request) => {
    if (!request.auth) {
        throw new HttpsError("unauthenticated", "Authentication is required.");
    }
    if (request.auth.token.admin !== true) {
        throw new HttpsError("permission-denied", "Administrator access is required.");
    }

    const pageToken = request.data?.pageToken;
    if (pageToken !== undefined && pageToken !== null &&
        (typeof pageToken !== "string" || pageToken.length > 2048)) {
        throw new HttpsError("invalid-argument", "Invalid pagination token.");
    }

    const result = await getAuth().listUsers(100, pageToken || undefined);
    return {
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
    };
});

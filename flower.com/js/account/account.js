import { firebaseConfig, connectFirebaseEmulators } from "../core/firebase-config.js";
import { logAuthError } from "./auth-logging.js";
import { isLocalAuth, localAuthRequest, persistLocalAccount } from "./local-auth-client.js?v=2";
import { monitorLocalSession, invalidateLocalSession } from "./session-lifecycle.js?v=1";

function getStoredAccount() {
    try {
        const account = window.FlowerSafeStorage.readJson("loggedUser", {
            fallback: null,
            validate: (value) => value !== null && typeof value === "object" && !Array.isArray(value)
        });
        return account && typeof account === "object" ? account : null;
    } catch (error) {
        console.error("Không thể đọc phiên tài khoản:", error);
        return null;
    }
}

async function restoreFirebaseAccount() {
    if (isLocalAuth) {
        try { return (await localAuthRequest("session")).account; }
        catch (error) { if (error.status === 401) return null; throw error; }
    }
    const [appSdk, authSdk] = await Promise.all([
        import("https://www.gstatic.com/firebasejs/11.6.0/firebase-app.js"),
        import("https://www.gstatic.com/firebasejs/11.6.0/firebase-auth.js")
    ]);
    const app = appSdk.getApps().length ? appSdk.getApp() : appSdk.initializeApp(firebaseConfig);
    const auth = authSdk.getAuth(app);
    connectFirebaseEmulators(app, auth, null, authSdk);
    await auth.authStateReady();

    const user = auth.currentUser;
    if (!user) return null;

    const provider = user.providerData.some((item) => item.providerId === "google.com")
        ? "google"
        : "password";
    return {
        name: user.displayName || "Khách hàng",
        email: user.email || "",
        phone: user.phoneNumber || "",
        uid: user.uid,
        authProvider: provider,
        role: "customer"
    };
}

let pageIsActive = true;
let localMonitor;
function hidePrivateAccount() {
    document.querySelector(".account-details").hidden = true;
    document.querySelectorAll(".account-details dd").forEach(element => { element.textContent = ""; });
    document.getElementById("logout-button").disabled = true;
}
window.addEventListener("pagehide", () => { pageIsActive = false; hidePrivateAccount(); });
window.addEventListener("pageshow", event => {
    pageIsActive = true;
    if (event.persisted) {
        hidePrivateAccount();
        if (isLocalAuth) void Promise.resolve(localMonitor?.check(true)).then(current => {
            if (!current) window.location.replace("./login.html?session=expired");
        });
        else window.location.reload();
    }
});

let account = getStoredAccount();
let sessionUnavailable = false;
if (isLocalAuth) {
    // A localStorage profile cannot keep a protected page open when verification fails.
    try {
        account = (await localAuthRequest("session")).account || null;
        if (account) persistLocalAccount(account);
        else invalidateLocalSession();
    } catch (error) {
        account = null;
        sessionUnavailable = error.status !== 401;
        invalidateLocalSession(sessionUnavailable ? "unavailable" : "expired");
    }
} else {
    if (account) {
        // The stored copy can outlive the Firebase session (password reset, disabled account).
        try {
            const firebaseAccount = await restoreFirebaseAccount();
            if (!firebaseAccount || (account.uid && firebaseAccount.uid !== account.uid)) {
                window.FlowerSafeStorage.remove("loggedUser");
                window.FlowerSafeStorage.remove("loggedAdmin");
                account = null;
            }
        } catch (error) {
            logAuthError("Chưa kiểm tra được phiên đăng nhập:", error);
            account = null;
            sessionUnavailable = true;
            window.FlowerSafeStorage.remove("loggedUser");
            window.FlowerSafeStorage.remove("loggedAdmin");
        }
    }
    if (!account) {
        try {
            account = await restoreFirebaseAccount();
            if (account) {
                window.FlowerSafeStorage.writeJson("loggedUser", account);
            }
        } catch (error) {
            logAuthError("Không thể khôi phục phiên Firebase:", error);
            document.getElementById("account-message").textContent =
                "Không thể xác minh phiên đăng nhập. Vui lòng đăng nhập lại.";
        }
    }
}

if (!account || account.role === "admin" || account.role === "owner") {
    window.location.replace(account?.role === "owner"
        ? "./owner.html"
        : account?.role === "admin" ? "./admin.html" : `./login.html?session=${sessionUnavailable ? "unavailable" : "expired"}`);
} else {
    document.querySelector(".account-details").hidden = false;
    document.getElementById("logout-button").disabled = false;
    document.getElementById("account-message").textContent = "";
    document.getElementById("account-name").textContent = account.name || "Chưa cập nhật";
    document.getElementById("account-email").textContent = account.email || "Chưa cập nhật";
    document.getElementById("account-phone").textContent = account.phone || "Chưa cập nhật";
    document.getElementById("account-provider").textContent = account.authProvider === "google"
        ? "Google"
        : account.authProvider === "password"
        ? "Thư điện tử và mật khẩu"
            : "Tài khoản tự tạo trên máy";
    if (isLocalAuth) {
        document.getElementById("account-email").closest("div").querySelector("dt").textContent = "Tên đăng nhập";
    }
}

document.getElementById("logout-button").addEventListener("click", async () => {
    const message = document.getElementById("account-message");
    const button = document.getElementById("logout-button");
    if (button.disabled) return;
    button.disabled = true;
    if (isLocalAuth) {
        try { await localAuthRequest("logout"); }
        catch (error) { message.textContent = error.message; button.disabled = false; return; }
        hidePrivateAccount();
        invalidateLocalSession("logout");
    }

    if (account?.authProvider === "google" || account?.authProvider === "password") {
        try {
            const [appSdk, authSdk] = await Promise.all([
                import("https://www.gstatic.com/firebasejs/11.6.0/firebase-app.js"),
                import("https://www.gstatic.com/firebasejs/11.6.0/firebase-auth.js")
            ]);
            const app = appSdk.getApps().length ? appSdk.getApp() : appSdk.initializeApp(firebaseConfig);
            await authSdk.signOut(authSdk.getAuth(app));
        } catch (error) {
            logAuthError("Không thể đăng xuất khỏi Firebase:", error);
            message.textContent = "Chưa thể đăng xuất do lỗi kết nối. Vui lòng thử lại.";
            button.disabled = false;
            return;
        }
    }

    try {
        window.FlowerSafeStorage.remove("loggedUser", { required: true });
        window.FlowerSafeStorage.remove("loggedAdmin");
    } catch (error) {
        console.error("Không thể xóa phiên tài khoản trên trình duyệt:", error);
        message.textContent = "Chưa thể xóa phiên đăng nhập trên trình duyệt này. Hãy thử lại trước khi rời trang.";
        button.disabled = false;
        return;
    }
    window.location.assign("./login.html");
});

if (isLocalAuth && account) {
    const pageUid = account.uid;
    localMonitor = monitorLocalSession({
        initialAccount: account,
        onVerified(current) {
            if (!pageIsActive) return;
            if (current.uid !== pageUid) { hidePrivateAccount(); window.location.replace("./account.html"); return; }
            account = current;
            document.getElementById("account-name").textContent = account.name;
            document.getElementById("account-email").textContent = account.email;
            document.getElementById("account-phone").textContent = account.phone;
            document.getElementById("account-provider").textContent = "Tài khoản tự tạo trên máy";
            document.querySelector(".account-details").hidden = false;
            document.getElementById("logout-button").disabled = false;
        },
        onExpired(reason) {
            hidePrivateAccount();
            if (reason !== "logout") window.location.replace(`./login.html?session=${reason === "unavailable" ? "unavailable" : "expired"}`);
        }
    });
}

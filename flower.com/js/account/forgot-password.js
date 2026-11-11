import { firebaseConfig, isFirebaseConfigured, connectFirebaseEmulators } from "../core/firebase-config.js";
import { logAuthError } from "./auth-logging.js";
import { isLocalAuth } from "./local-auth-client.js?v=2";

const form = document.getElementById("recovery-form");
const emailInput = document.getElementById("recovery-email");
const message = document.getElementById("recovery-message");
const submitButton = document.getElementById("recovery-submit");

function showMessage(text, isError = true) {
    message.textContent = text;
    message.classList.toggle("status-error", isError);
    message.classList.toggle("status-success", !isError);
}

if (!window.FlowerSafeStorage.remove("usersData")) {
    showMessage("Không xóa được mật khẩu cũ từng lưu trên trình duyệt. Hãy xóa dữ liệu trang web trong cài đặt trình duyệt.");
}

function getFirebaseErrorMessage(error) {
    const messages = {
        "auth/invalid-email": "Địa chỉ thư điện tử không hợp lệ.",
        "auth/invalid-api-key": "Cấu hình Firebase không hợp lệ. Kiểm tra firebase-config.js.",
        "auth/network-request-failed": "Không kết nối được Firebase. Kiểm tra mạng rồi thử lại.",
        "auth/too-many-requests": "Bạn đã thử quá nhiều lần. Vui lòng thử lại sau.",
        "auth/unauthorized-domain": "Tên miền trang web chưa được cấp phép trong Firebase Authentication."
    };
    return messages[error.code] || "Chưa thể gửi liên kết đặt lại mật khẩu. Vui lòng thử lại.";
}

async function sendFirebaseResetEmail(email) {
    const [appSdk, authSdk] = await Promise.all([
        import("https://www.gstatic.com/firebasejs/11.6.0/firebase-app.js"),
        import("https://www.gstatic.com/firebasejs/11.6.0/firebase-auth.js")
    ]);
    const app = appSdk.getApps().length ? appSdk.getApp() : appSdk.initializeApp(firebaseConfig);
    const auth = authSdk.getAuth(app);
    connectFirebaseEmulators(app, auth, null, authSdk);
    await authSdk.sendPasswordResetEmail(auth, email);
}

if (isLocalAuth) {
    form.hidden = true;
    document.getElementById("local-recovery-notice").hidden = false;
    document.getElementById("local-recovery-signup").hidden = false;
} else if (!isFirebaseConfigured) {
    document.getElementById("recovery-intro").textContent =
        "Chức năng đặt lại mật khẩu cần Firebase Authentication. Mật khẩu không được lưu trên trình duyệt này.";
    submitButton.disabled = true;
} else {
    submitButton.disabled = false;
}

const emailFromLogin = new URLSearchParams(window.location.search).get("email");
if (emailFromLogin) emailInput.value = emailFromLogin.trim().toLowerCase();

form.addEventListener("submit", async event => {
    event.preventDefault();
    const email = emailInput.value.trim().toLowerCase();
    if (!emailInput.validity.valid) {
        showMessage("Vui lòng nhập địa chỉ thư điện tử hợp lệ.");
        emailInput.focus();
        return;
    }
    if (isLocalAuth || !isFirebaseConfigured || submitButton.disabled) return;

    submitButton.disabled = true;
    message.textContent = "";
    message.classList.remove("status-error", "status-success");
    try {
        await sendFirebaseResetEmail(email);
        showMessage("Nếu email này đã được đăng ký, bạn sẽ nhận được liên kết đặt lại mật khẩu. Hãy hoàn tất đặt lại mật khẩu để mở lại đăng nhập.", false);
    } catch (error) {
        if (error.code === "auth/user-not-found") {
            showMessage("Nếu email này đã được đăng ký, bạn sẽ nhận được liên kết đặt lại mật khẩu. Hãy hoàn tất đặt lại mật khẩu để mở lại đăng nhập.", false);
        } else {
            logAuthError("Gửi thư khôi phục mật khẩu thất bại:", error);
            showMessage(getFirebaseErrorMessage(error));
        }
    } finally {
        submitButton.disabled = false;
    }
});

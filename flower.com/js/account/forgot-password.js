import "../core/password-policy.js?v=1";
import { firebaseConfig, isFirebaseConfigured, connectFirebaseEmulators } from "../core/firebase-config.js";
import { logAuthError } from "./auth-logging.js";
import { isLocalAuth, localAuthRequest } from "./local-auth-client.js?v=2";
import { recoveryError, restrictPinInputs } from "./recovery-security.js?v=4";

const passwordPolicy = globalThis.FlowerPasswordPolicy;
passwordPolicy.bindPasswordHint(document.getElementById("recovery-new-password"), document.getElementById("recovery-password-hint"));

const form = document.getElementById("recovery-form");
const emailInput = document.getElementById("recovery-email");
const message = document.getElementById("recovery-message");
const submitButton = document.getElementById("recovery-submit");
let openRequiredPinRecovery;

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
    restrictPinInputs();
    document.getElementById("recovery-intro").textContent =
        "Nhập tên đăng nhập, họ tên, số điện thoại đã đăng ký và PIN để đặt mật khẩu mới. Không cần email thật.";
    document.querySelector('label[for="recovery-email"]').textContent = "Tên đăng nhập";
    emailInput.placeholder = "Ví dụ ban@demo";
    emailInput.autocomplete = "username";
    document.getElementById("local-recovery-fields").hidden = false;
    for (const id of ["recovery-name", "recovery-phone", "recovery-pin", "recovery-new-password", "recovery-confirm-password"]) {
        document.getElementById(id).required = true;
    }
    submitButton.textContent = "Đặt lại mật khẩu";
} else if (!isFirebaseConfigured) {
    document.getElementById("recovery-intro").textContent =
        "Chức năng đặt lại mật khẩu cần Firebase Authentication. Mật khẩu không được lưu trên trình duyệt này.";
}

const emailFromLogin = new URLSearchParams(window.location.search).get("email");
if (emailFromLogin) {
    emailInput.value = emailFromLogin.trim().toLowerCase();
}
if (isLocalAuth) {
    try {
        const { account } = await localAuthRequest("session");
        if (account && (!emailInput.value || account.email === emailInput.value)) {
            emailInput.value = account.email;
            for (const [id, value] of [["recovery-name", account.name], ["recovery-phone", account.phone],
                ["pin-profile-name", account.name], ["pin-profile-phone", account.phone]]) document.getElementById(id).value = value || "";
        }
    } catch { /* A forgotten-password flow also works without a login session. */ }
}

form.addEventListener("submit", async (event) => {
    event.preventDefault();
    const email = emailInput.value.trim().toLowerCase();

    if (!emailInput.validity.valid) {
        showMessage("Vui lòng nhập địa chỉ thư điện tử hợp lệ.");
        emailInput.focus();
        return;
    }
    if (isLocalAuth) {
        if (submitButton.disabled) return;
        const newPassword = document.getElementById("recovery-new-password").value;
        if (!passwordPolicy.isStrongPassword(newPassword)) { showMessage(passwordPolicy.message); return; }
        if (newPassword !== document.getElementById("recovery-confirm-password").value) {
            showMessage("Mật khẩu xác nhận không khớp.");
            return;
        }
        submitButton.disabled = true;
        try {
            const legacy = document.getElementById("use-legacy-code").checked;
            await localAuthRequest("reset", {
                email, newPassword, name: document.getElementById("recovery-name").value, phone: document.getElementById("recovery-phone").value,
                ...(legacy ? { recoveryCode: document.getElementById("recovery-code").value }
                    : { pin: document.getElementById("recovery-pin").value })
            });
            window.FlowerSafeStorage.remove("loggedUser");
            window.FlowerSafeStorage.remove("loggedAdmin");
            document.getElementById("local-reset-result").hidden = false;
            form.hidden = true;
        } catch (error) {
            if (["pin-reset-required", "pin-format-update-required"].includes(error.code)) openRequiredPinRecovery(email, error.code);
            else showMessage(recoveryError(error));
        } finally {
            submitButton.disabled = false;
        }
        return;
    }
    if (!isFirebaseConfigured) {
        showMessage("Chưa thể khôi phục tài khoản. Cửa hàng cần cấu hình Firebase Authentication trước.");
        return;
    }

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

if (isLocalAuth) {
    const pinSection = document.getElementById("pin-recovery");
    const pinForm = document.getElementById("pin-recovery-form");
    const pinEmail = document.getElementById("pin-email");
    const pinMessage = document.getElementById("pin-message");
    const backButton = document.getElementById("back-to-password");
    const failedMessage = "Bạn đã nhập sai PIN 5 lần. Còn 0 lượt thử PIN cũ. Hãy xác nhận bằng mật khẩu hiện tại để đặt PIN mới.";
    const formatMessage = "PIN hiện tại dùng độ dài cũ. Hãy xác nhận bằng mật khẩu hiện tại để đặt PIN mới gồm 4–6 chữ số.";
    const genericMessage = "Hãy xác nhận bằng mật khẩu hiện tại để đặt PIN mới gồm 4–6 chữ số.";
    function setMandatory(value) {
        pinEmail.readOnly = value;
        backButton.hidden = value;
        document.getElementById("pin-recovery-title").textContent = value ? "Bắt buộc đặt PIN mới" : "Quên PIN";
    }
    function pinStatus(text, isError = true) {
        pinMessage.textContent = text;
        pinMessage.classList.toggle("status-error", isError);
        pinMessage.classList.toggle("status-success", !isError);
    }
    document.getElementById("use-legacy-code").addEventListener("change", event => {
        const legacy = event.target.checked;
        document.getElementById("recovery-pin").disabled = legacy;
        document.getElementById("recovery-pin").required = !legacy;
        document.getElementById("recovery-code").disabled = !legacy;
        document.getElementById("recovery-code").required = legacy;
    });
    document.getElementById("forgot-pin").addEventListener("click", () => {
        setMandatory(false);
        form.hidden = true;
        pinSection.hidden = false;
        pinForm.hidden = false;
        pinForm.reset();
        pinStatus("");
        pinEmail.value = emailInput.value;
        document.getElementById("pin-profile-name").value = document.getElementById("recovery-name").value;
        document.getElementById("pin-profile-phone").value = document.getElementById("recovery-phone").value;
        pinEmail.focus();
    });
    backButton.addEventListener("click", () => {
        pinSection.hidden = true;
        form.hidden = false;
        emailInput.value = pinEmail.value;
        document.getElementById("recovery-name").value = document.getElementById("pin-profile-name").value;
        document.getElementById("recovery-phone").value = document.getElementById("pin-profile-phone").value;
        pinForm.reset();
        emailInput.focus();
    });
    pinEmail.addEventListener("input", () => {
        for (const id of ["pin-current-password", "forgot-new-pin", "forgot-confirm-pin"]) document.getElementById(id).value = "";
        pinStatus("");
    });
    openRequiredPinRecovery = (email, reason) => {
        form.hidden = true;
        pinSection.hidden = false;
        pinForm.hidden = false;
        pinForm.reset();
        pinEmail.value = email;
        document.getElementById("pin-profile-name").value = document.getElementById("recovery-name").value;
        document.getElementById("pin-profile-phone").value = document.getElementById("recovery-phone").value;
        setMandatory(true);
        pinStatus(reason === "pin-reset-required" ? failedMessage : reason === "pin-format-update-required" ? formatMessage : genericMessage);
        const url = new URL(window.location.href);
        url.searchParams.set("email", email);
        url.searchParams.set("recoverPin", "1");
        if (["pin-reset-required", "pin-format-update-required"].includes(reason)) url.searchParams.set("pinReason", reason);
        window.history.replaceState(null, "", url);
    };
    pinForm.addEventListener("submit", async event => {
        event.preventDefault();
        const button = pinForm.querySelector("button");
        if (button.disabled) return;
        button.disabled = true;
        const email = pinEmail.value.trim().toLowerCase();
        try {
            await localAuthRequest("recover-pin", { email,
                name: document.getElementById("pin-profile-name").value, phone: document.getElementById("pin-profile-phone").value,
                password: document.getElementById("pin-current-password").value,
                newPin: document.getElementById("forgot-new-pin").value,
                confirmPin: document.getElementById("forgot-confirm-pin").value });
            window.FlowerSafeStorage.remove("loggedUser");
            window.FlowerSafeStorage.remove("loggedAdmin");
            emailInput.value = email;
            document.getElementById("recovery-name").value = document.getElementById("pin-profile-name").value;
            document.getElementById("recovery-phone").value = document.getElementById("pin-profile-phone").value;
            setMandatory(false);
            document.getElementById("recovery-pin").value = "";
            showMessage("", false);
            const url = new URL(window.location.href);
            url.searchParams.delete("recoverPin");
            url.searchParams.delete("pinReason");
            window.history.replaceState(null, "", url);
            pinForm.reset();
            pinEmail.value = email;
            document.getElementById("pin-profile-name").value = document.getElementById("recovery-name").value;
            document.getElementById("pin-profile-phone").value = document.getElementById("recovery-phone").value;
            pinForm.hidden = true;
            pinStatus("Đã đặt lại PIN. Bạn có thể đăng nhập bằng mật khẩu hiện tại hoặc dùng PIN mới để khôi phục mật khẩu.", false);
        } catch (error) { pinStatus(recoveryError(error)); }
        finally { button.disabled = false; }
    });
    for (const button of pinSection.querySelectorAll('button[type="submit"]')) button.disabled = false;
    if (emailInput.value && new URLSearchParams(window.location.search).get("recoverPin") === "1") {
        openRequiredPinRecovery(emailInput.value, new URLSearchParams(window.location.search).get("pinReason"));
    }
}
submitButton.disabled = false;

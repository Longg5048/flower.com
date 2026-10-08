function getCurrentAccount() {
    try {
        const account = JSON.parse(localStorage.getItem("loggedUser") || "null");
        return account && typeof account === "object" ? account : null;
    } catch (error) {
        console.error("Không thể đọc phiên tài khoản:", error);
        return null;
    }
}

const accountNav = document.querySelector(".signin");
if (accountNav) {
    const account = getCurrentAccount();
    const label = accountNav.querySelector("h5");

    if (account && label) {
        label.textContent = account.role === "admin"
            ? "Quản trị"
            : account.name || account.email || "Tài khoản";
    }

    accountNav.setAttribute("role", "link");
    accountNav.setAttribute("tabindex", "0");
    accountNav.addEventListener("click", () => {
        const currentAccount = getCurrentAccount();
        if (!currentAccount) {
            window.location.assign("./login.html");
        } else if (currentAccount.role === "admin") {
            window.location.assign("./admin.html");
        } else {
            window.location.assign("./account.html");
        }
    });
    accountNav.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            accountNav.click();
        }
    });
}

const ordersNav = document.querySelector(".orders");
if (ordersNav) {
    ordersNav.setAttribute("role", "link");
    ordersNav.setAttribute("tabindex", "0");
    document.addEventListener("click", (event) => {
        const orderTarget = event.target.closest?.(".orders");
        if (!orderTarget) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        window.location.assign("./tracking.html");
    }, true);
    ordersNav.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            ordersNav.click();
        }
    });
}

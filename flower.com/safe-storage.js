(function initializeSafeStorage(root) {
  const reportedKeys = new Set();
  const labels = {
    cart: "giỏ hàng",
    wish: "danh sách yêu thích",
    flowerCheckout: "thông tin thanh toán",
    flowerCategories: "danh mục",
    flowerPromotions: "ưu đãi",
    loggedUser: "phiên tài khoản",
    flowerMessages: "phản hồi cũ",
    flowerReviews: "đánh giá cũ",
    usersData: "danh sách tài khoản",
    flowerInventory: "kho sản phẩm"
  };

  function showStorageNotice(key) {
    if (reportedKeys.has(key) || !root.document?.body) return;
    reportedKeys.add(key);

    const notice = root.document.createElement("div");
    notice.setAttribute("role", "alert");
    notice.style.cssText = [
      "position:fixed", "z-index:2147483647", "top:12px", "left:50%",
      "transform:translateX(-50%)", "width:min(92vw,720px)", "box-sizing:border-box",
      "padding:14px 18px", "border:1px solid #d97706", "border-radius:10px",
      "background:#fff7ed", "color:#7c2d12", "font:14px/1.5 Arial,sans-serif",
      "box-shadow:0 4px 18px #0002"
    ].join(";");

    const message = root.document.createElement("span");
    message.textContent = `Không đọc được ${labels[key] || "dữ liệu đã lưu"}. Dữ liệu gốc vẫn được giữ nguyên, ứng dụng không tự ghi đè bằng dữ liệu mẫu. Hãy tải lại trang; nếu lỗi còn, liên hệ quản trị viên.`;

    const close = root.document.createElement("button");
    close.type = "button";
    close.textContent = "Đóng";
    close.setAttribute("aria-label", "Đóng thông báo lỗi dữ liệu");
    close.style.cssText = "margin-left:12px;padding:5px 10px;border:0;border-radius:6px;background:#7c2d12;color:#fff;cursor:pointer";
    close.addEventListener("click", () => notice.remove());

    notice.append(message, close);
    root.document.body.append(notice);
  }

  function readJson(key, options = {}) {
    const {
      fallback = null,
      validate = () => true,
      onError,
      throwOnError = false
    } = options;

    function fail(code, cause) {
      const error = new Error(`Không đọc được dữ liệu đã lưu (${code}).`);
      error.name = "SafeStorageError";
      error.code = code;
      error.key = key;
      if (cause) error.cause = cause;
      if (typeof onError === "function") onError(error);
      showStorageNotice(key);
      if (throwOnError) throw error;
      return fallback;
    }

    let raw;
    try {
      raw = root.localStorage.getItem(key);
    } catch (error) {
      return fail("storage-unavailable", error);
    }
    if (raw === null) return fallback;

    let value;
    try {
      value = JSON.parse(raw);
    } catch (error) {
      return fail("invalid-json", error);
    }

    let isValid = false;
    try {
      isValid = validate(value) === true;
    } catch (error) {
      return fail("invalid-shape", error);
    }
    if (!isValid) return fail("invalid-shape");
    return value;
  }

  root.FlowerSafeStorage = { readJson };
})(typeof globalThis !== "undefined" ? globalThis : this);

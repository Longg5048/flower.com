const searchForm = document.getElementById("tracking-search");
const orderIdInput = document.getElementById("tracking-order-id");
const phoneInput = document.getElementById("tracking-phone");
const message = document.getElementById("tracking-message");
const results = document.getElementById("tracking-results");
const reviewPanel = document.getElementById("review-panel");
const reviewForm = document.getElementById("order-review-form");
const reviewComment = document.getElementById("review-comment");
const reviewFeedback = document.getElementById("review-feedback");
const ratingStars = [...document.querySelectorAll(".rating-star")];
let selectedRating = 0;
let currentOrder = null;

function makeElement(tagName, className, text) {
    const element = document.createElement(tagName);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
}

function formatDate(value, includeTime = true) {
    if (!value) return "Chưa cập nhật";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "Chưa cập nhật";
    const options = includeTime
        ? { dateStyle: "medium", timeStyle: "short" }
        : { dateStyle: "medium" };
    return new Intl.DateTimeFormat("vi-VN", options).format(date);
}

function maskPhone(phone) {
    const digits = String(phone || "").replace(/\D/g, "");
    if (digits.length < 5) return phone || "";
    return `${digits.slice(0, 3)}•••${digits.slice(-3)}`;
}

function getOrderHistory(order) {
    const history = Array.isArray(order.trackingHistory) ? [...order.trackingHistory] : [];
    if (!history.length) {
        history.push({
            status: order.status || "Đặt hàng",
            location: order.currentLocation || "Đơn hàng đã được tạo",
            estimatedDelivery: order.estimatedDelivery || "",
            updatedAt: order.createdAt || ""
        });
    }
    return history.sort((first, second) => new Date(first.updatedAt || 0) - new Date(second.updatedAt || 0));
}

function renderHistory(order) {
    const timeline = document.getElementById("tracking-timeline");
    const history = getOrderHistory(order);
    timeline.replaceChildren();

    history.forEach((event, index) => {
        const item = makeElement("li", `timeline-event${index === history.length - 1 ? " is-current" : ""}`);
        const dot = makeElement("span", "timeline-dot");
        dot.setAttribute("aria-hidden", "true");
        const copy = makeElement("div", "timeline-copy");
        copy.append(makeElement("strong", "", event.status || "Cập nhật đơn hàng"));
        if (event.location) copy.append(makeElement("p", "", event.location));
        if (event.estimatedDelivery) {
            copy.append(makeElement("p", "", `Dự kiến giao: ${formatDate(event.estimatedDelivery, false)}`));
        }
        const time = makeElement("time", "", formatDate(event.updatedAt));
        if (event.updatedAt) time.dateTime = event.updatedAt;
        copy.append(time);
        item.append(dot, copy);
        timeline.append(item);
    });
}

function renderProducts(items) {
    const container = document.getElementById("tracking-products");
    container.replaceChildren();

    (Array.isArray(items) ? items : []).forEach((product) => {
        const row = makeElement("div", "tracking-product");
        const image = document.createElement("img");
        image.src = product.image || "";
        image.alt = product.title || "Sản phẩm trong đơn hàng";
        image.addEventListener("error", () => image.remove(), { once: true });
        row.append(
            image,
            makeElement("span", "tracking-product-name", product.title || "Sản phẩm"),
            makeElement("span", "tracking-product-quantity", `× ${Math.max(1, Number(product.quantity) || 1)}`)
        );
        container.append(row);
    });
}

function getStoredReviews() {
    try {
        const reviews = JSON.parse(localStorage.getItem("flowerReviews") || "[]");
        return Array.isArray(reviews) ? reviews : [];
    } catch (error) {
        console.error("Không thể đọc đánh giá:", error);
        return [];
    }
}

function setSelectedRating(rating) {
    selectedRating = rating;
    ratingStars.forEach((star) => {
        const value = Number(star.dataset.rating);
        star.classList.toggle("is-selected", value <= selectedRating);
        star.setAttribute("aria-pressed", String(value === selectedRating));
    });
}

function renderReviewPanel(order) {
    const canReview = order.status === "Hoàn thành chuyến";
    reviewPanel.hidden = !canReview;
    if (!canReview) return;

    setSelectedRating(0);
    reviewForm.reset();
    reviewForm.hidden = false;
    reviewFeedback.textContent = "";

    const existingReview = getStoredReviews().find((review) => String(review.orderId) === String(order.id));
    if (existingReview) {
        reviewForm.hidden = true;
        const stars = "★".repeat(Number(existingReview.rating) || 0);
        reviewFeedback.textContent = `Bạn đã đánh giá đơn hàng này: ${stars}. ${existingReview.comment || ""}`;
    }
}

function renderOrder(order) {
    currentOrder = order;
    document.getElementById("result-order-id").textContent = `#${order.id}`;
    document.getElementById("result-status").textContent = order.status || "Đang xử lý";
    document.getElementById("result-location").textContent = order.currentLocation || "Cửa hàng chưa cập nhật vị trí vận chuyển.";
    document.getElementById("result-eta").textContent = formatDate(order.estimatedDelivery, false);
    document.getElementById("result-customer").textContent = order.customerName || "Chưa cập nhật";
    document.getElementById("result-phone").textContent = maskPhone(order.phone);
    document.getElementById("result-address").textContent = order.address || "Chưa cập nhật";
    renderHistory(order);
    renderProducts(order.items);
    renderReviewPanel(order);
    results.hidden = false;
    message.textContent = "";
}

function searchOrder() {
    const orderId = orderIdInput.value.trim().replace(/^#/, "");
    const phone = phoneInput.value.replace(/\D/g, "");
    let orders = [];

    try {
        orders = JSON.parse(localStorage.getItem("flowerOrders") || "[]");
    } catch (error) {
        console.error("Không thể đọc danh sách đơn hàng:", error);
    }

    const order = orders.find((item) => String(item.id) === orderId
        && String(item.phone || "").replace(/\D/g, "") === phone);

    if (!order) {
        results.hidden = true;
        message.textContent = "Không tìm thấy đơn hàng. Hãy kiểm tra lại mã đơn và số điện thoại đặt hàng.";
        return;
    }

    renderOrder(order);
}

ratingStars.forEach((star) => {
    star.addEventListener("click", () => setSelectedRating(Number(star.dataset.rating)));
});

reviewForm.addEventListener("submit", (event) => {
    event.preventDefault();
    const comment = reviewComment.value.trim();

    if (!currentOrder || currentOrder.status !== "Hoàn thành chuyến") {
        reviewFeedback.textContent = "Chỉ có thể đánh giá đơn hàng đã hoàn thành chuyến.";
        reviewPanel.hidden = true;
        return;
    }
    if (!selectedRating) {
        reviewFeedback.textContent = "Vui lòng chọn số sao đánh giá.";
        return;
    }
    if (!comment) {
        reviewComment.focus();
        reviewFeedback.textContent = "Vui lòng nhập nhận xét trước khi gửi.";
        return;
    }

    const reviews = getStoredReviews();
    if (reviews.some((review) => String(review.orderId) === String(currentOrder.id))) {
        renderReviewPanel(currentOrder);
        return;
    }

    const review = {
        id: Date.now(),
        orderId: currentOrder.id,
        customerName: currentOrder.customerName || "Khách hàng",
        phone: currentOrder.phone || "",
        productName: (currentOrder.items || []).map((item) => item.title).filter(Boolean).join(", ") || `Đơn hàng #${currentOrder.id}`,
        rating: selectedRating,
        comment,
        createdAt: new Date().toISOString(),
        reply: ""
    };

    reviews.push(review);
    localStorage.setItem("flowerReviews", JSON.stringify(reviews));
    renderReviewPanel(currentOrder);
    reviewFeedback.textContent = "Cảm ơn bạn đã gửi đánh giá.";
});

searchForm.addEventListener("submit", (event) => {
    event.preventDefault();
    searchOrder();
});

const orderFromUrl = new URLSearchParams(window.location.search).get("id");
if (orderFromUrl) orderIdInput.value = orderFromUrl;

window.addEventListener("storage", (event) => {
    if (event.key === "flowerOrders" && orderIdInput.value && phoneInput.value) searchOrder();
});

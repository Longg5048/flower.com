import { firebaseConfig } from "./firebase-config.js";

let productPage = document.getElementById("product_page");
let ordersPage = document.getElementById("orders_page");
let reviewsPage = document.getElementById("reviews_page");
let messagesPage = document.getElementById("messages_page");
let revenuePage = document.getElementById("revenue_page");
let usersPage = document.getElementById("users_page");
let logOutPage = document.getElementById("logout_page");
let main = document.querySelector("main");
let currentUser;
let editingProductId = null;
let productSearchTerm = "";

const STORAGE_KEYS = {
   orders: "flowerOrders",
   reviews: "flowerReviews",
   messages: "flowerMessages"
};

const ORDER_STATUSES = [
   "Đặt hàng",
   "Đã nhận đơn",
   "Đã vận chuyển",
   "Hoàn thành chuyến",
   "Giao hàng thất bại",
   "Từ chối đơn hàng"
];

function moneyFormat(value) {
   return new Intl.NumberFormat("vi-VN", {
       style: "currency",
       currency: "VND"
   }).format(Number(value || 0));
}

function getDemoOrders() {
   const now = Date.now();
   return [
       {
          id: 1001,
          customerName: "Nguyễn Thị Lan",
          phone: "0908123456",
          address: "147 Lê Lợi, Quận 1, TP.HCM",
          email: "lan.nguyen@gmail.com",
          createdAt: new Date(now - 86400000).toISOString(),
          status: "Đã vận chuyển",
          total: 739900 + 649900,
          items: [
              { id: 1, title: "Vibrant Spring Basket", quantity: 1, price: 739900 },
              { id: 2, title: "Floral Jewels Arrangement", quantity: 1, price: 649900 }
          ]
       },
       {
          id: 1002,
          customerName: "Phạm Minh Quân",
          phone: "0912345678",
          address: "88 Nguyễn Huệ, Quận 3, TP.HCM",
          email: "quan.pham@gmail.com",
          createdAt: new Date(now - 172800000).toISOString(),
          status: "Đặt hàng",
          total: 599900,
          items: [
              { id: 3, title: "Lovely Lavender Bouquet", quantity: 1, price: 599900 }
          ]
       },
       {
          id: 1003,
          customerName: "Trần Bảo Anh",
          phone: "0987654321",
          address: "55 Hùng Vương, Hà Nội",
          email: "baoanh.tran@gmail.com",
          createdAt: new Date(now - 259200000).toISOString(),
          status: "Hoàn thành chuyến",
          total: 699900,
          items: [
              { id: 4, title: "Rising Star", quantity: 1, price: 699900 }
          ]
       }
   ];
}

function getDemoReviews() {
   const now = Date.now();
   return [
       {
          id: 2001,
          customerName: "Nguyễn Thị Lan",
          phone: "0908123456",
          productName: "Vibrant Spring Basket",
          rating: 5,
          comment: "Hoa rất đẹp, gói hàng cẩn thận và đúng hẹn. Tôi rất hài lòng.",
          createdAt: new Date(now - 86400000).toISOString(),
          reply: "Cảm ơn chị Lan đã tin tưởng. Chúng tôi rất vui được phục vụ!"
       },
       {
          id: 2002,
          customerName: "Trần Bảo Anh",
          phone: "0987654321",
          productName: "Rising Star",
          rating: 4,
          comment: "Mẫu hoa đẹp, màu sắc hài hòa, nhưng tôi muốn thêm một chút hương thơm hơn.",
          createdAt: new Date(now - 259200000).toISOString(),
          reply: "Cảm ơn phản hồi của anh/chị. Chúng tôi sẽ ghi nhận và cải thiện trong lần sau."
       }
   ];
}

function getDemoMessages() {
   const now = Date.now();
   return [
       {
          id: 3001,
          customerName: "Phạm Minh Quân",
          phone: "0912345678",
          subject: "Tư vấn sáng tạo hoa",
          message: "Tôi muốn đặt hoa cho sinh nhật bạn gái theo phong cách tối giản, màu hồng và trắng, gói trang trọng.",
          createdAt: new Date(now - 172800000).toISOString(),
          reply: "Chúng tôi sẽ gợi ý mẫu hoa hồng trắng kết hợp xanh lá tươi, phù hợp phong cách tối giản."
       },
       {
          id: 3002,
          customerName: "Lê Hoài Nam",
          phone: "0909988777",
          subject: "Tư vấn thiết kế hoa",
          message: "Có thể thiết kế giỏ hoa theo chủ đề tiệc cưới có thêm nơ và dây ruy băng không?",
          createdAt: new Date(now - 432000000).toISOString(),
          reply: "Có thể, chúng tôi có gói tùy chỉnh theo chủ đề cưới với nơ và dây ruy băng theo ý khách hàng."
       }
   ];
}

async function ensureStorage() {
   await FlowerInventory.initialize();

   const existingOrders = JSON.parse(localStorage.getItem(STORAGE_KEYS.orders) || "[]");
   const existingReviews = JSON.parse(localStorage.getItem(STORAGE_KEYS.reviews) || "[]");
   const existingMessages = JSON.parse(localStorage.getItem(STORAGE_KEYS.messages) || "[]");

   if (!localStorage.getItem(STORAGE_KEYS.orders) || !existingOrders.length) {
       localStorage.setItem(STORAGE_KEYS.orders, JSON.stringify(getDemoOrders()));
   }
   if (!localStorage.getItem(STORAGE_KEYS.reviews) || !existingReviews.length) {
       localStorage.setItem(STORAGE_KEYS.reviews, JSON.stringify(getDemoReviews()));
   }
   if (!localStorage.getItem(STORAGE_KEYS.messages) || !existingMessages.length) {
       localStorage.setItem(STORAGE_KEYS.messages, JSON.stringify(getDemoMessages()));
   }
}

function getProducts() {
    return FlowerInventory.readProducts();
}

function getOrders() {
   return JSON.parse(localStorage.getItem(STORAGE_KEYS.orders) || "[]");
}

function getReviews() {
   return JSON.parse(localStorage.getItem(STORAGE_KEYS.reviews) || "[]");
}

function getMessages() {
   return JSON.parse(localStorage.getItem(STORAGE_KEYS.messages) || "[]");
}

function renderDashboard() {
   const products = getProducts();
   const orders = getOrders();
   const reviews = getReviews();
   const messages = getMessages();
   const totalStock = products.reduce((sum, item) => sum + Number(item.quantity || 0), 0);
   const totalRevenue = orders.reduce((sum, order) => sum + Number(order.total || 0), 0);

   main.innerHTML = `
       <section class="dashboard">
           <div class="summary">
               <div class="summary-card">
                   <h3>Tổng sản phẩm</h3>
                   <div class="value">${products.length}</div>
               </div>
               <div class="summary-card">
                   <h3>Tổng số lượng hoa</h3>
                   <div class="value">${totalStock}</div>
               </div>
               <div class="summary-card">
                   <h3>Đánh giá</h3>
                   <div class="value">${reviews.length}</div>
               </div>
               <div class="summary-card">
                   <h3>Đơn hàng</h3>
                   <div class="value">${orders.length}</div>
               </div>
               <div class="summary-card">
                   <h3>Phản hồi khách</h3>
                   <div class="value">${messages.length}</div>
               </div>
               <div class="summary-card">
                   <h3>Doanh thu</h3>
                   <div class="value">${moneyFormat(totalRevenue)}</div>
               </div>
           </div>
       </section>
   `;
}

function renderRevenueTab() {
   const products = getProducts();
   const orders = getOrders();
   const totalCurrentStock = products.reduce((sum, item) => sum + Number(item.quantity || 0), 0);
   const totalSoldUnits = orders.reduce((sum, order) => {
       const sold = (order.items || []).reduce((orderSum, item) => orderSum + Number(item.quantity || 0), 0);
       return sum + sold;
   }, 0);
   const totalRevenue = orders.reduce((sum, order) => sum + Number(order.total || 0), 0);
   const avgRevenuePerOrder = orders.length ? totalRevenue / orders.length : 0;
   const hoaDaThem = totalCurrentStock + totalSoldUnits;
   const stockHealth = Math.round((totalCurrentStock / Math.max(hoaDaThem, 1)) * 100);

   main.innerHTML = `
       <section class="panel">
           <h2>Doanh thu & kho hàng</h2>
           <div class="summary revenue-summary">
               <div class="summary-card revenue-card">
                   <h3>Số hoa đã thêm</h3>
                   <div class="value">${hoaDaThem}</div>
                   <small>Tổng lượng hoa nhập/đưa vào kho</small>
               </div>
               <div class="summary-card revenue-card">
                   <h3>Số hoa đã bán</h3>
                   <div class="value">${totalSoldUnits}</div>
                   <small>Tổng số lượng đã giao cho khách</small>
               </div>
               <div class="summary-card revenue-card">
                   <h3>Tổng doanh thu</h3>
                   <div class="value">${moneyFormat(totalRevenue)}</div>
                   <small>Từ tất cả đơn hàng</small>
               </div>
               <div class="summary-card revenue-card">
                   <h3>Doanh thu / đơn</h3>
                   <div class="value">${moneyFormat(avgRevenuePerOrder)}</div>
                   <small>Trung bình mỗi đơn hàng</small>
               </div>
               <div class="summary-card revenue-card">
                   <h3>Hoa hiện có</h3>
                   <div class="value">${totalCurrentStock}</div>
                   <small>Tồn kho hiện tại</small>
               </div>
               <div class="summary-card revenue-card">
                   <h3>Chỉ số kho</h3>
                   <div class="value">${stockHealth}%</div>
                   <small>Mức độ hàng tồn kho ổn định</small>
               </div>
           </div>
       </section>
   `;
}

function renderProductTab() {
   const products = getProducts();
   const searchTerm = productSearchTerm.trim().toLocaleLowerCase("vi");
   const filteredProducts = products.filter((item) =>
       `${item.title} ${item.description || ""}`.toLocaleLowerCase("vi").includes(searchTerm)
   );
   const editingProduct = products.find((item) => String(item.id) === String(editingProductId));
   main.innerHTML = `
       <section class="panel-wrap">
           <div class="panel">
               <h2>Quản lý kho hoa</h2>
               <form id="product-search-form" class="product-search" role="search">
                   <label class="visually-hidden" for="product-search">Tìm sản phẩm</label>
                   <input id="product-search" name="search" type="search" placeholder="Tìm theo tên hoặc mô tả..." value="${escapeHtml(productSearchTerm)}">
                   <button type="submit" class="secondary-btn">Tìm kiếm</button>
                   ${productSearchTerm ? '<button type="button" class="clear-search" data-action="clear-search">Xóa tìm kiếm</button>' : ""}
               </form>
               <div class="product-table-wrap">
               <table class="product-table">
                   <thead>
                       <tr>
                           <th>Ảnh</th>
                           <th>Tên sản phẩm</th>
                           <th>Giá</th>
                           <th>Số lượng</th>
                           <th>Thao tác</th>
                       </tr>
                   </thead>
                   <tbody>
                       ${filteredProducts.map((item) => `
                           <tr>
                               <td><img src="${escapeHtml(item.image)}" alt="${escapeHtml(item.title)}"></td>
                               <td>
                                   <div><strong>${escapeHtml(item.title)}</strong></div>
                                   <small>${escapeHtml(item.description || "")}</small>
                               </td>
                               <td>${formatVnd(item.price)}</td>
                               <td><input class="qty-input" data-id="${escapeHtml(item.id)}" type="number" min="0" value="${Number(item.quantity) || 0}"></td>
                               <td>
                                   <div class="product-row-actions">
                                       <button class="save-qty" data-id="${escapeHtml(item.id)}" type="button">Lưu tồn kho</button>
                                       <button class="secondary-btn edit-product" data-id="${escapeHtml(item.id)}" type="button">Sửa</button>
                                       <button class="delete-product" data-id="${escapeHtml(item.id)}" type="button">Xóa</button>
                                   </div>
                               </td>
                           </tr>
                       `).join("") || `<tr><td colspan="5" class="empty-state">${products.length ? "Không tìm thấy sản phẩm phù hợp." : "Chưa có sản phẩm nào."}</td></tr>`}
                   </tbody>
               </table>
               </div>
           </div>
           <div class="panel">
               <h2>${editingProduct ? "Chỉnh sửa sản phẩm" : "Thêm hoa mới"}</h2>
               <form id="add-product-form" class="product-form">
                   <input type="text" id="product-title" placeholder="Tên hoa" value="${escapeHtml(editingProduct?.title || "")}" required maxlength="120">
                   <input type="url" id="product-image" placeholder="Link hình ảnh" value="${escapeHtml(editingProduct?.image || "")}" required>
                   <input type="number" id="product-price" placeholder="Giá (VND)" value="${editingProduct ? toVndAmount(editingProduct.price) : ""}" min="1" step="1" required>
                   <input type="number" id="product-quantity" placeholder="Số lượng" value="${editingProduct ? Number(editingProduct.quantity) || 0 : ""}" min="0" step="1" required>
                   <textarea id="product-description" placeholder="Mô tả sản phẩm" required maxlength="1000">${escapeHtml(editingProduct?.description || "")}</textarea>
                   <div class="product-form-actions">
                       <button type="submit" class="submit-btn">${editingProduct ? "Lưu thay đổi" : "Thêm sản phẩm"}</button>
                       ${editingProduct ? '<button type="button" class="secondary-btn" data-action="cancel-edit">Hủy</button>' : ""}
                   </div>
               </form>
           </div>
       </section>
   `;
}

function getProductById(productId) {
   const products = getProducts();
   return products.find((product) => String(product.id) === String(productId));
}

function renderCompletionAction(type, record) {
   if (record.completedAt) {
       return `<button class="restore-record" data-record-type="${type}" data-record-id="${record.id}" type="button">Khôi phục</button>`;
   }
   return `<button class="complete-record" data-record-type="${type}" data-record-id="${record.id}" type="button">Hoàn thành</button>`;
}

function renderCompletedSection(records, renderCard, label) {
   return `
       <section class="completed-section">
           <h3>Hoàn thành (${records.length})</h3>
           <div class="list-stack">
               ${records.length ? records.map(renderCard).join("") : `<div class="empty-state">Chưa có ${label} nào hoàn thành.</div>`}
           </div>
       </section>
   `;
}

function renderCompletedOrderSummary(order) {
   return renderOrderSummary(order);
}

function renderActiveOrderSummary(order) {
   return renderOrderSummary(order);
}

function renderOrderSummary(order) {
   const itemSummary = (order.items || [])
       .map((item) => `${item.title} × ${item.quantity}`)
       .join(", ") || "Không có sản phẩm";
   return `
       <article class="record-summary">
           <div class="completed-order-info">
               <strong>Đơn #${order.id} · ${order.customerName}</strong>
               <span><b>Sản phẩm:</b> ${itemSummary}</span>
               <span><b>Thời gian đặt:</b> ${new Date(order.createdAt).toLocaleString("vi-VN")}</span>
           </div>
           <div class="record-summary-actions">
               <button class="secondary-btn show-record-details" data-record-type="orders" data-record-id="${order.id}" type="button" aria-expanded="false">Xem chi tiết</button>
           </div>
           <div class="record-details" data-record-details="orders:${order.id}" hidden>
               ${renderOrderCard(order)}
           </div>
       </article>
   `;
}

function renderOrderCard(order, showCompletionAction = true) {
   const items = order.items || [];
   return `
       <div class="order-item content-card">
           <div class="card-header">
               <div>
                   <div class="title-row">
                       <span class="card-icon">🧾</span>
                       <strong>Đơn #${order.id} - ${order.customerName}</strong>
                   </div>
                   <div class="order-meta">
                       <span>📞 ${order.phone}</span>
                       <span>🕒 ${new Date(order.createdAt).toLocaleString("vi-VN")}</span>
                       <span>💰 ${moneyFormat(order.total)}</span>
                   </div>
               </div>
               <span class="status-badge">${order.status}</span>
           </div>

           <div class="info-grid two-col">
               <div class="field">
                   <span class="field-label">Khách hàng</span>
                   <span class="field-value">${order.customerName}</span>
               </div>
               <div class="field">
                   <span class="field-label">Điện thoại</span>
                   <span class="field-value">${order.phone}</span>
               </div>
               <div class="field full-width">
                   <span class="field-label">Địa chỉ</span>
                   <span class="field-value">${order.address || "Chưa cập nhật"}</span>
               </div>
           </div>

           <div class="section-label">Sản phẩm trong đơn</div>
           <div class="order-product-list">
               ${items.map((item) => {
                   const product = getProductById(item.id) || {};
                   const image = item.image || product.image || "https://images.contentstack.io/v3/assets/bltdd99f24e8a94d536/bltce6d6c480577e10e/5d4866eff9ece57fa9a82245/flowers.png?quality=60&auto=webp&optimize={medium}";
                   return `
                       <div class="product-card">
                           <img src="${image}" alt="${item.title}">
                           <div class="product-card-content">
                               <strong>${item.title}</strong>
                               <span>Số lượng: ${item.quantity}</span>
                               <span>Giá: ${moneyFormat(item.price)}</span>
                           </div>
                       </div>
                   `;
               }).join("")}
           </div>

           ${!order.completedAt ? `
               <div class="reply-box compact-box">
                   <label class="field-label">Lịch trình vận chuyển</label>
                   <div class="tracking-admin-fields">
                       <label>
                           Vị trí hiện tại
                           <input class="tracking-location" data-order-id="${order.id}" value="${escapeHtml(order.currentLocation || "")}" placeholder="Ví dụ: Đang giao tại Quận 1">
                       </label>
                       <label>
                           Dự kiến giao đến
                           <input class="tracking-eta" data-order-id="${order.id}" type="datetime-local" value="${escapeHtml(toDateTimeLocal(order.estimatedDelivery))}">
                       </label>
                   </div>
               </div>
           ` : ""}

           ${order.completedAt ? `
               <div class="completion-meta">Hoàn thành lúc ${new Date(order.completedAt).toLocaleString("vi-VN")}</div>
               ${showCompletionAction ? `<div class="status-row">${renderCompletionAction("orders", order)}</div>` : ""}
           ` : `
               <div class="reply-box compact-box">
                   <label class="field-label">Cập nhật trạng thái</label>
                   <div class="status-row">
                       <select class="status-select" data-order-id="${order.id}">
                           ${ORDER_STATUSES.map((status) => `
                               <option value="${status}" ${status === order.status ? "selected" : ""}>${status}</option>
                           `).join("")}
                       </select>
                       <button class="status-save" data-order-id="${order.id}">Cập nhật trạng thái</button>
                       ${showCompletionAction ? renderCompletionAction("orders", order) : ""}
                   </div>
               </div>
           `}
       </div>
   `;
}

function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, (character) => ({
         "&": "&amp;",
         "<": "&lt;",
         ">": "&gt;",
         '"': "&quot;",
         "'": "&#39;"
    })[character]);
}

function toDateTimeLocal(value) {
    if (!value) return "";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "";
    return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
}

function renderOrdersTab() {
   const statusPriority = {
       "Đặt hàng": 0,
       "Đã nhận đơn": 1,
       "Đã vận chuyển": 2,
       "Hoàn thành chuyến": 3,
       "Giao hàng thất bại": 4,
       "Từ chối đơn hàng": 5
   };
   const sortedOrders = getOrders().sort((first, second) => {
       const priorityDifference = (statusPriority[first.status] ?? 1) - (statusPriority[second.status] ?? 1);
       if (priorityDifference !== 0) return priorityDifference;
       return new Date(second.createdAt).getTime() - new Date(first.createdAt).getTime();
   });
   const orders = sortedOrders.filter((order) => !order.completedAt);
   const completedOrders = sortedOrders.filter((order) => order.completedAt);
   main.innerHTML = `
       <section class="panel">
           <h2>Quản lý đơn hàng</h2>
           <div class="list-stack">
               ${orders.length ? orders.map(renderActiveOrderSummary).join("") : '<div class="empty-state">Không có đơn hàng nào cần xử lý.</div>'}
           </div>
           ${renderCompletedSection(completedOrders, renderCompletedOrderSummary, "đơn hàng")}
       </section>
   `;
}

function renderReviewCard(review, showCompletionAction = true) {
   return `
       <div class="review-item feedback-card content-card">
           <div class="card-header">
               <div>
                   <div class="title-row">
                       <span class="card-icon">⭐</span>
                       <strong>${review.customerName}</strong>
                   </div>
                   <div class="review-meta">
                       <span>📞 ${review.phone}</span>
                       <span>🕒 ${new Date(review.createdAt).toLocaleString("vi-VN")}</span>
                   </div>
               </div>
               <div class="card-actions">
                   <span class="card-tag card-tag-review">Đánh giá</span>
                   ${showCompletionAction ? renderCompletionAction("reviews", review) : ""}
               </div>
           </div>

           <div class="info-grid two-col">
               <div class="field">
                   <span class="field-label">Sản phẩm</span>
                   <span class="field-value">${review.productName}</span>
               </div>
               <div class="field">
                   <span class="field-label">Xếp hạng</span>
                   <span class="field-value rating-value">${"★".repeat(Number(review.rating || 0))}${"☆".repeat(5 - Number(review.rating || 0))}</span>
               </div>
           </div>

           <div class="section-label">Nội dung đánh giá</div>
           <div class="detail-block quote-box">${review.comment || "Không có nhận xét"}</div>

           <div class="reply-box">
               <label class="field-label">Phản hồi của bạn</label>
               <textarea class="reply-input" data-review-id="${review.id}" placeholder="Nhập phản hồi cho khách hàng...">${review.reply || ""}</textarea>
               <button class="send-reply" data-review-id="${review.id}">Gửi phản hồi</button>
           </div>
       </div>
   `;
}

function renderCompletedReviewSummary(review) {
   return renderReviewSummary(review);
}

function renderActiveReviewSummary(review) {
   return renderReviewSummary(review);
}

function renderReviewSummary(review) {
   return `
       <article class="record-summary">
           <div class="completed-order-info">
               <strong>${review.customerName} · ${review.productName}</strong>
               <span class="rating-value">${"★".repeat(Number(review.rating || 0))}${"☆".repeat(5 - Number(review.rating || 0))}</span>
               <span class="record-summary-preview"><b>Đánh giá:</b> ${review.comment || "Không có nhận xét"}</span>
               <span><b>Thời gian:</b> ${new Date(review.createdAt).toLocaleString("vi-VN")}</span>
           </div>
           <div class="record-summary-actions">
               <button class="secondary-btn show-record-details" data-record-type="reviews" data-record-id="${review.id}" type="button" aria-expanded="false">Xem chi tiết</button>
           </div>
           <div class="record-details" data-record-details="reviews:${review.id}" hidden>
               ${renderReviewCard(review)}
           </div>
       </article>
   `;
}

function renderReviewsTab() {
   const reviews = getReviews();
   const activeReviews = reviews.filter((review) => !review.completedAt);
   const completedReviews = reviews.filter((review) => review.completedAt);
   main.innerHTML = `
       <section class="panel">
           <h2>Đánh giá khách hàng</h2>
           <div class="list-stack">
               ${activeReviews.length ? activeReviews.map(renderActiveReviewSummary).join("") : '<div class="empty-state">Không có đánh giá nào cần xử lý.</div>'}
           </div>
           ${renderCompletedSection(completedReviews, renderCompletedReviewSummary, "đánh giá")}
       </section>
   `;
}

function renderMessageCard(message, showCompletionAction = true) {
   return `
       <div class="message-item feedback-card content-card">
           <div class="card-header">
               <div>
                   <div class="title-row">
                       <span class="card-icon">💬</span>
                       <strong>${message.customerName}</strong>
                   </div>
                   <div class="message-meta">
                       <span>📞 ${message.phone}</span>
                       <span>🕒 ${new Date(message.createdAt).toLocaleString("vi-VN")}</span>
                   </div>
               </div>
               <div class="card-actions">
                   <span class="card-tag card-tag-message">Phản hồi</span>
                   ${showCompletionAction ? renderCompletionAction("messages", message) : ""}
               </div>
           </div>

           <div class="info-grid single-col">
               <div class="field">
                   <span class="field-label">Chủ đề</span>
                   <span class="field-value">${message.subject || "Tư vấn đặt hàng"}</span>
               </div>
           </div>

           <div class="section-label">Nội dung khách nhắn</div>
           <div class="detail-block quote-box">${message.message}</div>
           ${message.reply ? `<div class="reply-banner"><strong>Phản hồi của bạn:</strong> ${message.reply}</div>` : ""}

           <div class="reply-box">
               <label class="field-label">Trả lời khách</label>
               <textarea class="reply-input" data-message-id="${message.id}" placeholder="Nhập câu trả lời cho khách hàng...">${message.reply || ""}</textarea>
               <button class="send-reply" data-message-id="${message.id}">Trả lời</button>
           </div>
       </div>
   `;
}

function renderCompletedMessageSummary(message) {
   return renderMessageSummary(message);
}

function renderActiveMessageSummary(message) {
   return renderMessageSummary(message);
}

function renderMessageSummary(message) {
   return `
       <article class="record-summary">
           <div class="completed-order-info">
               <strong>${message.customerName} · ${message.subject || "Tư vấn đặt hàng"}</strong>
               <span class="record-summary-preview"><b>Nội dung:</b> ${message.message}</span>
               <span><b>Thời gian:</b> ${new Date(message.createdAt).toLocaleString("vi-VN")}</span>
           </div>
           <div class="record-summary-actions">
               <button class="secondary-btn show-record-details" data-record-type="messages" data-record-id="${message.id}" type="button" aria-expanded="false">Xem chi tiết</button>
           </div>
           <div class="record-details" data-record-details="messages:${message.id}" hidden>
               ${renderMessageCard(message)}
           </div>
       </article>
   `;
}

function renderMessagesTab() {
   const messages = getMessages();
   const activeMessages = messages.filter((message) => !message.completedAt);
   const completedMessages = messages.filter((message) => message.completedAt);
   main.innerHTML = `
       <section class="panel">
           <h2>Phản hồi từ khách hàng</h2>
           <div class="list-stack">
               ${activeMessages.length ? activeMessages.map(renderActiveMessageSummary).join("") : '<div class="empty-state">Không có phản hồi nào cần xử lý.</div>'}
           </div>
           ${renderCompletedSection(completedMessages, renderCompletedMessageSummary, "phản hồi")}
       </section>
   `;
}

function saveProductQuantity(id, quantity) {
   const products = getProducts();
   const nextProducts = products.map((item) => {
       if (String(item.id) === String(id)) {
           return { ...item, quantity: Number(quantity || 0) };
       }
       return item;
   });
    FlowerInventory.saveProducts(nextProducts);
   renderProductTab();
}

function addNewProduct(event) {
   event.preventDefault();
   const title = document.getElementById("product-title").value.trim();
   const image = document.getElementById("product-image").value.trim();
    const priceInVnd = Number(document.getElementById("product-price").value);
   const quantity = Number(document.getElementById("product-quantity").value);
   const description = document.getElementById("product-description").value.trim();

    if (!title || !image || !description || !Number.isFinite(priceInVnd) || priceInVnd < 1 || !Number.isFinite(quantity) || quantity < 0) {
       alert("Vui lòng nhập đầy đủ thông tin.");
       return;
   }

   const products = getProducts();
    const productData = { title, image, price: usdFromVnd(priceInVnd), quantity, description };
   const isEditing = editingProductId !== null;
   const nextProducts = isEditing
       ? products.map((item) => String(item.id) === String(editingProductId) ? { ...item, ...productData } : item)
       : [...products, { id: Date.now(), ...productData }];

    FlowerInventory.saveProducts(nextProducts);
   editingProductId = null;
   alert(isEditing ? "Cập nhật sản phẩm thành công!" : "Thêm sản phẩm thành công!");
   renderProductTab();
}

function deleteProduct(id) {
   const product = getProductById(id);
   if (!product || !window.confirm(`Bạn có chắc muốn xóa “${product.title}”?`)) return;

   const products = getProducts().filter((item) => String(item.id) !== String(id));
    FlowerInventory.saveProducts(products);
   if (String(editingProductId) === String(id)) editingProductId = null;
   renderProductTab();
}

function updateOrderStatus(orderId, status, location, estimatedDelivery) {
   if (status === "Đã vận chuyển" && (!location || !estimatedDelivery)) {
       alert("Vui lòng nhập vị trí hiện tại và thời gian dự kiến giao hàng.");
       return;
   }

   const orders = getOrders();
   const updatedAt = new Date().toISOString();
   const nextOrders = orders.map((order) => {
       if (String(order.id) !== String(orderId)) return order;

       const trackingHistory = Array.isArray(order.trackingHistory) && order.trackingHistory.length
           ? order.trackingHistory
           : [{
               status: order.status,
               location: order.currentLocation || "Đơn hàng đã được tạo",
               estimatedDelivery: order.estimatedDelivery || "",
               updatedAt: order.createdAt || updatedAt
           }];
       const hasChanges = order.status !== status
           || (order.currentLocation || "") !== location
           || (order.estimatedDelivery || "") !== estimatedDelivery;
       if (!hasChanges) return order;

       return {
           ...order,
           status,
           currentLocation: location,
           estimatedDelivery,
           trackingUpdatedAt: updatedAt,
           trackingHistory: [...trackingHistory, { status, location, estimatedDelivery, updatedAt }]
       };
   });
   localStorage.setItem(STORAGE_KEYS.orders, JSON.stringify(nextOrders));
   renderOrdersTab();
}

function updateReviewReply(reviewId, reply) {
   const reviews = getReviews();
   const nextReviews = reviews.map((review) => {
       if (String(review.id) === String(reviewId)) {
           return { ...review, reply };
       }
       return review;
   });
   localStorage.setItem(STORAGE_KEYS.reviews, JSON.stringify(nextReviews));
   renderReviewsTab();
}

function updateMessageReply(messageId, reply) {
   const messages = getMessages();
   const nextMessages = messages.map((message) => {
       if (String(message.id) === String(messageId)) {
           return { ...message, reply };
       }
       return message;
   });
   localStorage.setItem(STORAGE_KEYS.messages, JSON.stringify(nextMessages));
   renderMessagesTab();
}

function deleteAdminRecord(type, id) {
   const records = {
       orders: { key: STORAGE_KEYS.orders, label: "đơn hàng", render: renderOrdersTab },
       reviews: { key: STORAGE_KEYS.reviews, label: "đánh giá", render: renderReviewsTab },
       messages: { key: STORAGE_KEYS.messages, label: "phản hồi", render: renderMessagesTab }
   };
   const record = records[type];
   if (!record) {
       console.error("Không thể xóa mục admin không hợp lệ:", type);
       return;
   }

   if (!window.confirm(`Bạn có chắc muốn xóa ${record.label} này không?`)) return;

   const savedRecords = JSON.parse(localStorage.getItem(record.key) || "[]");
   const remainingRecords = savedRecords.filter((item) => String(item.id) !== String(id));
   if (remainingRecords.length === savedRecords.length) {
       console.error(`Không tìm thấy ${record.label} cần xóa:`, id);
       return;
   }

   localStorage.setItem(record.key, JSON.stringify(remainingRecords));
   record.render();
}

function setRecordCompletion(type, id, completed) {
   const records = {
       orders: { key: STORAGE_KEYS.orders, label: "đơn hàng", render: renderOrdersTab },
       reviews: { key: STORAGE_KEYS.reviews, label: "đánh giá", render: renderReviewsTab },
       messages: { key: STORAGE_KEYS.messages, label: "phản hồi", render: renderMessagesTab }
   };
   const record = records[type];
   if (!record) {
       console.error("Không thể cập nhật mục hoàn thành không hợp lệ:", type);
       return;
   }

   const savedRecords = JSON.parse(localStorage.getItem(record.key) || "[]");
   let found = false;
   const updatedRecords = savedRecords.map((item) => {
       if (String(item.id) !== String(id)) return item;
       found = true;
       if (completed) return { ...item, completedAt: new Date().toISOString() };
       const { completedAt, ...restoredItem } = item;
       return restoredItem;
   });
   if (!found) {
       console.error(`Không tìm thấy ${record.label} cần cập nhật:`, id);
       return;
   }

   localStorage.setItem(record.key, JSON.stringify(updatedRecords));
   record.render();
}

function renderUsersTab() {
   main.innerHTML = `
       <section class="panel">
           <div class="users-heading">
               <div>
                   <h2>Tài khoản Firebase</h2>
                   <p class="users-description">Thông tin tài khoản và phương thức đăng nhập. Danh sách Firebase không trả về mật khẩu.</p>
               </div>
               <button class="primary-btn" id="refresh-users" type="button">Tải lại</button>
           </div>
           <div class="table-wrap">
               <table class="users-table">
                   <thead>
                       <tr>
                           <th>Tên</th>
                           <th>Email</th>
                           <th>Phương thức</th>
                           <th>UID</th>
                           <th>Tạo tài khoản</th>
                           <th>Đăng nhập gần nhất</th>
                           <th>Trạng thái</th>
                       </tr>
                   </thead>
                   <tbody id="firebase-users"></tbody>
               </table>
           </div>
           <p class="users-status" id="users-status" role="status" aria-live="polite"></p>
           <button class="secondary-btn" id="load-more-users" type="button" hidden>Tải thêm</button>
       </section>
   `;
   loadFirebaseUsers();

   document.getElementById("refresh-users").addEventListener("click", () => loadFirebaseUsers());
   document.getElementById("load-more-users").addEventListener("click", (event) => {
       loadFirebaseUsers(event.currentTarget.dataset.pageToken);
   });
}

function formatUserDate(value) {
   if (!value) return "Chưa có";
   const date = new Date(value);
   return Number.isNaN(date.getTime()) ? "Không xác định" : date.toLocaleString("vi-VN");
}

async function loadFirebaseUsers(pageToken) {
   const status = document.getElementById("users-status");
   const tbody = document.getElementById("firebase-users");
   const loadMore = document.getElementById("load-more-users");
   const refresh = document.getElementById("refresh-users");
   if (!status || !tbody || !loadMore || !refresh) return;

   status.textContent = "Đang tải danh sách tài khoản...";
   loadMore.hidden = true;
   refresh.disabled = true;
   try {
       const idToken = await currentUser.getIdToken();
       const httpResponse = await fetch("/api/admin/users", {
           method: "POST",
           headers: {
               "Authorization": `Bearer ${idToken}`,
               "Content-Type": "application/json"
           },
           body: JSON.stringify({
           pageToken: pageToken || null
           })
       });
       const data = await httpResponse.json();
       if (!httpResponse.ok) {
           const error = new Error(data.error || "Không thể tải danh sách tài khoản.");
           error.code = data.code;
           throw error;
       }
       const response = { data };
       if (!pageToken) tbody.replaceChildren();

       response.data.users.forEach((user) => {
           const row = document.createElement("tr");
           const providers = user.providers.length
               ? user.providers.map((provider) => provider === "google.com" ? "Google" :
                   provider === "password" ? "Email / mật khẩu" : provider).join(", ")
               : "Không xác định";
           [
               user.displayName || "Chưa có tên",
               user.email || "Chưa có email",
               providers,
               user.uid,
               formatUserDate(user.creationTime),
               formatUserDate(user.lastSignInTime),
               user.disabled ? "Đã khóa" : "Đang hoạt động"
           ].forEach((value) => {
               const cell = document.createElement("td");
               cell.textContent = value;
               row.appendChild(cell);
           });
           tbody.appendChild(row);
       });

       const nextPageToken = response.data.nextPageToken;
       if (nextPageToken) {
           loadMore.dataset.pageToken = nextPageToken;
           loadMore.hidden = false;
       }
       const totalShown = tbody.rows.length;
       status.textContent = totalShown
           ? `Đang hiển thị ${totalShown} tài khoản${nextPageToken ? " (có thể tải thêm)." : "."}`
           : "Chưa có tài khoản Firebase nào.";
   } catch (error) {
       console.error("Không thể tải danh sách tài khoản Firebase:", error);
       status.textContent = error.code === "permission-denied"
           ? "Tài khoản hiện tại không có quyền xem danh sách."
           : error.code === "admin-api-not-configured"
               ? "API chưa được cấu hình khóa dịch vụ trong Vercel. Xem ADMIN-SETUP.md."
               : "Không tải được danh sách tài khoản. Kiểm tra kết nối rồi thử lại.";
   } finally {
       refresh.disabled = false;
   }
}

function bindAdminEvents(authApi) {
if (productPage) {
   productPage.addEventListener("click", renderProductTab);
}

if (ordersPage) {
   ordersPage.addEventListener("click", renderOrdersTab);
}

if (reviewsPage) {
   reviewsPage.addEventListener("click", renderReviewsTab);
}

if (messagesPage) {
   messagesPage.addEventListener("click", renderMessagesTab);
}

if (revenuePage) {
   revenuePage.addEventListener("click", renderRevenueTab);
}

if (usersPage) {
   usersPage.addEventListener("click", renderUsersTab);
}

if (logOutPage) {
   logOutPage.addEventListener("click", async () => {
       localStorage.removeItem("loggedUser");
       localStorage.removeItem("loggedAdmin");
       await authApi.signOut(authApi.auth);
       window.location.assign("./login.html");
   });
}

main.addEventListener("click", (event) => {
   const detailsButton = event.target.closest(".show-record-details");
   if (detailsButton) {
       const details = [...main.querySelectorAll(".record-details")]
           .find((element) => element.dataset.recordDetails === `${detailsButton.dataset.recordType}:${detailsButton.dataset.recordId}`);
       if (details) {
           details.hidden = !details.hidden;
           detailsButton.setAttribute("aria-expanded", String(!details.hidden));
           detailsButton.textContent = details.hidden ? "Xem chi tiết" : "Ẩn chi tiết";
       }
       return;
   }

   const editProductButton = event.target.closest(".edit-product");
   if (editProductButton) {
       editingProductId = editProductButton.dataset.id;
       renderProductTab();
       document.getElementById("product-title").focus();
       document.getElementById("add-product-form").scrollIntoView({ behavior: "smooth", block: "center" });
       return;
   }

   const deleteProductButton = event.target.closest(".delete-product");
   if (deleteProductButton) {
       deleteProduct(deleteProductButton.dataset.id);
       return;
   }

   const actionButton = event.target.closest("[data-action]");
   if (actionButton?.dataset.action === "clear-search") {
       productSearchTerm = "";
       renderProductTab();
       return;
   }
   if (actionButton?.dataset.action === "cancel-edit") {
       editingProductId = null;
       renderProductTab();
       return;
   }

   const deleteButton = event.target.closest(".delete-record");
   if (deleteButton) {
       deleteAdminRecord(deleteButton.dataset.recordType, deleteButton.dataset.recordId);
       return;
   }

   const completeButton = event.target.closest(".complete-record");
   if (completeButton) {
       setRecordCompletion(completeButton.dataset.recordType, completeButton.dataset.recordId, true);
       return;
   }

   const restoreButton = event.target.closest(".restore-record");
   if (restoreButton) {
       setRecordCompletion(restoreButton.dataset.recordType, restoreButton.dataset.recordId, false);
       return;
   }

   const saveQty = event.target.closest(".save-qty");
   if (saveQty) {
       const id = saveQty.dataset.id;
       const input = document.querySelector(`.qty-input[data-id="${id}"]`);
       if (input) {
           saveProductQuantity(id, input.value);
       }
   }

   const statusSave = event.target.closest(".status-save");
   if (statusSave) {
       const orderId = statusSave.dataset.orderId;
       const select = document.querySelector(`.status-select[data-order-id="${orderId}"]`);
       const location = document.querySelector(`.tracking-location[data-order-id="${orderId}"]`);
       const estimatedDelivery = document.querySelector(`.tracking-eta[data-order-id="${orderId}"]`);
       if (select && location && estimatedDelivery) {
           updateOrderStatus(orderId, select.value, location.value.trim(), estimatedDelivery.value);
       }
   }

   const replyButton = event.target.closest(".send-reply");
   if (replyButton) {
       const reviewId = replyButton.dataset.reviewId;
       const messageId = replyButton.dataset.messageId;

       if (reviewId) {
           const input = document.querySelector(`.reply-input[data-review-id="${reviewId}"]`);
           if (input) updateReviewReply(reviewId, input.value.trim());
       }

       if (messageId) {
           const input = document.querySelector(`.reply-input[data-message-id="${messageId}"]`);
           if (input) updateMessageReply(messageId, input.value.trim());
       }
   }
});

main.addEventListener("submit", (event) => {
   if (event.target && event.target.id === "product-search-form") {
       event.preventDefault();
       productSearchTerm = new FormData(event.target).get("search") || "";
       renderProductTab();
   }

   if (event.target && event.target.id === "add-product-form") {
       addNewProduct(event);
   }
});
}

async function initializeAdmin() {
   try {
       const [appSdk, authSdk] = await Promise.all([
           import("https://www.gstatic.com/firebasejs/11.6.0/firebase-app.js"),
           import("https://www.gstatic.com/firebasejs/11.6.0/firebase-auth.js")
       ]);
       const app = appSdk.getApps().length ? appSdk.getApp() : appSdk.initializeApp(firebaseConfig);
       const auth = authSdk.getAuth(app);
       currentUser = await new Promise((resolve, reject) => {
           let unsubscribe = () => {};
           unsubscribe = authSdk.onAuthStateChanged(auth, (user) => {
               unsubscribe();
               resolve(user);
           }, (error) => {
               unsubscribe();
               reject(error);
           });
       });

       if (!currentUser) {
           window.location.replace("./login.html");
           return;
       }

       const token = await currentUser.getIdTokenResult();
       if (token.claims.admin !== true) {
           await authSdk.signOut(auth);
           localStorage.removeItem("loggedUser");
           localStorage.removeItem("loggedAdmin");
           window.location.replace("./login.html");
           return;
       }

       bindAdminEvents({ auth, signOut: authSdk.signOut });
    await ensureStorage();
       renderDashboard();
   } catch (error) {
       console.error("Không thể xác thực trang quản trị:", error);
       main.textContent = "Không thể xác thực quyền quản trị. Kiểm tra cấu hình Firebase rồi tải lại trang.";
   }
}

window.addEventListener("storage", (event) => {
   const activeHeading = main.querySelector(".panel h2");
   if (event.key === "flowerInventory" && activeHeading?.textContent === "Quản lý kho hoa") {
       renderProductTab();
   }
   if (event.key === STORAGE_KEYS.orders && activeHeading?.textContent === "Quản lý đơn hàng") {
       renderOrdersTab();
   }
});

window.addEventListener("flowerinventorychange", () => {
    const activeHeading = main.querySelector(".panel h2");
    if (activeHeading?.textContent === "Quản lý kho hoa") {
         renderProductTab();
    }
});

initializeAdmin();

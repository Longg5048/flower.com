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

const STORAGE_KEYS = {
   products: "flowerInventory",
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

function getDefaultProducts() {
   return [
       { id: 1, title: "Vibrant Spring Basket", price: 739900, quantity: 18, image: "https://img-src2.akamaized.net/img/p/GEN/lgwt/1103.jpg", description: "Giỏ hoa tươi rực rỡ cho ngày sinh nhật." },
       { id: 2, title: "Floral Jewels Arrangement", price: 649900, quantity: 12, image: "https://img-src2.akamaized.net/img/p/GEN/lgwt/2344.jpg", description: "Hoa đẹp, sáng và tươi mới." },
       { id: 3, title: "Lovely Lavender Bouquet", price: 599900, quantity: 15, image: "https://img-src2.akamaized.net/img/p/GEN/lgwt/2976.jpg", description: "Bó hoa tím dịu dàng, sang trọng." },
       { id: 4, title: "Rising Star", price: 699900, quantity: 9, image: "https://img-src2.akamaized.net/img/p/GEN/lgwt/8501.jpg", description: "Hoa hồng và lily tinh tế." }
   ];
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

function ensureStorage() {
   if (!localStorage.getItem(STORAGE_KEYS.products)) {
       localStorage.setItem(STORAGE_KEYS.products, JSON.stringify(getDefaultProducts()));
   }

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
   return JSON.parse(localStorage.getItem(STORAGE_KEYS.products) || "[]");
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
   main.innerHTML = `
       <section class="panel-wrap">
           <div class="panel">
               <h2>Quản lý kho hoa</h2>
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
                       ${products.map((item) => `
                           <tr>
                               <td><img src="${item.image}" alt="${item.title}"></td>
                               <td>
                                   <div><strong>${item.title}</strong></div>
                                   <small>${item.description || ""}</small>
                               </td>
                               <td>${moneyFormat(item.price)}</td>
                               <td><input class="qty-input" data-id="${item.id}" type="number" min="0" value="${item.quantity || 0}"></td>
                               <td class="product-actions">
                                   <button class="save-qty" data-id="${item.id}">Lưu</button>
                                   <button class="delete-record" data-record-type="products" data-record-id="${item.id}" type="button">Xóa</button>
                               </td>
                           </tr>
                       `).join("")}
                   </tbody>
               </table>
           </div>
           <div class="panel">
               <h2>Thêm hoa mới</h2>
               <form id="add-product-form" class="product-form">
                   <input type="text" id="product-title" placeholder="Tên hoa" required>
                   <input type="text" id="product-image" placeholder="Link hình ảnh" required>
                   <input type="number" id="product-price" placeholder="Giá (VND)" min="0" required>
                   <input type="number" id="product-quantity" placeholder="Số lượng" min="0" required>
                   <textarea id="product-description" placeholder="Mô tả sản phẩm" required></textarea>
                   <button type="submit" class="submit-btn">Thêm sản phẩm</button>
               </form>
           </div>
       </section>
   `;
}

function getProductById(productId) {
   const products = getProducts();
   return products.find((product) => String(product.id) === String(productId));
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
   const orders = getOrders().sort((first, second) => {
       const priorityDifference = (statusPriority[first.status] ?? 1) - (statusPriority[second.status] ?? 1);
       if (priorityDifference !== 0) return priorityDifference;
       return new Date(second.createdAt).getTime() - new Date(first.createdAt).getTime();
   });
   main.innerHTML = `
       <section class="panel">
           <h2>Quản lý đơn hàng</h2>
           <div class="list-stack">
               ${orders.length ? orders.map((order) => `
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
                           ${order.items.map((item) => {
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

                       <div class="reply-box compact-box">
                           <label class="field-label">Trạng thái đơn hàng</label>
                           <div class="status-row">
                               <select class="status-select" data-order-id="${order.id}">
                                   ${ORDER_STATUSES.map(status => `
                                       <option value="${status}" ${status === order.status ? "selected" : ""}>${status}</option>
                                   `).join("")}
                               </select>
                               <button class="status-save" data-order-id="${order.id}">Lưu lịch trình</button>
                               <button class="delete-record" data-record-type="orders" data-record-id="${order.id}" type="button">Xóa đơn hàng</button>
                           </div>
                       </div>
                   </div>
               `).join("") : '<div class="empty-state">Chưa có đơn hàng nào.</div>'}
           </div>
       </section>
   `;
}

function renderReviewsTab() {
   const reviews = getReviews();
   main.innerHTML = `
       <section class="panel">
           <h2>Đánh giá khách hàng</h2>
           <div class="list-stack">
               ${reviews.length ? reviews.map((review) => `
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
                               <button class="delete-record" data-record-type="reviews" data-record-id="${review.id}" type="button">Xóa đánh giá</button>
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
               `).join("") : '<div class="empty-state">Chưa có đánh giá nào.</div>'}
           </div>
       </section>
   `;
}

function renderMessagesTab() {
   const messages = getMessages();
   main.innerHTML = `
       <section class="panel">
           <h2>Phản hồi từ khách hàng</h2>
           <div class="list-stack">
               ${messages.length ? messages.map((message) => `
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
                               <button class="delete-record" data-record-type="messages" data-record-id="${message.id}" type="button">Xóa phản hồi</button>
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
               `).join("") : '<div class="empty-state">Chưa có tin nhắn nào.</div>'}
           </div>
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
   localStorage.setItem(STORAGE_KEYS.products, JSON.stringify(nextProducts));
   renderProductTab();
}

function addNewProduct(event) {
   event.preventDefault();
   const title = document.getElementById("product-title").value.trim();
   const image = document.getElementById("product-image").value.trim();
   const price = Number(document.getElementById("product-price").value);
   const quantity = Number(document.getElementById("product-quantity").value);
   const description = document.getElementById("product-description").value.trim();

   if (!title || !image || !description || Number.isNaN(price) || Number.isNaN(quantity)) {
       alert("Vui lòng nhập đầy đủ thông tin.");
       return;
   }

   const products = getProducts();
   products.push({
       id: Date.now(),
       title,
       image,
       price,
       quantity,
       description
   });

   localStorage.setItem(STORAGE_KEYS.products, JSON.stringify(products));
   alert("Thêm sản phẩm thành công!");
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
       products: { key: STORAGE_KEYS.products, label: "sản phẩm", render: renderProductTab },
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
   const deleteButton = event.target.closest(".delete-record");
   if (deleteButton) {
       deleteAdminRecord(deleteButton.dataset.recordType, deleteButton.dataset.recordId);
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
       ensureStorage();
       renderDashboard();
   } catch (error) {
       console.error("Không thể xác thực trang quản trị:", error);
       main.textContent = "Không thể xác thực quyền quản trị. Kiểm tra cấu hình Firebase rồi tải lại trang.";
   }
}

window.addEventListener("storage", (event) => {
   const activeHeading = main.querySelector(".panel h2");
   if (event.key === STORAGE_KEYS.orders && activeHeading?.textContent === "Quản lý đơn hàng") {
       renderOrdersTab();
   }
});

initializeAdmin();

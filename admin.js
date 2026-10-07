const productApi = "https://63cae32cf36cbbdfc76280f7.mockapi.io/data";
const main = document.getElementById("main");
const productPage = document.getElementById("product_page");
const addProduct = document.getElementById("add_product");

let products = [];
let searchTerm = "";

function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, (character) => ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        '"': "&quot;",
        "'": "&#39;"
    })[character]);
}

function getDescription(product) {
    return product.description ?? product.discription ?? "";
}

function setActiveNav(activeButton) {
    [productPage, addProduct].forEach((button) => {
        button.classList.toggle("is-active", button === activeButton);
    });
}

async function loadProducts() {
    main.innerHTML = '<p class="page-message">Đang tải danh sách sản phẩm...</p>';

    try {
        const response = await fetch(productApi);
        if (!response.ok) throw new Error("Không thể tải danh sách sản phẩm.");
        products = await response.json();
        renderProducts();
    } catch (error) {
        main.innerHTML = `<section class="page-message error-message"><p>${escapeHtml(error.message)}</p><button class="button button-primary" type="button" data-action="reload">Thử lại</button></section>`;
    }
}

function renderProducts() {
    const normalizedSearch = searchTerm.trim().toLocaleLowerCase("vi");
    const filteredProducts = products.filter((product) => {
        const searchableText = `${product.title} ${getDescription(product)}`.toLocaleLowerCase("vi");
        return searchableText.includes(normalizedSearch);
    });

    main.innerHTML = `
        <section class="page-heading">
            <div>
                <p class="eyebrow">KHO SẢN PHẨM</p>
                <h1>Quản lý sản phẩm</h1>
                <p class="result-count">${filteredProducts.length} / ${products.length} sản phẩm</p>
            </div>
            <button class="button button-primary heading-add" type="button" data-action="new">+ Thêm sản phẩm</button>
        </section>
        <form class="search-bar" id="search-form" role="search">
            <label class="visually-hidden" for="product-search">Tìm sản phẩm</label>
            <input id="product-search" name="search" type="search" placeholder="Tìm theo tên hoặc mô tả..." value="${escapeHtml(searchTerm)}">
            <button class="button button-secondary" type="submit">Tìm kiếm</button>
            ${searchTerm ? '<button class="clear-search" type="button" data-action="clear-search">Xóa tìm kiếm</button>' : ""}
        </form>
        ${filteredProducts.length ? `
            <section class="product-grid" aria-label="Danh sách sản phẩm">
                ${filteredProducts.map((product) => `
                    <article class="product-card">
                        <div class="product-image-wrap">
                            <img src="${escapeHtml(product.image)}" alt="${escapeHtml(product.title)}" loading="lazy">
                        </div>
                        <div class="product-info">
                            <h2>${escapeHtml(product.title)}</h2>
                            <p class="product-price">${escapeHtml(formatVnd(product.price))}</p>
                            <p class="product-description">${escapeHtml(getDescription(product)) || "Chưa có mô tả."}</p>
                            <div class="product-actions">
                                <button class="button button-secondary" type="button" data-action="edit" data-id="${escapeHtml(product.id)}">Chỉnh sửa</button>
                                <button class="button button-danger" type="button" data-action="delete" data-id="${escapeHtml(product.id)}">Xóa</button>
                            </div>
                        </div>
                    </article>
                `).join("")}
            </section>
        ` : '<p class="empty-state">Không tìm thấy sản phẩm phù hợp.</p>'}
    `;
}

function renderForm(product = null) {
    const isEditing = Boolean(product);
    setActiveNav(isEditing ? productPage : addProduct);
    main.innerHTML = `
        <section class="form-page">
            <button class="back-button" type="button" data-action="back">← Danh sách sản phẩm</button>
            <p class="eyebrow">${isEditing ? "CẬP NHẬT KHO" : "BỔ SUNG VÀO KHO"}</p>
            <h1>${isEditing ? "Chỉnh sửa sản phẩm" : "Thêm sản phẩm mới"}</h1>
            <form id="product-form" data-id="${escapeHtml(product?.id ?? "")}">
                <label for="product-title">Tên sản phẩm</label>
                <input id="product-title" name="title" type="text" value="${escapeHtml(product?.title ?? "")}" required maxlength="120" placeholder="Ví dụ: Bó hoa mùa xuân">

                <label for="product-image">Đường dẫn hình ảnh</label>
                <input id="product-image" name="image" type="url" value="${escapeHtml(product?.image ?? "")}" required placeholder="https://...">

                <label for="product-price">Giá bán (VNĐ)</label>
                <input id="product-price" name="price" type="number" min="1" step="1" value="${product ? escapeHtml(toVndAmount(product.price)) : ""}" required placeholder="Ví dụ: 350000">

                <label for="product-description">Mô tả</label>
                <textarea id="product-description" name="description" rows="5" required maxlength="1000" placeholder="Mô tả ngắn về sản phẩm">${escapeHtml(product ? getDescription(product) : "")}</textarea>

                <div class="form-actions">
                    <button class="button button-primary" type="submit">${isEditing ? "Lưu thay đổi" : "Thêm sản phẩm"}</button>
                    <button class="button button-secondary" type="button" data-action="back">Hủy</button>
                </div>
                <p class="form-status" role="status"></p>
            </form>
        </section>
    `;
    document.getElementById("product-title").focus();
}

async function deleteProduct(id) {
    const product = products.find((item) => String(item.id) === String(id));
    if (!product || !window.confirm(`Bạn có chắc muốn xóa “${product.title}”?`)) return;

    try {
        const response = await fetch(`${productApi}/${encodeURIComponent(id)}`, { method: "DELETE" });
        if (!response.ok) throw new Error("Không thể xóa sản phẩm.");
        await loadProducts();
    } catch (error) {
        window.alert(error.message);
    }
}

async function saveProduct(form) {
    const formData = new FormData(form);
    const product = {
        title: formData.get("title").trim(),
        image: formData.get("image").trim(),
        price: usdFromVnd(formData.get("price")),
        description: formData.get("description").trim()
    };
    const id = form.dataset.id;
    const status = form.querySelector(".form-status");
    const submitButton = form.querySelector('[type="submit"]');
    submitButton.disabled = true;
    status.textContent = "Đang lưu...";

    try {
        const response = await fetch(id ? `${productApi}/${encodeURIComponent(id)}` : productApi, {
            method: id ? "PUT" : "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(product)
        });
        if (!response.ok) throw new Error("Không thể lưu sản phẩm. Vui lòng thử lại.");
        searchTerm = "";
        setActiveNav(productPage);
        await loadProducts();
    } catch (error) {
        status.textContent = error.message;
        submitButton.disabled = false;
    }
}

main.addEventListener("click", (event) => {
    const button = event.target.closest("[data-action]");
    if (!button) return;

    const { action, id } = button.dataset;
    if (action === "new") renderForm();
    if (action === "back") {
        setActiveNav(productPage);
        renderProducts();
    }
    if (action === "edit") {
        const product = products.find((item) => String(item.id) === String(id));
        if (product) renderForm(product);
    }
    if (action === "delete") deleteProduct(id);
    if (action === "reload") loadProducts();
    if (action === "clear-search") {
        searchTerm = "";
        renderProducts();
    }
});

main.addEventListener("submit", (event) => {
    if (event.target.id === "search-form") {
        event.preventDefault();
        searchTerm = new FormData(event.target).get("search");
        renderProducts();
    }
    if (event.target.id === "product-form") {
        event.preventDefault();
        saveProduct(event.target);
    }
});

productPage.addEventListener("click", () => {
    setActiveNav(productPage);
    renderProducts();
});

addProduct.addEventListener("click", () => renderForm());

loadProducts();
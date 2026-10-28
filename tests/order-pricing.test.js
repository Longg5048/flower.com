// Unit tests: prices, promotions and totals (server api/orders.js and browser currency.js).
const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { internals } = require("../flower.com/api/orders.js");

const { vnd, priceInUsd, priceCart, findPromotion } = internals;
const VND_PER_USD = 25949.123093;

function loadBrowserCurrency(selectedPromotionId = "") {
    const context = {
        window: {
            FlowerSafeStorage: { readText: () => selectedPromotionId, readJson: () => [] },
            addEventListener() {}
        },
        document: { getElementById: () => null },
        Intl
    };
    vm.createContext(context);
    vm.runInContext(fs.readFileSync(path.join(__dirname, "../flower.com/currency.js"), "utf8"), context);
    return context;
}

const promo20All = { id: "p20", name: "Giảm 20%", discountPercent: 20, active: true, allProducts: true, productIds: [] };
const promo30One = { id: "p30", name: "Giảm 30% hoa 1", discountPercent: 30, active: true, productIds: ["1"] };

test("vnd() đổi USD sang VND và làm tròn tới đồng", () => {
    assert.equal(vnd(1), 25949);
    assert.equal(vnd(73.99), Math.round(73.99 * VND_PER_USD));
    assert.equal(vnd(0), 0);
});

test("priceInUsd() hiểu cả giá cũ đã lưu bằng VND", () => {
    assert.equal(priceInUsd(73.99), 73.99);
    assert.ok(Math.abs(priceInUsd(1919976) - 73.99) < 0.001);
    assert.ok(Number.isNaN(priceInUsd("abc")));
});

test("priceCart() không ưu đãi: tổng = giá × số lượng", () => {
    const totals = priceCart([{ id: "1", price: 73.99, quantity: 2 }, { id: "3", price: 59.99, quantity: 1 }], null);
    assert.deepEqual(totals, { subtotal: vnd(73.99 * 2 + 59.99), savings: 0, tax: 0, total: vnd(73.99 * 2 + 59.99) });
});

test("priceCart() ưu đãi toàn bộ sản phẩm", () => {
    const totals = priceCart([{ id: "1", price: 73.99, quantity: 2 }, { id: "3", price: 59.99, quantity: 1 }], promo20All);
    assert.equal(totals.subtotal, 5396639);
    assert.equal(totals.savings, 1079328);
    assert.equal(totals.total, 4317311);
});

test("priceCart() ưu đãi chỉ áp dụng cho sản phẩm được chọn", () => {
    const totals = priceCart([{ id: "1", price: 10, quantity: 1 }, { id: "2", price: 10, quantity: 1 }], promo30One);
    assert.equal(totals.savings, vnd(3));
    assert.equal(totals.total, vnd(20) - vnd(3));
});

test("priceCart() tiền giảm không vượt quá tạm tính (giảm 100%)", () => {
    const totals = priceCart([{ id: "1", price: 10, quantity: 3 }], { ...promo20All, discountPercent: 100 });
    assert.equal(totals.total, 0);
    assert.equal(totals.savings, totals.subtotal);
});

test("findPromotion() trả về null khi không chọn ưu đãi", () => {
    assert.equal(findPromotion([promo20All], ""), null);
});

test("findPromotion() từ chối ưu đãi không tồn tại, đã tắt hoặc sai phần trăm", () => {
    assert.throws(() => findPromotion([promo20All], "nope"), { code: "invalid-promotion" });
    assert.throws(() => findPromotion([{ ...promo20All, active: false }], "p20"), { code: "invalid-promotion" });
    for (const discountPercent of [0, -5, 101, "abc"]) {
        assert.throws(() => findPromotion([{ ...promo20All, discountPercent }], "p20"), { code: "invalid-promotion" });
    }
    assert.equal(findPromotion([promo20All], "p20"), promo20All);
});

test("giá hiển thị ở trình duyệt khớp giá máy chủ tính (không lệch khi đặt hàng)", () => {
    const cart = [{ id: "1", price: 73.99, quantity: 2 }, { id: "3", price: 59.99, quantity: 1 }, { id: "9", price: 84.99, quantity: 3 }];
    for (const promotion of [null, promo20All, promo30One]) {
        const browser = loadBrowserCurrency(promotion?.id || "");
        browser.setTrustedPromotions(promotion ? [promotion] : []);
        const client = browser.calculateCartTotals(cart, promotion?.id || "");
        const server = priceCart(cart, promotion);
        assert.ok(Math.abs(browser.toVndAmount(client.total) - server.total) <= 1, `lệch giá với ưu đãi ${promotion?.id || "không"}`);
    }
});

test("trình duyệt chỉ liệt kê ưu đãi đang bật và có sản phẩm trong giỏ", () => {
    const browser = loadBrowserCurrency();
    browser.setTrustedPromotions([promo20All, promo30One, { ...promo30One, id: "off", active: false }, { ...promo30One, id: "p99", productIds: ["99"] }]);
    const ids = browser.getAvailableCartPromotions([{ id: "1", price: 1, quantity: 1 }]).map((item) => item.id);
    assert.deepEqual(ids, ["p20", "p30"]);
});

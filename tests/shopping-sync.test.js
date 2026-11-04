const test = require("node:test");
const assert = require("node:assert/strict");

const shoppingSyncPromise = import("../flower.com/js/core/shopping-sync.js");

test("đồng bộ chỉ giữ trường cần cho mua hàng và từ chối dữ liệu không hợp lệ", async () => {
  const { internals } = await shoppingSyncPromise;
  assert.deepEqual(internals.normalizeShoppingList([{
    id: "line-rose",
    productId: 7,
    title: "Hoa hồng",
    price: 250000,
    quantity: 2,
    image: "./images/rose.webp",
    personalNote: "Giao buổi sáng",
    privateField: "không được đồng bộ"
  }], "cart"), [{
    id: "line-rose",
    productId: 7,
    title: "Hoa hồng",
    price: 250000,
    quantity: 2,
    image: "./images/rose.webp",
    personalNote: "Giao buổi sáng"
  }]);

  assert.throws(() => internals.normalizeShoppingList("not a list", "cart"), { code: "invalid-shopping-data" });
  assert.throws(() => internals.normalizeShoppingList([{ id: "line", quantity: 0 }], "cart"), { code: "invalid-shopping-data" });
  assert.throws(() => internals.normalizeShoppingList(Array.from({ length: 101 }, (_, index) => ({ id: String(index) })), "wish"), {
    code: "shopping-list-limit"
  });
});

test("lần đồng bộ đầu gộp giỏ cũ mà không cộng trùng số lượng và hợp nhất yêu thích", async () => {
  const { internals } = await shoppingSyncPromise;
  const merged = internals.mergeInitialData(
    {
      cart: [{ id: "same", productId: 1, title: "Hoa", quantity: 2 }, { id: "remote", productId: 2, quantity: 1 }],
      wish: [{ id: 2, title: "Hoa ly" }]
    },
    {
      cart: [{ id: "same", productId: 1, title: "Hoa", quantity: 3 }, { id: "local", productId: 3, quantity: 1 }],
      wish: [{ id: 2, title: "Hoa ly cũ" }, { id: 3, title: "Hoa tulip" }]
    }
  );

  assert.deepEqual(merged.cart.map(({ id, quantity }) => [id, quantity]), [
    ["same", 3],
    ["remote", 1],
    ["local", 1]
  ]);
  assert.deepEqual(merged.wish.map(({ id }) => id), [2, 3]);
});

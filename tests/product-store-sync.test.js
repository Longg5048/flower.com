const assert = require('assert');
const { mergeCatalogProducts, normalizeProduct } = require('../flower.com/product-store.js');

const catalog = [
  { id: 1, title: 'Hoa hồng', price: 250000, quantity: 8, image: 'rose.jpg', description: 'Hoa hồng đỏ' },
  { id: 2, title: 'Hoa cúc', price: 320000, quantity: 5, image: 'chrysanthemum.jpg', description: 'Hoa cúc vàng' },
];

const existing = [
  { id: 1, title: 'Hoa hồng cập nhật', price: 260000, quantity: 10, image: 'rose.jpg', description: 'Hoa hồng đỏ' },
  { id: 3, title: 'Hoa lan', price: 410000, quantity: 6, image: 'orchid.jpg', description: 'Hoa lan trắng' },
];

const merged = mergeCatalogProducts(existing, catalog);

assert.strictEqual(Array.isArray(merged), true);
assert.strictEqual(merged.length, 3);
assert.strictEqual(merged.find((item) => item.id === 1).title, 'Hoa hồng cập nhật');
assert.strictEqual(merged.find((item) => item.id === 2).title, 'Hoa cúc');
assert.strictEqual(normalizeProduct({ id: '7', title: '  Hoa tulip  ', price: '750000', quantity: '9', image: 'tulip.jpg' }).price, 750000);

console.log('product-store sync test passed');

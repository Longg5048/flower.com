const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { PRODUCT_IMAGES, productNameFor, imageForProduct } = require("../flower.com/lib/product-images.js");
const catalog = require("../flower.com/flower.json").flower;
const site = path.join(__dirname, "..", "flower.com");

test("mỗi tên hoa có một ảnh riêng và file ảnh có thật", () => {
  const images = Object.values(PRODUCT_IMAGES);
  assert.equal(new Set(images).size, images.length, "hai tên hoa không được dùng chung một ảnh");
  for (const image of images) {
    assert.match(image, /^\.\/images\/products\/[a-z0-9-]+\.webp$/);
    assert.ok(fs.statSync(path.join(site, image)).size > 1000, image);
  }
});

test("tên bị lỗi dấu thành ? vẫn tìm đúng hoa; tên lạ không đổi ảnh", () => {
  assert.equal(productNameFor("B? hoa ??ng n?i n??c Ph?p"), "Bó hoa đồng nội nước Pháp");
  assert.equal(productNameFor("Gi? hoa v?ng l?i ch?c l?nh"), "Giỏ hoa vàng lời chúc lành");
  assert.equal(imageForProduct("Bó hồng trong mơ"), PRODUCT_IMAGES["Bó hồng trong mơ"]);
  assert.equal(imageForProduct("  Bó hồng trong mơ "), PRODUCT_IMAGES["Bó hồng trong mơ"]);
  assert.equal(imageForProduct("Hải"), null);
  assert.equal(imageForProduct("Bó hồng trong mơ đẹp"), null);
  assert.equal(imageForProduct(undefined), null);
});

test("catalog mặc định dùng ảnh riêng của từng tên hoa", () => {
  for (const product of catalog) assert.equal(product.image, imageForProduct(product.title), product.title);
});

// Picks the bouquet photo made for a product from its name (lib/product-images.json).
// Some products in Firestore were saved with a broken encoding: every Vietnamese letter became
// "?" ("B? h?ng trong m?"). A "?" therefore matches any single letter of the real name.
const PRODUCT_IMAGES = require("./product-images.json");

function sameName(title, name) {
    const left = Array.from(String(title || "").normalize("NFC").trim());
    const right = Array.from(String(name).normalize("NFC"));
    return left.length === right.length && left.every((char, index) => char === "?" || char === right[index]);
}

function productNameFor(title) {
    return Object.keys(PRODUCT_IMAGES).find((name) => sameName(title, name)) || null;
}

function imageForProduct(title) {
    const name = productNameFor(title);
    return name ? PRODUCT_IMAGES[name] : null;
}

module.exports = { PRODUCT_IMAGES, productNameFor, imageForProduct };

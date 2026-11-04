const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const projectRoot = path.join(__dirname, "..", "flower.com");
const catalog = require("../flower.com/flower.json").flower;
const textExtensions = new Set([".css", ".html", ".js", ".json", ".md"]);

function listSourceFiles(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const filePath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      return entry.name === "node_modules" || entry.name === ".git" ? [] : listSourceFiles(filePath);
    }
    return textExtensions.has(path.extname(entry.name).toLowerCase()) ? [filePath] : [];
  });
}

test("storefront source no longer references third-party brand asset hosts or product-name mapper", () => {
  const source = listSourceFiles(projectRoot)
    .map((filePath) => fs.readFileSync(filePath, "utf8"))
    .join("\n");

  assert.doesNotMatch(source, /contentstack|akamaized/i);
  assert.doesNotMatch(source, /product-names\.js|getVietnameseProductName|getVietnameseProductDescription/);
  assert.equal(fs.existsSync(path.join(projectRoot, "product-names.js")), false);
});

test("seed product names and descriptions are localized and reference existing local images", () => {
  const projectRootWithSeparator = `${path.resolve(projectRoot)}${path.sep}`;
  assert.ok(catalog.length > 0);

  for (const product of catalog) {
    assert.match(product.title, /[\u00C0-\u024F]/, `product ${product.id} title should be Vietnamese`);
    assert.match(product.description, /[\u00C0-\u024F]/, `product ${product.id} description should be Vietnamese`);
    assert.match(product.image, /^\.\/images\//, `product ${product.id} should use a local image`);

    const imagePath = path.resolve(projectRoot, product.image);
    assert.ok(imagePath.startsWith(projectRootWithSeparator), `product ${product.id} image must stay in the project`);
    assert.ok(fs.existsSync(imagePath), `product ${product.id} image should exist`);
  }
});

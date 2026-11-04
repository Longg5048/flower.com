// Gives every product the bouquet photo made for its name (lib/product-images.json).
//   npm run product-images            -> only lists what would change
//   npm run product-images -- --dong-y  -> writes the new image paths into Firestore
// Products whose name is not in the list (for example ones added by hand) are left alone.
const { imageForProduct } = require("../lib/product-images");
const { connectFirestore } = require("./firestore-client");

(async () => {
    const apply = process.argv.includes("--dong-y");
    const { db, projectId } = connectFirestore();
    const snapshot = await db.collection("products").get();
    const changes = [];
    const skipped = [];
    snapshot.docs.forEach((doc) => {
        const { title, image } = doc.data();
        const next = imageForProduct(title);
        if (!next) skipped.push(title);
        else if (next !== image) changes.push({ doc, title, from: image, to: next });
    });
    console.log(`Project ${projectId}: ${snapshot.size} sản phẩm, ${changes.length} sẽ đổi ảnh.`);
    changes.forEach(({ title, from, to }) => console.log(`  ${title}: ${from || "(trống)"} -> ${to}`));
    if (skipped.length) console.log(`Giữ nguyên ${skipped.length} sản phẩm không có trong danh sách: ${skipped.join(", ")}`);
    if (!apply) {
        console.log("\nChưa ghi gì vào Firestore. Kiểm tra danh sách trên, rồi chạy lại kèm --dong-y để đổi ảnh.");
        return;
    }
    for (let index = 0; index < changes.length; index += 400) {
        const batch = db.batch();
        changes.slice(index, index + 400).forEach(({ doc, to }) => batch.update(doc.ref, { image: to }));
        await batch.commit();
    }
    console.log(`Đã đổi ảnh ${changes.length} sản phẩm. Mở trang web và bấm Ctrl + F5 để xem.`);
})().catch((error) => {
    console.error(`LỖI: ${error.message}`);
    process.exitCode = 1;
});

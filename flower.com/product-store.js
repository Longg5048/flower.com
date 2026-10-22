(() => {
    const PRODUCT_KEY = "flowerInventory";
    const CATEGORY_KEY = "flowerCategories";
    let initialization;
    let synchronization;
    let firebaseApiPromise;
    let unsubscribeProducts;
    let unsubscribeCategories;

    function storage() {
        try {
            return window.localStorage;
        } catch (error) {
            return null;
        }
    }

    function normalizeProduct(product = {}) {
        const rawId = product.id ?? product._id;
        const numericId = Number(rawId);
        const rawPrice = Number(product.price ?? 0);
        const price = rawPrice > 10000
            ? (typeof usdFromVnd === "function" ? usdFromVnd(rawPrice) : rawPrice / 25949.123093)
            : rawPrice;
        const title = String(product.title ?? "Sản phẩm").trim();
        const description = String(product.description ?? "").trim();
        return {
            id: Number.isFinite(numericId) && numericId > 0 ? numericId : String(rawId ?? Date.now()),
            title,
            price,
            quantity: Number(product.quantity ?? product.stock ?? 10),
            image: product.image || "",
            images: Array.isArray(product.images) ? product.images.filter((image) => typeof image === "string" && image) : [],
            category: String(product.category || "").trim(),
            categoryKey: String(product.categoryKey || "").trim(),
            sameDayDelivery: product.sameDayDelivery === true,
            onSale: product.onSale === true,
            description: description.includes("?")
                ? `${title} được tuyển chọn và sắp xếp tỉ mỉ, phù hợp làm quà tặng cho những dịp đặc biệt. Màu sắc và chủng loại hoa có thể thay đổi nhẹ theo mùa.`
                : description
        };
    }

    function deduplicateProducts(products) {
        const uniqueProducts = new Map();
        // Products are identified by id only: different products may share an image or a title.
        (Array.isArray(products) ? products : []).map(normalizeProduct).forEach((product) => {
            const identity = String(product.id);
            if (!uniqueProducts.has(identity)) uniqueProducts.set(identity, product);
        });
        return Array.from(uniqueProducts.values());
    }

    function mergeCatalogProducts(existing = [], catalog = []) {
        const catalogById = new Map((Array.isArray(catalog) ? catalog : []).map((item) => {
            const product = normalizeProduct(item);
            return [String(product.id), product];
        }));
        const merged = new Map((Array.isArray(existing) ? existing : []).map((item) => {
            const product = normalizeProduct(item);
            const catalogProduct = catalogById.get(String(product.id));
            if (product.title.includes("?") && catalogProduct && !catalogProduct.title.includes("?")) {
                product.title = catalogProduct.title;
            }
            return [String(product.id), product];
        }));
        (Array.isArray(catalog) ? catalog : []).forEach((item) => {
            const product = normalizeProduct(item);
            if (!merged.has(String(product.id))) merged.set(String(product.id), product);
        });
        return deduplicateProducts(Array.from(merged.values()));
    }

    function readProducts() {
        try {
            if (typeof window !== "undefined" && window.FlowerSafeStorage?.readJson) {
                return deduplicateProducts(window.FlowerSafeStorage.readJson(PRODUCT_KEY, {
                    fallback: [],
                    validate: (value) => Array.isArray(value)
                        && value.every((product) => product
                            && typeof product === "object"
                            && !Array.isArray(product)
                            && (product.id !== undefined || product._id !== undefined)
                            && typeof product.title === "string"
                            && Number.isFinite(Number(product.price))),
                    throwOnError: true
                }));
            }
            return deduplicateProducts(JSON.parse(storage()?.getItem(PRODUCT_KEY) || "[]"));
        } catch (error) {
            console.warn("Không đọc được kho sản phẩm:", error);
            return [];
        }
    }

    function cacheProducts(products, notify = true) {
        const normalized = deduplicateProducts(products);
        if (typeof window !== "undefined" && window.FlowerSafeStorage?.writeJson) {
            window.FlowerSafeStorage.writeJson(PRODUCT_KEY, normalized);
        } else {
            storage()?.setItem(PRODUCT_KEY, JSON.stringify(normalized));
        }
        if (notify) {
            window.dispatchEvent(new CustomEvent("flowerinventorychange", { detail: normalized }));
        }
        return normalized;
    }

    async function getFirebaseApi() {
        if (!firebaseApiPromise) {
            firebaseApiPromise = (async () => {
                const configModule = await import("./firebase-config.js");
                const [appSdk, authSdk, firestoreSdk] = await Promise.all([
                    import("https://www.gstatic.com/firebasejs/11.6.0/firebase-app.js"),
                    import("https://www.gstatic.com/firebasejs/11.6.0/firebase-auth.js"),
                    import("https://www.gstatic.com/firebasejs/11.6.0/firebase-firestore.js")
                ]);
                const app = appSdk.getApps().length
                    ? appSdk.getApp()
                    : appSdk.initializeApp(configModule.firebaseConfig);
                return {
                    app,
                    auth: authSdk.getAuth(app),
                    db: firestoreSdk.getFirestore(app),
                    firestoreSdk
                };
            })().catch((error) => {
                firebaseApiPromise = null;
                console.warn("Firestore chưa sẵn sàng:", error);
                return null;
            });
        }
        return firebaseApiPromise;
    }

    async function readRemoteProducts() {
        const firebase = await getFirebaseApi();
        if (!firebase) return null;
        try {
            const { db, firestoreSdk } = firebase;
            const snapshot = await firestoreSdk.getDocs(firestoreSdk.collection(db, "products"));
            return snapshot.docs.map((doc) => normalizeProduct({ ...doc.data(), id: doc.id }));
        } catch (error) {
            console.warn("Không đọc được sản phẩm trên Firestore:", error);
            return null;
        }
    }

    function normalizeCategories(categories) {
        return [...new Set((Array.isArray(categories) ? categories : [])
            .map((category) => String(category || "").trim())
            .filter(Boolean))].sort((left, right) => left.localeCompare(right, "vi"));
    }

    function cacheCategories(categories, notify = true) {
        const normalized = normalizeCategories(categories);
        if (typeof window !== "undefined" && window.FlowerSafeStorage?.writeJson) {
            window.FlowerSafeStorage.writeJson(CATEGORY_KEY, normalized);
        } else {
            storage()?.setItem(CATEGORY_KEY, JSON.stringify(normalized));
        }
        if (notify && typeof window !== "undefined") {
            window.dispatchEvent(new CustomEvent("flowercategorieschange", { detail: normalized }));
        }
        return normalized;
    }

    function readCategories() {
        try {
            if (typeof window !== "undefined" && window.FlowerSafeStorage?.readJson) {
                return normalizeCategories(window.FlowerSafeStorage.readJson(CATEGORY_KEY, {
                    fallback: [],
                    validate: (value) => Array.isArray(value)
                        && value.every((category) => typeof category === "string"),
                    throwOnError: true
                }));
            }
            const categories = JSON.parse(storage()?.getItem(CATEGORY_KEY) || "[]");
            return normalizeCategories(categories);
        } catch (error) {
            console.warn("Không đọc được cache danh mục:", error);
            return [];
        }
    }

    async function getCategories() {
        const firebase = await getFirebaseApi();
        if (!firebase) return readCategories();
        const reference = firebase.firestoreSdk.doc(firebase.db, "metadata", "storeCategories");
        const snapshot = await firebase.firestoreSdk.getDoc(reference);
        const categories = snapshot.exists() ? snapshot.data().items : [];
        return cacheCategories(categories, false);
    }

    async function initializeCategories(legacyCategories = []) {
        const firebase = await getFirebaseApi();
        if (!firebase) throw new Error("Không kết nối được Firestore để tải danh mục dùng chung.");
        await firebase.auth.authStateReady();
        const isAdmin = Boolean(firebase.auth.currentUser
            && (await firebase.auth.currentUser.getIdTokenResult()).claims.admin === true);
        const reference = firebase.firestoreSdk.doc(firebase.db, "metadata", "storeCategories");
        if (!isAdmin) return getCategories();
        const categories = await firebase.firestoreSdk.runTransaction(firebase.db, async (transaction) => {
            const snapshot = await transaction.get(reference);
            if (snapshot.exists()) return snapshot.data().items;
            const initialCategories = normalizeCategories(legacyCategories);
            transaction.set(reference, {
                items: initialCategories,
                updatedAt: firebase.firestoreSdk.serverTimestamp()
            });
            return initialCategories;
        });
        return cacheCategories(categories);
    }

    async function saveCategories(categories) {
        const firebase = await getFirebaseApi();
        if (!firebase) throw new Error("Không kết nối được Firestore để lưu danh mục dùng chung.");
        await firebase.auth.authStateReady();
        if (!firebase.auth.currentUser
            || (await firebase.auth.currentUser.getIdTokenResult()).claims.admin !== true) {
            throw new Error("Chỉ quản trị viên mới được thay đổi danh mục.");
        }
        const normalized = normalizeCategories(categories);
        await firebase.firestoreSdk.setDoc(
            firebase.firestoreSdk.doc(firebase.db, "metadata", "storeCategories"),
            { items: normalized, updatedAt: firebase.firestoreSdk.serverTimestamp() }
        );
        return cacheCategories(normalized);
    }

    function subscribeCategories(onCategories, onError = () => {}) {
        if (unsubscribeCategories) unsubscribeCategories();
        let active = true;
        getFirebaseApi().then((firebase) => {
            if (!active || !firebase) return;
            unsubscribeCategories = firebase.firestoreSdk.onSnapshot(
                firebase.firestoreSdk.doc(firebase.db, "metadata", "storeCategories"),
                (snapshot) => {
                    const categories = cacheCategories(snapshot.exists() ? snapshot.data().items : []);
                    onCategories(categories);
                },
                onError
            );
        }).catch(onError);
        return () => {
            active = false;
            if (unsubscribeCategories) unsubscribeCategories();
            unsubscribeCategories = null;
        };
    }

    // Writes only the products that changed and deletes only the ones listed in removedIds,
    // so saving one product never touches (or wipes) the rest of the catalog.
    async function writeRemoteProducts(products, removedIds = []) {
        const firebase = await getFirebaseApi();
        if (!firebase) return false;
        try {
            const { db, firestoreSdk } = firebase;
            const snapshot = await firestoreSdk.getDocs(firestoreSdk.collection(db, "products"));
            const remoteById = new Map(snapshot.docs.map((doc) => [doc.id, normalizeProduct({ ...doc.data(), id: doc.id })]));
            const batch = firestoreSdk.writeBatch(db);
            let writes = 0;
            products.map(normalizeProduct).forEach((product) => {
                const remote = remoteById.get(String(product.id));
                if (remote && JSON.stringify(remote) === JSON.stringify(product)) return;
                batch.set(firestoreSdk.doc(db, "products", String(product.id)), product);
                writes += 1;
            });
            removedIds.map(String).forEach((id) => {
                if (!remoteById.has(id)) return;
                batch.delete(firestoreSdk.doc(db, "products", id));
                writes += 1;
            });
            if (writes) await batch.commit();
            return true;
        } catch (error) {
            console.warn("Không ghi được sản phẩm lên Firestore:", error);
            return false;
        }
    }

    async function syncMissingRemoteProducts(remoteProducts, products, markCatalogSeeded = false) {
        const firebase = await getFirebaseApi();
        if (!firebase) return false;
        try {
            const { db, firestoreSdk } = firebase;
            const existingIds = new Set(remoteProducts.map((product) => String(normalizeProduct(product).id)));
            const missingProducts = products.filter((product) => !existingIds.has(String(normalizeProduct(product).id)));
            const snapshot = await firestoreSdk.getDocs(firestoreSdk.collection(db, "products"));
            const productsNeedingPriceMigration = snapshot.docs.filter((doc) => {
                const normalized = normalizeProduct({ ...doc.data(), id: doc.id });
                return Number(doc.data().price) !== normalized.price;
            });
            if (!missingProducts.length && !productsNeedingPriceMigration.length && !markCatalogSeeded) return true;

            const batch = firestoreSdk.writeBatch(db);
            productsNeedingPriceMigration.forEach((doc) => {
                const normalized = normalizeProduct({ ...doc.data(), id: doc.id });
                batch.update(doc.ref, { price: normalized.price });
            });
            missingProducts.forEach((product) => {
                const normalized = normalizeProduct(product);
                batch.set(firestoreSdk.doc(db, "products", String(normalized.id)), normalized);
            });
            if (markCatalogSeeded) {
                batch.set(firestoreSdk.doc(db, "metadata", "productCatalog"), { catalogSeeded: true });
            }
            await batch.commit();
            return true;
        } catch (error) {
            console.warn("Không thể bổ sung sản phẩm còn thiếu trên Firestore:", error);
            return false;
        }
    }

    function subscribeToProducts(catalog) {
        if (unsubscribeProducts) return;
        getFirebaseApi().then((firebase) => {
            if (!firebase || unsubscribeProducts) return;
            const { db, firestoreSdk } = firebase;
            unsubscribeProducts = firestoreSdk.onSnapshot(
                firestoreSdk.collection(db, "products"),
                (snapshot) => {
                    if (snapshot.empty) return;
                    const remoteProducts = snapshot.docs.map((doc) => ({ ...doc.data(), id: doc.id }));
                    cacheProducts(mergeCatalogProducts(remoteProducts, catalog));
                },
                (error) => console.warn("Không nhận được cập nhật sản phẩm:", error)
            );
        });
    }

    async function saveProducts(products) {
        const normalized = deduplicateProducts(products);
        const keptIds = new Set(normalized.map((product) => String(product.id)));
        const removedIds = readProducts().map((product) => String(product.id)).filter((id) => !keptIds.has(id));
        const saved = await writeRemoteProducts(normalized, removedIds);
        if (!saved) {
            throw new Error("Could not synchronize the product catalog with Firestore.");
        }
        return cacheProducts(normalized);
    }

    async function synchronizeProducts(localProducts, catalog) {
        const firebase = await getFirebaseApi();
        const remoteProducts = firebase ? await readRemoteProducts() : null;
        if (remoteProducts === null) return;

        let isAdmin = false;
        try {
            const user = firebase.auth.currentUser;
            isAdmin = Boolean(user && (await user.getIdTokenResult()).claims.admin === true);
        } catch (error) {
            console.warn("Không xác minh được quyền khởi tạo catalog:", error);
        }

        let catalogSeeded = false;
        if (isAdmin) {
            try {
                const marker = await firebase.firestoreSdk.getDoc(
                    firebase.firestoreSdk.doc(firebase.db, "metadata", "productCatalog")
                );
                catalogSeeded = marker.exists() && marker.data().catalogSeeded === true;
            } catch (error) {
                console.warn("Không đọc được trạng thái khởi tạo catalog:", error);
            }
        }

        const existingProducts = remoteProducts.length ? remoteProducts : localProducts;
        const products = mergeCatalogProducts(existingProducts, catalog);
        if (isAdmin) {
            await syncMissingRemoteProducts(remoteProducts, products, !catalogSeeded);
        }
        cacheProducts(products);
        subscribeToProducts(catalog);
    }

    function startProductSynchronization(localProducts, catalog) {
        if (synchronization) return;
        synchronization = synchronizeProducts(localProducts, catalog)
            .catch((error) => console.warn("Không thể đồng bộ sản phẩm:", error))
            .finally(() => {
                synchronization = null;
            });
    }

    async function initialize() {
        if (initialization) return initialization;
        initialization = (async () => {
            const localProducts = readProducts();
            let catalog = [];
            try {
                const response = await fetch("./flower.json?v=2");
                if (response.ok) {
                    const data = await response.json();
                    catalog = Array.isArray(data.flower) ? data.flower.map((item) => ({ ...item, quantity: 10 })) : [];
                }
            } catch (error) {
                console.warn("Không tải được catalog sản phẩm:", error);
            }

            const products = mergeCatalogProducts(localProducts, catalog);
            cacheProducts(products, false);
            startProductSynchronization(localProducts, catalog);
            return products;
        })().finally(() => {
            initialization = null;
        });
        return initialization;
    }

    const api = {
        initialize,
        readProducts,
        saveProducts,
        mergeCatalogProducts,
        normalizeProduct,
        getCategories,
        readCategories,
        initializeCategories,
        saveCategories,
        subscribeCategories
    };
    if (typeof window !== "undefined") window.FlowerInventory = api;
    if (typeof module !== "undefined" && module.exports) module.exports = api;
})();

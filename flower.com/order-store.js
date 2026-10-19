(function initializeFlowerOrderStore(root) {
  let firebasePromise;

  async function getFirebase() {
    if (!firebasePromise) {
      firebasePromise = (async () => {
        const [{ firebaseConfig }, appSdk, authSdk, firestoreSdk] = await Promise.all([
          import("./firebase-config.js"),
          import("https://www.gstatic.com/firebasejs/11.6.0/firebase-app.js"),
          import("https://www.gstatic.com/firebasejs/11.6.0/firebase-auth.js"),
          import("https://www.gstatic.com/firebasejs/11.6.0/firebase-firestore.js")
        ]);
        const app = appSdk.getApps().length
          ? appSdk.getApp()
          : appSdk.initializeApp(firebaseConfig);
        return {
          auth: authSdk.getAuth(app),
          db: firestoreSdk.getFirestore(app),
          firestoreSdk
        };
      })().catch((error) => {
        firebasePromise = null;
        throw error;
      });
    }
    return firebasePromise;
  }

  function serializeDocument(document) {
    const data = document.data();
    return {
      id: document.id,
      ...data,
      createdAt: data.createdAt?.toDate?.().toISOString() || data.createdAt || "",
      trackingHistory: Array.isArray(data.trackingHistory)
        ? data.trackingHistory.map((event) => ({
            ...event,
            updatedAt: event.updatedAt?.toDate?.().toISOString() || event.updatedAt || ""
          }))
        : []
    };
  }

  function aggregateOrderItems(items) {
    const quantities = new Map();
    (Array.isArray(items) ? items : []).forEach((item) => {
      const id = String(item?.id ?? "");
      const quantity = Math.max(1, Number(item?.quantity) || 1);
      quantities.set(id, (quantities.get(id) || 0) + quantity);
    });
    return Array.from(quantities, ([id, quantity]) => ({ id, quantity }));
  }

  async function create(order, expectedTotals) {
    const { auth } = await getFirebase();
    await auth.authStateReady();
    const headers = { "Content-Type": "application/json", Accept: "application/json" };
    if (auth.currentUser) headers.Authorization = `Bearer ${await auth.currentUser.getIdToken()}`;
    const response = await fetch("/api/orders", {
      method: "POST",
      headers,
      body: JSON.stringify({
        customerName: order.customerName,
        phone: order.phone,
        address: order.address,
        orderNote: order.orderNote || "",
        paymentMethod: order.paymentMethod,
        paymentReference: order.paymentReference || "",
        promotionId: order.promotionId || "",
        expectedTotals,
        items: (Array.isArray(order.items) ? order.items : []).map((item) => ({
          id: item.id,
          quantity: item.quantity
        }))
      })
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
      const error = new Error(result.error || "Không thể xác nhận đơn hàng.");
      error.code = result.code || "order-create-failed";
      throw error;
    }
    return result.order.id;
  }

  async function quote(items, promotionId = "") {
    const response = await fetch("/api/orders", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({
        action: "quote",
        items: (Array.isArray(items) ? items : []).map((item) => ({ id: item.id, quantity: item.quantity })),
        promotionId: promotionId || ""
      })
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
      const error = new Error(result.error || "Không thể kiểm tra giá sản phẩm.");
      error.code = result.code || "order-quote-failed";
      throw error;
    }
    return result;
  }

  async function getPromotions() {
    const { db, firestoreSdk } = await getFirebase();
    const snapshot = await firestoreSdk.getDoc(firestoreSdk.doc(db, "metadata", "activePromotions"));
    return snapshot.exists() && Array.isArray(snapshot.data().items) ? snapshot.data().items : [];
  }

  async function savePromotions(promotions) {
    const { auth, db, firestoreSdk } = await getFirebase();
    await auth.authStateReady();
    if (!auth.currentUser || (await auth.currentUser.getIdTokenResult()).claims.admin !== true) {
      throw new Error("Chỉ quản trị viên mới được thay đổi ưu đãi.");
    }
    await firestoreSdk.setDoc(firestoreSdk.doc(db, "metadata", "activePromotions"), {
      items: Array.isArray(promotions) ? promotions : [],
      updatedAt: firestoreSdk.serverTimestamp()
    });
    return getPromotions();
  }

  // Admin-defined categories, kept even when no product uses them yet.
  async function getCategories() {
    const { db, firestoreSdk } = await getFirebase();
    const snapshot = await firestoreSdk.getDoc(firestoreSdk.doc(db, "metadata", "storeCategories"));
    return snapshot.exists() && Array.isArray(snapshot.data().items) ? snapshot.data().items : null;
  }

  async function saveCategories(categories) {
    const { db, firestoreSdk } = await getFirebase();
    await firestoreSdk.setDoc(firestoreSdk.doc(db, "metadata", "storeCategories"), {
      items: Array.isArray(categories) ? categories : [],
      updatedAt: firestoreSdk.serverTimestamp()
    });
  }

  async function initializePromotions(legacyPromotions = []) {
    const { auth, db, firestoreSdk } = await getFirebase();
    await auth.authStateReady();
    if (!auth.currentUser || (await auth.currentUser.getIdTokenResult()).claims.admin !== true) {
      throw new Error("Cần quyền quản trị để khởi tạo danh sách ưu đãi.");
    }
    const reference = firestoreSdk.doc(db, "metadata", "activePromotions");
    const snapshot = await firestoreSdk.getDoc(reference);
    if (snapshot.exists()) return Array.isArray(snapshot.data().items) ? snapshot.data().items : [];
    const promotions = Array.isArray(legacyPromotions) ? legacyPromotions : [];
    await firestoreSdk.setDoc(reference, {
      items: promotions,
      updatedAt: firestoreSdk.serverTimestamp()
    });
    return promotions;
  }

  async function subscribeAdmin(onOrders, onError) {
    const { auth, db, firestoreSdk } = await getFirebase();
    await auth.authStateReady();
    if (!auth.currentUser) throw new Error("Đăng nhập tài khoản quản trị để xem đơn hàng.");
    const token = await auth.currentUser.getIdTokenResult();
    if (token.claims.admin !== true) throw new Error("Tài khoản hiện tại không có quyền quản trị.");

    const ordersQuery = firestoreSdk.query(
      firestoreSdk.collection(db, "orders"),
      firestoreSdk.orderBy("createdAt", "desc")
    );
    return firestoreSdk.onSnapshot(ordersQuery, (snapshot) => {
      onOrders(snapshot.docs.map(serializeDocument));
    }, onError);
  }

  async function subscribeAccount(uid, onOrders, onError) {
    const { auth, db, firestoreSdk } = await getFirebase();
    await auth.authStateReady();
    if (!auth.currentUser || auth.currentUser.uid !== uid) {
      throw new Error("Phiên đăng nhập không khớp với tài khoản cần tra cứu.");
    }
    const ordersQuery = firestoreSdk.query(
      firestoreSdk.collection(db, "orders"),
      firestoreSdk.where("uid", "==", uid)
    );
    return firestoreSdk.onSnapshot(ordersQuery, (snapshot) => {
      onOrders(snapshot.docs.map(serializeDocument).sort(
        (first, second) => new Date(second.createdAt || 0) - new Date(first.createdAt || 0)
      ));
    }, onError);
  }

  async function getPublicOrder(id, phone) {
    const response = await fetch("/api/orders", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ action: "lookup", id: String(id), phone: String(phone) }),
      cache: "no-store"
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
      const error = new Error(result.error || "Không thể tra cứu đơn hàng.");
      error.code = result.code || "order-lookup-failed";
      throw error;
    }
    return result.order;
  }

  async function expireReservations() {
    const { auth } = await getFirebase();
    await auth.authStateReady();
    if (!auth.currentUser) throw new Error("Cần đăng nhập quản trị để dọn đơn hết hạn.");
    const token = await auth.currentUser.getIdToken();
    const response = await fetch("/api/orders?action=expire", {
      method: "GET",
      headers: { Authorization: `Bearer ${token}`, Accept: "application/json" },
      cache: "no-store"
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(result.error || "Không thể dọn đơn giữ hàng hết hạn.");
    return result.released || 0;
  }

  async function update(id, changes) {
    const { db, firestoreSdk } = await getFirebase();
    const orderRef = firestoreSdk.doc(db, "orders", String(id));
    if (!Object.prototype.hasOwnProperty.call(changes || {}, "status")) {
      await firestoreSdk.updateDoc(orderRef, changes);
      return;
    }

    await firestoreSdk.runTransaction(db, async (transaction) => {
      const orderSnapshot = await transaction.get(orderRef);
      if (!orderSnapshot.exists()) throw new Error("Không tìm thấy đơn hàng.");
      const order = orderSnapshot.data();
      const wasCancelled = order.status === "Đã hủy";
      const willBeCancelled = changes.status === "Đã hủy";
      const willBeDelivered = changes.status === "Đã giao";
      const shouldRestore = !wasCancelled && willBeCancelled && order.inventoryReserved === true;
      const shouldReserve = wasCancelled && !willBeCancelled && order.inventoryReserved === false;
      const items = aggregateOrderItems(order.items);
      const productRefs = shouldRestore || shouldReserve
        ? items.map((item) => firestoreSdk.doc(db, "products", String(item.id)))
        : [];
      const productSnapshots = [];
      for (const productRef of productRefs) productSnapshots.push(await transaction.get(productRef));

      if (shouldReserve && productSnapshots.some((snapshot, index) => (
        !snapshot.exists()
        || !Number.isInteger(Number(snapshot.data().quantity))
        || Number(snapshot.data().quantity) < Number(items[index].quantity)
      ))) {
        throw new Error("Không đủ tồn kho để khôi phục đơn đã hủy.");
      }
      productSnapshots.forEach((snapshot, index) => {
        if (!snapshot.exists()) return;
        const quantity = Number(snapshot.data().quantity) || 0;
        const orderQuantity = Math.max(1, Number(items[index].quantity) || 1);
        transaction.update(snapshot.ref, { quantity: quantity + (shouldRestore ? orderQuantity : -orderQuantity) });
      });

      transaction.update(orderRef, {
        ...changes,
        ...((shouldRestore || shouldReserve || (willBeDelivered && order.inventoryReserved === true))
          ? { inventoryReserved: shouldReserve && !willBeDelivered }
          : {}),
        ...(shouldReserve ? {
          reservationExpiresAt: firestoreSdk.Timestamp.fromDate(new Date(Date.now() + (
            order.paymentMethod === "qr" ? 30 * 60 * 1000 : 24 * 60 * 60 * 1000
          ))),
          reservationExpiredAt: firestoreSdk.deleteField()
        } : {}),
        ...(willBeDelivered ? { reservationExpiresAt: firestoreSdk.deleteField() } : {})
      });
    });
  }

  async function remove(id) {
    const { db, firestoreSdk } = await getFirebase();
    const orderRef = firestoreSdk.doc(db, "orders", String(id));
    await firestoreSdk.runTransaction(db, async (transaction) => {
      const orderSnapshot = await transaction.get(orderRef);
      if (!orderSnapshot.exists()) return;
      const order = orderSnapshot.data();
      const items = aggregateOrderItems(order.items);
      const productRefs = order.inventoryReserved === true
        ? items.map((item) => firestoreSdk.doc(db, "products", String(item.id)))
        : [];
      const productSnapshots = [];
      for (const productRef of productRefs) productSnapshots.push(await transaction.get(productRef));
      productSnapshots.forEach((snapshot, index) => {
        if (!snapshot.exists()) return;
        const quantity = Number(snapshot.data().quantity) || 0;
        const orderQuantity = Math.max(1, Number(items[index].quantity) || 1);
        transaction.update(snapshot.ref, { quantity: quantity + orderQuantity });
      });
      transaction.delete(orderRef);
    });
  }

  root.FlowerOrderStore = {
    create,
    quote,
    getPromotions,
    savePromotions,
    initializePromotions,
    getCategories,
    saveCategories,
    subscribeAdmin,
    subscribeAccount,
    getPublicOrder,
    expireReservations,
    update,
    remove
  };
})(typeof window !== "undefined" ? window : globalThis);

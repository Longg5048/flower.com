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

  async function create(order) {
    const { auth, db, firestoreSdk } = await getFirebase();
    await auth.authStateReady();
    const reference = await firestoreSdk.addDoc(firestoreSdk.collection(db, "orders"), {
      ...order,
      uid: auth.currentUser?.uid || "",
      createdAt: firestoreSdk.serverTimestamp()
    });
    return reference.id;
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
    const params = new URLSearchParams({ id: String(id), phone: String(phone) });
    const response = await fetch(`/api/orders?${params}`, {
      headers: { Accept: "application/json" },
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

  async function update(id, changes) {
    const { db, firestoreSdk } = await getFirebase();
    await firestoreSdk.updateDoc(firestoreSdk.doc(db, "orders", String(id)), changes);
  }

  async function remove(id) {
    const { db, firestoreSdk } = await getFirebase();
    await firestoreSdk.deleteDoc(firestoreSdk.doc(db, "orders", String(id)));
  }

  root.FlowerOrderStore = {
    create,
    subscribeAdmin,
    subscribeAccount,
    getPublicOrder,
    update,
    remove
  };
})(typeof window !== "undefined" ? window : globalThis);

(function initializeReviewStore(root) {
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

  function subscribe(productId, onReviews, onError = () => {}) {
    let unsubscribe = null;
    let cancelled = false;

    getFirebase().then(({ db, firestoreSdk }) => {
      if (cancelled) return;
      const reviewsQuery = firestoreSdk.query(
        firestoreSdk.collection(db, "reviews"),
        firestoreSdk.where("productIds", "array-contains", String(productId))
      );
      unsubscribe = firestoreSdk.onSnapshot(reviewsQuery, (snapshot) => {
        const reviews = snapshot.docs.map((doc) => ({ id: doc.id, ...doc.data() }));
        reviews.sort((first, second) => {
          const firstDate = first.createdAt?.toMillis?.() || 0;
          const secondDate = second.createdAt?.toMillis?.() || 0;
          return secondDate - firstDate;
        });
        onReviews(reviews);
      }, onError);
    }).catch(onError);

    return () => {
      cancelled = true;
      if (unsubscribe) unsubscribe();
    };
  }

  async function submit(review) {
    const { auth, db, firestoreSdk } = await getFirebase();
    await auth.authStateReady();
    if (!auth.currentUser) {
      throw new Error("Đăng nhập tài khoản khách hàng để gửi đánh giá công khai.");
    }

    const productIds = [...new Set((review.productIds || []).map(String).filter(Boolean))].slice(0, 10);
    if (!productIds.length) throw new Error("Đơn hàng không có thông tin sản phẩm để gắn đánh giá.");

    const result = await firestoreSdk.addDoc(firestoreSdk.collection(db, "reviews"), {
      productIds,
      productName: String(review.productName || "Sản phẩm hoa").slice(0, 500),
      customerName: String(review.customerName || "Khách hàng").slice(0, 80),
      rating: Math.max(1, Math.min(5, Math.round(Number(review.rating) || 0))),
      comment: String(review.comment || "").trim().slice(0, 1000),
      createdAt: firestoreSdk.serverTimestamp()
    });
    return result.id;
  }

  function serializeReview(doc) {
    const data = doc.data();
    return {
      id: doc.id,
      ...data,
      createdAt: data.createdAt?.toDate?.().toISOString() || data.createdAt || ""
    };
  }

  // Admin only (Security Rules): every review, newest first.
  function subscribeAll(onReviews, onError = () => {}) {
    let unsubscribe = null;
    let cancelled = false;

    getFirebase().then(({ db, firestoreSdk }) => {
      if (cancelled) return;
      unsubscribe = firestoreSdk.onSnapshot(firestoreSdk.collection(db, "reviews"), (snapshot) => {
        onReviews(snapshot.docs.map(serializeReview).sort(
          (first, second) => String(second.createdAt).localeCompare(String(first.createdAt))
        ));
      }, onError);
    }).catch(onError);

    return () => {
      cancelled = true;
      if (unsubscribe) unsubscribe();
    };
  }

  async function update(id, changes) {
    const { db, firestoreSdk } = await getFirebase();
    const reviewChanges = { ...changes };
    if (reviewChanges.completedAt === null) reviewChanges.completedAt = firestoreSdk.deleteField();
    await firestoreSdk.updateDoc(firestoreSdk.doc(db, "reviews", String(id)), reviewChanges);
  }

  async function remove(id) {
    const { db, firestoreSdk } = await getFirebase();
    await firestoreSdk.deleteDoc(firestoreSdk.doc(db, "reviews", String(id)));
  }

  root.FlowerReviewStore = { subscribe, submit, subscribeAll, update, remove };
})(typeof window !== "undefined" ? window : globalThis);

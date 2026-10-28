export const firebaseConfig = {
    apiKey: "AIzaSyD6EUQ729_NLNrXecZvmaD4oFhWkE_D_5g",
    authDomain: "web-ban-hoa-9fe0b.firebaseapp.com",
    projectId: "web-ban-hoa-9fe0b",
    storageBucket: "web-ban-hoa-9fe0b.firebasestorage.app",
    messagingSenderId: "842068724218",
    appId: "1:842068724218:web:51bfa70dde2eec0a957c43",
    measurementId: "G-3TREPTQ660"
};

export const isFirebaseConfigured = Object.values(firebaseConfig).every(
    (value) => typeof value === "string" && value.trim().length > 0
);

const connectedAuthEmulators = new WeakSet();
const connectedFirestoreEmulators = new WeakSet();

export function connectFirebaseEmulators(app, auth, firestore, authSdk, firestoreSdk) {
    const isLocalHost = typeof window !== "undefined"
        && ["localhost", "127.0.0.1"].includes(window.location.hostname);
    const useEmulators = isLocalHost
        && (window.sessionStorage.getItem("flowerflowE2e") === "true"
            || new URLSearchParams(window.location.search).has("e2e"));
    if (!useEmulators) return;

    if (auth && authSdk?.connectAuthEmulator && !connectedAuthEmulators.has(auth)) {
        try {
            authSdk.connectAuthEmulator(auth, "http://127.0.0.1:9099", { disableWarnings: true });
            connectedAuthEmulators.add(auth);
        } catch (error) {
            if (!String(error?.message || "").includes("already been initialized")) throw error;
            connectedAuthEmulators.add(auth);
        }
    }
    if (firestore && firestoreSdk?.connectFirestoreEmulator && !connectedFirestoreEmulators.has(firestore)) {
        try {
            firestoreSdk.connectFirestoreEmulator(firestore, "127.0.0.1", 8080);
            connectedFirestoreEmulators.add(firestore);
        } catch (error) {
            if (!String(error?.message || "").includes("already been initialized")) throw error;
            connectedFirestoreEmulators.add(firestore);
        }
    }
}

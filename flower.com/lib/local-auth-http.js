const { sessionCookie, sessionCookies, setSessionCookie } = require("./local-session");
const MESSAGES = {
    "invalid-credential": "Tên đăng nhập hoặc mật khẩu không chính xác.",
    "local-rate-limited": "Bạn đã gửi quá nhiều yêu cầu trong thời gian ngắn. Vui lòng chờ rồi thử lại.",
    "account-locked": "Bạn đã đăng nhập sai nhiều lần. Vui lòng chờ hết thời gian khóa.",
};

function createLocalAuthHandler(accounts) {
    const limit = require("./local-rate-limit").createLocalRateLimit();
    return async (action, req, res) => {
        res.setHeader("Cache-Control", "no-store");
        const cookie = sessionCookie(req);
        const setSession = (token = "") => setSessionCookie(req, res, token);
        const sendResult = (result) => {
            if (result.retryAfter) res.setHeader("Retry-After", String(result.retryAfter));
            if (result.status === 401 && result.code === "session-required") setSession();
            if (result.status !== 200) return res.status(result.status).json({ code: result.code,
                error: MESSAGES[result.code] || "Chưa xử lý được yêu cầu.",
                ...(Number.isInteger(result.attemptsRemaining) ? { attemptsRemaining: result.attemptsRemaining } : {}),
                ...(result.retryAfter ? { retryAfter: result.retryAfter } : {}) });
            if (result.sessionToken) setSession(result.sessionToken);
            return res.status(200).json({ ...(result.account ? { account: result.account } : {}) });
        };
        if (!["register", "login", "session", "logout"].includes(action)) {
            return res.status(404).json({ code: "not-found", error: "Không tìm thấy chức năng tài khoản." });
        }
        const method = action === "session" ? "GET" : "POST";
        if (req.method !== method) {
            res.setHeader("Allow", method);
            return res.status(405).json({ code: "method-not-allowed", error: "Phương thức không được hỗ trợ." });
        }
        try {
            if (!["session", "logout"].includes(action)) {
                const bucket = action === "register" ? "register" : "login";
                const retryAfter = limit(`${req.socket?.remoteAddress || "local"}:${bucket}`, bucket === "register" ? 5 : 60, bucket === "register" ? 600000 : 900000);
                if (retryAfter) return sendResult({ status: 429, code: "local-rate-limited", retryAfter });
            }
            if (action === "register") return sendResult({ status: 200, ...await accounts.register(req.body, sessionCookies(req)) });
            if (action === "login") return sendResult(await accounts.login(req.body, sessionCookies(req)));
            if (action === "logout") {
                await accounts.logout(sessionCookies(req));
                setSession();
                return res.status(200).json({ ok: true });
            }
            const account = await accounts.session(cookie);
            if (!account && cookie !== undefined) setSession();
            return account ? res.status(200).json({ account })
                : res.status(401).json({ code: "session-required", error: "Vui lòng đăng nhập lại." });
        } catch (error) {
            if (error.status === 429) { res.setHeader("Retry-After", "5"); return res.status(429).json({ code: error.code, error: error.message, retryAfter: 5 }); }
            if ([400, 409].includes(error.status)) return res.status(error.status).json({ code: error.code, error: error.message });
            return res.status(503).json({ code: "local-auth-unavailable", error: "Chưa xử lý được tài khoản. Vui lòng thử lại." });
        }
    };
}

module.exports = { createLocalAuthHandler };

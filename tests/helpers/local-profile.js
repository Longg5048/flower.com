module.exports = function localProfile(overrides = {}) {
    return { email: "ban@demo", password: "Mat-khau-thu-123", name: "Người thử", phone: "0900000000", ...overrides };
};

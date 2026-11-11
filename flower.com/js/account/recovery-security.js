export function restrictPinInputs() {
    document.querySelectorAll("input[data-pin]").forEach(input => {
        input.addEventListener("input", () => {
            const digits = input.value.replace(/[^0-9]/g, "").slice(0, 6);
            if (input.value !== digits) input.value = digits;
        });
    });
}

export function recoveryError(error) {
    const remaining = Number.isInteger(error.attemptsRemaining) && error.code !== "pin-reset-required"
        ? ` Bạn còn ${error.attemptsRemaining} lượt thử.` : "";
    const wait = error.retryAfter ? ` Thử lại sau ${Math.ceil(error.retryAfter / 60)} phút.` : "";
    return `${error.message}${remaining}${wait}`;
}

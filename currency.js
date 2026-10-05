const USD_TO_VND = 25949.123093;
const VND_FORMATTER = new Intl.NumberFormat("vi-VN", {
  style: "currency",
  currency: "VND",
  maximumFractionDigits: 0
});

function formatVnd(amountInUsd) {
  return VND_FORMATTER.format(Number(amountInUsd) * USD_TO_VND);
}

function toVndAmount(amountInUsd) {
  return Math.round(Number(amountInUsd) * USD_TO_VND);
}

function usdFromVnd(amountInVnd) {
  return Number((Number(amountInVnd) / USD_TO_VND).toFixed(2));
}
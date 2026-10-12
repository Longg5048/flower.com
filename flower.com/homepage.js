const menuToggle = document.querySelector(".menu-toggle")
const primaryNav = document.getElementById("primary-nav")

menuToggle.addEventListener("click", () => {
  const isOpen = menuToggle.getAttribute("aria-expanded") === "true"
  menuToggle.setAttribute("aria-expanded", String(!isOpen))
  menuToggle.querySelector(".sr-only").textContent = isOpen ? "Mở menu" : "Đóng menu"
  primaryNav.classList.toggle("is-open", !isOpen)
})

primaryNav.addEventListener("click", (event) => {
  if (!event.target.closest("a")) return
  primaryNav.classList.remove("is-open")
  menuToggle.setAttribute("aria-expanded", "false")
  menuToggle.querySelector(".sr-only").textContent = "Mở menu"
})

function updateCartCount() {
  const countElement = document.getElementById("cartTotal")
  try {
    const cart = JSON.parse(localStorage.getItem("cart") || "[]")
    countElement.textContent = String(cart.length)
  } catch (error) {
    console.error("Không thể đọc giỏ hàng:", error)
    countElement.textContent = "0"
  }
}

updateCartCount()
window.addEventListener("storage", (event) => {
  if (event.key === "cart") updateCartCount()
})

const currentYear = document.getElementById("current-year")
if (currentYear) currentYear.textContent = String(new Date().getFullYear())

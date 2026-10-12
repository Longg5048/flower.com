
let products = []

// Localstorage 
let cart = JSON.parse(localStorage.getItem('cart')) || []
let wish = JSON.parse(localStorage.getItem('wish')) || []

let Total = document.getElementById("cartTotal")
Total.innerText = cart.length
let WishTotal = document.getElementById("wishTotal")
if (WishTotal) WishTotal.innerText = wish.length

console.log(cart.length)
let container = document.getElementById("container")
let searchButton = document.getElementById("sbtn")
let searchInput = document.getElementById("search")


function display(data) {

    container.innerHTML = ""

    if (!data.length) {
      const emptyState = document.createElement("p")
      emptyState.className = "catalog-empty"
      emptyState.textContent = "Không tìm thấy sản phẩm phù hợp."
      container.append(emptyState)
      return
    }

    data.forEach((el) => {

        const productName = getVietnameseProductName(el.title || "Sản phẩm hoa")
        const box = document.createElement("article")
        const imageFrame = document.createElement("div")
        let img = document.createElement("img")
        let title = document.createElement("h2")
        let price = document.createElement("p")
        let stock = document.createElement("p")
        let actions = document.createElement("div")
        let detailsButton = document.createElement("a")
        let orderButton = document.createElement("button")
        let cartButton = document.createElement("button")
        let wishlistButton = document.createElement("button")
        const quantity = Math.max(0, Number(el.quantity) || 0)

        imageFrame.className = "product-image"
        img.src = el.image || ""
        img.alt = productName
        img.loading = "lazy"
        img.addEventListener("error", () => {
          img.hidden = true
          imageFrame.classList.add("image-unavailable")
          imageFrame.setAttribute("aria-label", `Ảnh ${productName} hiện không khả dụng`)
        }, { once: true })
        title.innerText = productName
        price.className = "product-price"
        price.innerText = formatVnd(el.price)
        stock.className = quantity > 0 ? "stock-status" : "stock-status out-of-stock"
        stock.innerText = quantity > 0 ? `Còn hàng: ${quantity}` : "Hết hàng"
        orderButton.type = "button"
        orderButton.className = "product-order"
        orderButton.innerText = "Đặt hàng"
        orderButton.disabled = quantity === 0
        cartButton.type = "button"
        cartButton.className = "product-add-to-cart"
        cartButton.innerText = "Thêm vào giỏ"
        cartButton.disabled = quantity === 0
        wishlistButton.type = "button"
        wishlistButton.className = "product-wishlist"
        wishlistButton.innerText = "Thêm vào yêu thích"
        detailsButton.className = "product-view-details"
        detailsButton.href = `./product.html?id=${encodeURIComponent(el.id)}`
        detailsButton.textContent = "Xem chi tiết"

        box.className = "box product-card"
        actions.className = "product-actions"

        cartButton.addEventListener("click", () => {
          if (checkdub2(el)) {
            Swal.fire({
              position: "center",
              icon: "error",
              title: "Sản phẩm đã có trong giỏ hàng",
              showConfirmButton: false,
              timer: 1500
            })
            return
          }

          addProductToCart(el)
          Swal.fire({
            position: "center",
            icon: "success",
            title: "Đã thêm vào giỏ hàng",
            showConfirmButton: false,
            timer: 1500
          })
        })

        orderButton.addEventListener("click", () => {
          if (!checkdub2(el)) addProductToCart(el)
          window.location.href = "./checkout.html"
        })

        wishlistButton.addEventListener("click", () => {
          if (checkdub(el)) {
            Swal.fire({
              position: "center",
              icon: "error",
              title: "Sản phẩm đã có trong danh sách yêu thích",
              showConfirmButton: false,
              timer: 1500
            })
            return
          }

          wish.push(el)
          localStorage.setItem("wish", JSON.stringify(wish))
          if (WishTotal) WishTotal.innerText = wish.length
          Swal.fire({
            position: "center",
            icon: "success",
            title: "Đã thêm vào danh sách yêu thích",
            showConfirmButton: false,
            timer: 1500
          })
        })

        imageFrame.append(img)
        actions.append(orderButton, cartButton, wishlistButton, detailsButton)
        box.append(imageFrame, title, price, stock, actions)
        container.append(box)
    });

}

function addProductToCart(product) {
  cart.push({ ...product, quantity: 1 })
  localStorage.setItem("cart", JSON.stringify(cart))
  Total.innerText = cart.length
}


const sortSelect = document.querySelector(".arrange")
const flowerTypeSelect = document.getElementById("flower-type")
const priceFilterSelect = document.getElementById("price-filter")
const detailPage = document.getElementById("product-detail")
const productPage = document.querySelector(".product-page")
const detailQuantity = document.getElementById("detail-quantity")
const detailQuantityError = document.getElementById("detail-quantity-error")
let selectedProduct = null

const flowerCategoryLabels = {
  rose: "Hoa hồng",
  orchid: "Hoa lan",
  lily: "Hoa ly",
  lavender: "Hoa oải hương",
  gerber: "Hoa đồng tiền",
  mixed: "Hoa tổng hợp"
}

function getProductCategory(product) {
  if (product.category) return product.category
  return flowerCategoryLabels[getFlowerTypes(product)[0]] || flowerCategoryLabels.mixed
}

function getProductImages(product) {
  return [...new Set([product.image, ...(Array.isArray(product.images) ? product.images : [])]
    .filter((image) => typeof image === "string" && image.trim()))]
}

function renderProductDetail(product) {
  const mainImage = document.getElementById("detail-main-image")
  const previousImage = selectedProduct && String(selectedProduct.id) === String(product.id)
    ? mainImage.getAttribute("src")
    : ""
  selectedProduct = product
  const productName = getVietnameseProductName(product.title || "Sản phẩm hoa")
  const images = getProductImages(product)
  const thumbnails = document.getElementById("detail-thumbnails")
  const stock = Math.max(0, Number(product.quantity) || 0)
  const selectedImageIndex = Math.max(0, images.indexOf(previousImage))

  productPage.classList.add("is-detail-view")
  detailPage.hidden = false
  document.getElementById("detail-breadcrumb-name").textContent = productName
  document.getElementById("detail-name").textContent = productName
  document.getElementById("detail-category").textContent = getProductCategory(product)
  document.getElementById("detail-price").textContent = formatVnd(product.price)
  document.getElementById("detail-stock").textContent = stock > 0 ? `Còn hàng · ${stock} sản phẩm` : "Hiện đã hết hàng"
  document.getElementById("detail-stock").classList.toggle("out-of-stock", stock === 0)
  document.getElementById("detail-description").textContent = product.description || "Sản phẩm hoa tươi được tuyển chọn và sắp xếp chỉn chu để gửi trao những lời yêu thương."

  detailQuantity.min = "1"
  detailQuantity.max = String(stock)
  detailQuantity.value = stock > 0 ? "1" : "0"
  detailQuantity.disabled = stock === 0
  detailQuantityError.textContent = ""
  thumbnails.replaceChildren()
  mainImage.hidden = images.length === 0
  mainImage.alt = productName
  mainImage.onerror = () => {
    mainImage.hidden = true
    mainImage.parentElement.classList.add("image-unavailable")
  }
  mainImage.parentElement.classList.toggle("image-unavailable", images.length === 0)
  mainImage.src = images[selectedImageIndex] || ""

  images.forEach((imageUrl, index) => {
    const thumbnail = document.createElement("button")
    const image = document.createElement("img")
    thumbnail.type = "button"
    thumbnail.className = `gallery-thumbnail${index === selectedImageIndex ? " is-active" : ""}`
    thumbnail.setAttribute("aria-label", `Xem ảnh ${index + 1} của ${productName}`)
    thumbnail.setAttribute("aria-pressed", String(index === selectedImageIndex))
    image.src = imageUrl
    image.alt = ""
    image.loading = "lazy"
    image.addEventListener("error", () => thumbnail.remove(), { once: true })
    thumbnail.append(image)
    thumbnail.addEventListener("click", () => {
      mainImage.src = imageUrl
      mainImage.hidden = false
      mainImage.parentElement.classList.remove("image-unavailable")
      thumbnails.querySelectorAll(".gallery-thumbnail").forEach((item) => {
        const isActive = item === thumbnail
        item.classList.toggle("is-active", isActive)
        item.setAttribute("aria-pressed", String(isActive))
      })
    })
    thumbnails.append(thumbnail)
  })

  const outOfStock = stock === 0
  document.getElementById("detail-add-to-cart").disabled = outOfStock
  document.getElementById("detail-buy-now").disabled = outOfStock
  document.title = `${productName} | Flowerflow`
}

function showQuantityError(text) {
  detailQuantityError.textContent = text
  detailQuantity.setCustomValidity(text)
}

function getRequestedQuantity() {
  const quantity = Number(detailQuantity.value)
  const stock = Math.max(0, Number(selectedProduct?.quantity) || 0)
  const currentCartQuantity = cart
    .filter((item) => String(item.id) === String(selectedProduct?.id))
    .reduce((sum, item) => sum + Math.max(0, Number(item.quantity) || 0), 0)

  if (!Number.isInteger(quantity) || quantity < 1) {
    showQuantityError("Số lượng phải là số nguyên lớn hơn 0.")
    return null
  }
  if (quantity + currentCartQuantity > stock) {
    const remainingStock = Math.max(0, stock - currentCartQuantity)
    showQuantityError(currentCartQuantity
      ? `Giỏ hàng đã có ${currentCartQuantity} sản phẩm. Chỉ còn ${remainingStock} sản phẩm có thể thêm.`
      : `Số lượng vượt quá tồn kho. Hiện có ${stock} sản phẩm.`)
    return null
  }

  showQuantityError("")
  return quantity
}

function addSelectedProductToCart(quantity) {
  const existingItem = cart.find((item) => String(item.id) === String(selectedProduct.id))
  if (existingItem) {
    existingItem.quantity = Math.max(0, Number(existingItem.quantity) || 0) + quantity
  } else {
    cart.push({ ...selectedProduct, quantity })
  }
  localStorage.setItem("cart", JSON.stringify(cart))
  Total.innerText = cart.length
}

function navigateToProductDetail() {
  const productId = new URLSearchParams(window.location.search).get("id")
  if (!productId) {
    detailPage.hidden = true
    productPage.classList.remove("is-detail-view")
    document.title = "Cửa hàng hoa | Flowerflow"
    return
  }
  const product = products.find((item) => String(item.id) === productId)
  if (!product) {
    detailPage.hidden = true
    productPage.classList.remove("is-detail-view")
    container.textContent = "Không tìm thấy sản phẩm. Hãy quay lại danh sách hoa."
    return
  }
  renderProductDetail(product)
}

detailQuantity.addEventListener("input", () => {
  detailQuantity.setCustomValidity("")
  detailQuantityError.textContent = ""
})

document.getElementById("detail-add-to-cart").addEventListener("click", () => {
  if (!selectedProduct) return
  const quantity = getRequestedQuantity()
  if (quantity === null) return
  addSelectedProductToCart(quantity)
  Swal.fire({
    position: "center",
    icon: "success",
    title: "Đã thêm vào giỏ hàng",
    showConfirmButton: false,
    timer: 1500
  })
})

document.getElementById("detail-buy-now").addEventListener("click", () => {
  if (!selectedProduct) return
  const quantity = getRequestedQuantity()
  if (quantity === null) return
  addSelectedProductToCart(quantity)
  window.location.assign("./checkout.html")
})

function normalizeSearchText(value) {
  return value.toLocaleLowerCase("vi").normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d")
}

function getFlowerTypes(product) {
  const productDetails = `${product.title} ${product.description}`.toLocaleLowerCase("en")
  const types = []
  const typeKeywords = {
    rose: /\broses?\b/,
    orchid: /\borchids?\b/,
    lily: /\blilies?\b|\blily\b/,
    lavender: /\blavender\b/,
    gerber: /\bgerbers?\b|\bdais(?:y|ies)\b/
  }

  Object.entries(typeKeywords).forEach(([type, keyword]) => {
    if (keyword.test(productDetails)) types.push(type)
  })

  return types.length ? types : ["mixed"]
}

function renderProducts() {
  const searchTerm = normalizeSearchText(searchInput.value.trim())
  const flowerType = flowerTypeSelect.value
  const priceFilter = priceFilterSelect.value
  const collator = new Intl.Collator("vi", { sensitivity: "base" })
  let visibleProducts = products.filter((product) => {
    const englishName = normalizeSearchText(product.title)
    const vietnameseName = normalizeSearchText(getVietnameseProductName(product.title))
    const matchesSearch = !searchTerm
      || englishName.includes(searchTerm)
      || vietnameseName.includes(searchTerm)
      || String(product.id).includes(searchTerm)
    const matchesFlowerType = !flowerType || getFlowerTypes(product).includes(flowerType)
    const priceInVnd = toVndAmount(product.price)
    let matchesPrice = true

    if (priceFilter === "price-under-1500000") {
      matchesPrice = priceInVnd < 1500000
    } else if (priceFilter === "price-1500000-2000000") {
      matchesPrice = priceInVnd >= 1500000 && priceInVnd < 2000000
    } else if (priceFilter === "price-over-2000000") {
      matchesPrice = priceInVnd >= 2000000
    }

    return matchesSearch && matchesFlowerType && matchesPrice
  })

  switch (sortSelect.value) {
    case "asc-p":
      visibleProducts.sort((first, second) => first.price - second.price)
      break
    case "dsc-p":
      visibleProducts.sort((first, second) => second.price - first.price)
      break
    case "str-i":
      visibleProducts.sort((first, second) => collator.compare(
        getVietnameseProductName(first.title),
        getVietnameseProductName(second.title)
      ))
      break
    case "str-d":
      visibleProducts.sort((first, second) => collator.compare(
        getVietnameseProductName(second.title),
        getVietnameseProductName(first.title)
      ))
      break
  }

  if (!visibleProducts.length) {
    container.textContent = "Không tìm thấy sản phẩm phù hợp."
    return
  }

  display(visibleProducts)
  navigateToProductDetail()
}

sortSelect.addEventListener("change", renderProducts)
flowerTypeSelect.addEventListener("change", renderProducts)
priceFilterSelect.addEventListener("change", renderProducts)
searchButton.addEventListener("click", renderProducts)
searchInput.addEventListener("input", renderProducts)
searchInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter") renderProducts()
})

window.addEventListener("storage", (event) => {
  if (event.key === "flowerInventory") {
    products = FlowerInventory.readProducts()
    renderProducts()
    navigateToProductDetail()
  }
})

window.addEventListener("flowerinventorychange", (event) => {
  products = event.detail || FlowerInventory.readProducts()
  renderProducts()
  navigateToProductDetail()
})

async function fetchAndRenderProducts() {
  try {
    products = await FlowerInventory.initialize()
    renderProducts()
    navigateToProductDetail()
  } catch (error) {
    console.error("Không thể tải danh sách hoa:", error)
    container.textContent = "Chưa thể tải danh sách sản phẩm. Vui lòng tải lại trang."
  }
}

fetchAndRenderProducts()
// console.log(data)









let order = document.querySelector(".orders");
order.onclick = () => {
  location.href = "./tracking.html";
};


  // let user = document.querySelector(".signin");
  // user.onclick = () => {
  //   location.href = "./login.html";
  // };
  // let order = document.querySelector(".orders");
  // order.onclick = () => {
  //   location.href = "./cart.html";
  // };
  // let shopnow = document.querySelector(".btn");
  // shopnow.onclick = () => {
  //   location.href = "./product.html";
  // };
  let bag = document.querySelector(".bag");
  bag.onclick = () => {
    location.href = "./cart.html";
  };


  function checkdub(data){
    for(let i=0;i<wish.length;i++){
        if(wish[i].id==data.id){
            return true
        }
    }
    return false
  }
  function checkdub2(data){
    for(let i=0;i<cart.length;i++){
        if(cart[i].id==data.id){
            return true
        }
    }
    return false
  }


































// let filterBy = document.getElementById("filter")
// function FilterData(data) {
//     filterBy.addEventListener("change", (event) => {
//         let res = filterBy.value;
//         console.log(res)
//         if (res == "") {
//             display(data)

//             console.log(data)
//         } else if (res == "Basket Of Wishes") {
//             data = data.filter((element) => {
//                 return element.title == res
//             })
//             display(data)
//         }
//         else if (res == "Vibrant Spring Basket") {
//             data = data.filter((element) => {
//                 return element.title == res
//             })
//             display(data)
//         }
//         // display(data)
//         //   fetch()
//         // FilterData()
//         // FilterData(data.flower)
//     })
// }



// function FilterData(data) {
//   let filterValue = filterBy.value
//   if (filterValue === "") {
//     display(data)
//     console.log(data)
//   } else {
//     data = data.filter((element) => {
//       // it will return boolean value
//       return element.title == filterValue
//     })
//     display(data)
//   }

// }

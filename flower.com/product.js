
let products = []

// Localstorage 
let cart = JSON.parse(localStorage.getItem('cart')) || []
let wish = JSON.parse(localStorage.getItem('wish')) || []

let Total = document.getElementById("cartTotal")
Total.innerText = cart.length
let WishTotal = document.getElementById("wishTotal")
WishTotal.innerText = wish.length

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
          WishTotal.innerText = wish.length
          Swal.fire({
            position: "center",
            icon: "success",
            title: "Đã thêm vào danh sách yêu thích",
            showConfirmButton: false,
            timer: 1500
          })
        })

        imageFrame.append(img)
        actions.append(orderButton, cartButton, wishlistButton)
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
  }
})

window.addEventListener("flowerinventorychange", (event) => {
  products = event.detail || FlowerInventory.readProducts()
  renderProducts()
})

async function fetchAndRenderProducts() {
  try {
    products = await FlowerInventory.initialize()
    renderProducts()
  } catch (error) {
    console.error("Không thể tải danh sách hoa:", error)
    container.textContent = "Chưa thể tải danh sách sản phẩm. Vui lòng tải lại trang."
  }
}

fetchAndRenderProducts()
// console.log(data)









let wishlistLink = document.querySelector(".wishlist");
wishlistLink.onclick = () => {
  location.href = "./wish.html";
};
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

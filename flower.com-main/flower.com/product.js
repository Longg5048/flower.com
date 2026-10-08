
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

    data.forEach((el, ind) => {

        // Creating 
        let box = document.createElement("div")
        let box2 = document.createElement("div")
        let box3 = document.createElement("div")
        let img = document.createElement("img")
        let title = document.createElement("h2")
        let price = document.createElement("h3")
        let stock = document.createElement("p")
        let btn = document.createElement("button")
        let btn2 = document.createElement("button")

        // Assigning Data
        img.src = el.image
        title.innerText = getVietnameseProductName(el.title)
        price.innerText = formatVnd(el.price)
        const quantity = Math.max(0, Number(el.quantity) || 0)
        stock.className = quantity > 0 ? "stock-status" : "stock-status out-of-stock"
        stock.innerText = quantity > 0 ? `Còn hàng: ${quantity}` : "Hết hàng"
        btn.innerText = quantity > 0 ? "Thêm vào giỏ hàng" : "Hết hàng"
        btn.disabled = quantity === 0
        btn2.innerText = "Thêm vào yêu thích"

        // Clssess
        box.className = "box";
        box3.id = "wish";

        //Event Listner

        btn.addEventListener("click", () => {
          if(checkdub2(el)){
            Swal.fire({
              position: 'center',
              icon: 'error',
              title: 'Sản phẩm đã có trong giỏ hàng',
              showConfirmButton: false,
              timer: 1500
            })
        }else{
            cart.push({...el,quantity:1})
            localStorage.setItem("cart", JSON.stringify(cart))
            Total.innerText=cart.length
            Swal.fire({
              position: 'center',
              icon: 'success',
              title: 'Đã thêm vào giỏ hàng',
              showConfirmButton: false,
              timer: 1500
            })
        } 
        })
        btn2.addEventListener("click", () => {

              if(checkdub(el)){
                  Swal.fire({
                    position: 'center',
                    icon: 'error',
                    title: 'Sản phẩm đã có trong danh sách yêu thích',
                    showConfirmButton: false,
                    timer: 1500
                  })
              }else{
                  wish.push(el)
                  localStorage.setItem("wish", JSON.stringify(wish))
                  WishTotal.innerText = wish.length
                  Swal.fire({
                    position: 'center',
                    icon: 'success',
                    title: 'Đã thêm vào danh sách yêu thích',
                    showConfirmButton: false,
                    timer: 1500
                  })
              } 
          })


        // Appending to Main 
        box2.append(img)
        box3.append(btn, btn2)
        box.append(box2, title, price, stock, box3)
        container.append(box)
    });

}


const sortSelect = document.querySelector(".arrange")
const filterSelect = document.getElementById("filter")

function renderProducts() {
  const searchTerm = searchInput.value.trim().toLocaleLowerCase("vi")
  const filterValue = filterSelect.value
  const collator = new Intl.Collator("vi", { sensitivity: "base" })
  let visibleProducts = products.filter((product) => {
    const englishName = product.title.toLocaleLowerCase("vi")
    const vietnameseName = getVietnameseProductName(product.title).toLocaleLowerCase("vi")
    const matchesSearch = !searchTerm || englishName.includes(searchTerm) || vietnameseName.includes(searchTerm)
    const priceInVnd = toVndAmount(product.price)
    let matchesFilter = true

    if (filterValue === "price-under-1500000") {
      matchesFilter = priceInVnd < 1500000
    } else if (filterValue === "price-1500000-2000000") {
      matchesFilter = priceInVnd >= 1500000 && priceInVnd < 2000000
    } else if (filterValue === "price-over-2000000") {
      matchesFilter = priceInVnd >= 2000000
    } else if (filterValue) {
      matchesFilter = product.title === filterValue
    }

    return matchesSearch && matchesFilter
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

  display(visibleProducts)
}

sortSelect.addEventListener("change", renderProducts)
filterSelect.addEventListener("change", renderProducts)
searchButton.addEventListener("click", renderProducts)
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

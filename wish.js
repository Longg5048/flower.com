
// Localstorage 
let cart=JSON.parse(localStorage.getItem('cart'))||[]
let wish=JSON.parse(localStorage.getItem('wish'))||[]
let Cart = JSON.parse(localStorage.getItem("cart")) || [];




// Fetching the data

let container=document.getElementById("container")
let wishTotal=document.getElementById("wishTotal")

display(wish)
function display(wish){
  wishTotal.innerText=wish.length
    container.innerHTML=""

  if(wish.length===0){
    container.innerHTML='<p class="empty-wishlist">Your wishlist is empty. <a href="./product.html">Browse flowers</a></p>'
    return
  }
   
    wish.forEach((el,ind) => {

        // Creating 
        let box=document.createElement("div")
        let box2=document.createElement("div")
        let box3=document.createElement("div")
        let img=document.createElement("img")
        let title=document.createElement("h2")
        let price=document.createElement("h3")
        let btn=document.createElement("button")
        let btn2=document.createElement("button")

        // Assigning Data
        img.src=el.image
        title.innerText=el.title
        price.innerText=`₹ ${el.price}`
        btn.innerText="Add to Cart"
        btn2.innerText="Remove"

        // Clssess
        box.className="box";
        box3.id="wish";

        //Event Listner

        btn.addEventListener("click",()=>{

          if(checkdub(el)){
              Swal.fire({
                position: 'center',
                icon: 'error',
                title: 'Product Already in Cart',
                showConfirmButton: false,
                timer: 1500
              })
          }else{

            Swal.fire({
              position: 'center',
              icon: 'success',
              title: 'Product Added To Cart',
              showConfirmButton: false,
              timer: 1500
            })

              cart.push({...el,quantity:1})
              localStorage.setItem("cart",JSON.stringify(cart))
              wish.splice(ind,1)
              localStorage.setItem("wish",JSON.stringify(wish))
              wishTotal.innerText=wish.length
              display(wish)
              
          } 
      })
      btn2.addEventListener("click",()=>{
            Swal.fire({
              position: 'center',
              icon: 'success',
              title: 'Removed',
              showConfirmButton: false,
              timer: 1500
            })
           wish.splice(ind,1)
            localStorage.setItem("wish",JSON.stringify(wish))
            wishTotal.innerText=wish.length
            display(wish)
    })


        // Appending to Main 
        box2.append(img)
        box3.append(btn,btn2)
        box.append(box2,title,price,box3)
        container.append(box)
    });
}
let Total=document.getElementById("cartTotal")
Total.innerText=cart.length


let user = document.querySelector(".signin");
user.onclick = () => {
  location.href = "./login.html";
};
let order = document.querySelector(".orders");
order.onclick = () => {
  location.href = "./cart.html";
};

let bag = document.querySelector(".bag");
bag.onclick = () => {
  location.href = "./cart.html";
};


function checkdub(data){
  for(i=0;i<cart.length;i++){
      if(cart[i].id==data.id){
          return true
      }
  }
  return false
}


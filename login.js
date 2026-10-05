const signUpButton = document.getElementById('signup');
const signInButton = document.getElementById('signin');
const container = document.getElementById('container');
const display = document.getElementById("display")
signUpButton.addEventListener('click', () => {
    container.classList.add("right-panel-active");
});

signInButton.addEventListener('click', () => {
    container.classList.remove("right-panel-active");
});
// ..................................................Sign In and Sign Up.......................................................
let signIn = document.querySelector("#signIn");
let signUp = document.querySelector("#signUp");
let pass = document.getElementById("checkpassword")
let pass1 = document.getElementById("password")
let users = JSON.parse(localStorage.getItem("usersData")) || [];

signUp.addEventListener("submit", function (event) {

    event.preventDefault();

    let count = 0;
    users.forEach(function (el) {
        if (el.email === signUp.email.value) {
            count++;
        }
    });

    if (count > 0) {
        alert("Email này đã được đăng ký!");
    }
    else {

        let user = {
            name: signUp.name.value,
            email: signUp.email.value,
            password: signUp.password.value,
        };

        users.push(user);




        localStorage.setItem("usersData", JSON.stringify(users));
        alert("Đăng ký thành công!");




        signUp.reset();

    }
});

signIn.addEventListener("submit", function (event) {
    event.preventDefault();
    let count = 0;
    // we are creating a temp var to store user who is trying to login
    let temp;
    // we are checking whether the user is registered or not
    users.forEach(function (el) {
        if (el.email === signIn.email1.value) {
            count++;
            // when count is incremented or we found user, we are storing the user data;
            temp = el;
            // we are storing the user in our local storage so that we can access user data from different pages
            localStorage.setItem('loggedUser', JSON.stringify(el));
        }
    });
    // if we didn't find any user we alert
    if (count == 0) {
        // alert("user not registered!");
        display.innerHTML = "Tài khoản chưa được đăng ký"
    }
    // if we find user we check the password
    else {
        // we check the temp password with form password
        if (temp.password != signIn.password1.value) {
            display.innerHTML = "Thông tin đăng nhập không chính xác"
        } else {
            display.innerHTML = "Đăng nhập thành công"
            // alert("login successful!");
            // we can use n number of methods to change the page. here we are using assign() method
            // window.location.assign('url')
            window.location.assign("./product.html");
        }
    }
});
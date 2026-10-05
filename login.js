const signUpButton = document.getElementById('signup');
const signInButton = document.getElementById('signin');
const container = document.getElementById('container');
const display = document.getElementById("display");
const adminLoginLink = document.getElementById("adminLoginLink");
const adminCredentials = {
    email: "admin@flower.com",
    password: "admin123"
};

if (signUpButton) {
    signUpButton.addEventListener('click', () => {
        container.classList.add("right-panel-active");
    });
}

if (signInButton) {
    signInButton.addEventListener('click', () => {
        container.classList.remove("right-panel-active");
    });
}

if (adminLoginLink) {
    adminLoginLink.addEventListener("click", (event) => {
        event.preventDefault();
        container.classList.remove("right-panel-active");
        document.getElementById("email1").value = adminCredentials.email;
        document.getElementById("password1").value = adminCredentials.password;
        display.innerHTML = "Tài khoản admin đã sẵn sàng";
    });
}

let signIn = document.querySelector("#signIn");
let signUp = document.querySelector("#signUp");
let users = JSON.parse(localStorage.getItem("usersData")) || [];

signUp.addEventListener("submit", function (event) {
    event.preventDefault();

    const submittedEmail = document.getElementById("email").value.trim().toLowerCase();
    const password = document.getElementById("password").value;
    const confirmPassword = document.getElementById("checkpassword").value;

    if (password !== confirmPassword) {
        alert("Mật khẩu xác nhận không khớp!");
        return;
    }

    let count = 0;
    users.forEach(function (el) {
        if ((el.email || "").toLowerCase() === submittedEmail) {
            count++;
        }
    });

    if (count > 0) {
        alert("Tài khoản này đã tồn tại!");
    } else {
        let user = {
            name: document.getElementById("name").value,
            email: submittedEmail,
            password: password,
            phone: document.getElementById("phone").value,
        };

        users.push(user);
        localStorage.setItem("usersData", JSON.stringify(users));
        alert("Đăng ký tài khoản thành công!");
        signUp.reset();
    }
});

signIn.addEventListener("submit", function (event) {
    event.preventDefault();

    const enteredEmail = document.getElementById("email1").value.trim().toLowerCase();
    const enteredPassword = document.getElementById("password1").value;

    if (enteredEmail === adminCredentials.email && enteredPassword === adminCredentials.password) {
        localStorage.setItem("loggedUser", JSON.stringify({
            name: "Admin",
            email: adminCredentials.email,
            password: adminCredentials.password,
            role: "admin"
        }));
        localStorage.setItem("loggedAdmin", "true");
        display.innerHTML = "Đăng nhập quản trị thành công";
        window.location.assign("./admin.html");
        return;
    }

    let count = 0;
    let temp;
    users.forEach(function (el) {
        if ((el.email || "").toLowerCase() === enteredEmail) {
            count++;
            temp = el;
            localStorage.setItem('loggedUser', JSON.stringify(el));
        }
    });

    if (count === 0) {
        display.innerHTML = "Tài khoản chưa đăng ký";
    } else if (temp.password !== enteredPassword) {
        display.innerHTML = "Sai thông tin đăng nhập";
    } else {
        display.innerHTML = "Đăng nhập thành công";
        window.location.assign("./product.html");
    }
});
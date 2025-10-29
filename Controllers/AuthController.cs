using Microsoft.AspNetCore.Mvc;
using WedNightFury.Models;
using System.Linq;

namespace WedNightFury.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Auth/Login
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Auth/Login
        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Vui lòng nhập tên đăng nhập và mật khẩu!";
                return View();
            }

            // So sánh không phân biệt hoa thường
            var user = _context.Users
                .FirstOrDefault(u => u.UserName.ToLower() == username.ToLower()
                                  && u.Password == password);

            if (user == null)
            {
                ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu!";
                return View();
            }

            // Lưu session
            HttpContext.Session.SetString("UserName", user.UserName);
            HttpContext.Session.SetString("Role", user.Role);

            // Điều hướng theo Role
            switch (user.Role.ToLower())
            {
                case "customer":
                    return RedirectToAction("Dashboard", "Customer");
                case "admin":
                    return RedirectToAction("Index", "Admin");
                case "employee":
                    return RedirectToAction("Index", "Employee");
                default:
                    return RedirectToAction("Index", "Home");
            }
        }

        // GET: /Auth/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Auth/Register
        [HttpPost]
        public IActionResult Register(User user)
        {
            if (ModelState.IsValid)
            {
                // Check username/email trùng
                if (_context.Users.Any(u => u.UserName == user.UserName || u.Email == user.Email))
                {
                    ViewBag.Error = "Tên đăng nhập hoặc Email đã tồn tại!";
                    return View(user);
                }

                user.Role = "customer"; // mặc định là khách hàng
                user.CreatedAt = DateTime.Now;

                _context.Users.Add(user);
                _context.SaveChanges();

                // Sau khi đăng ký -> về trang đăng nhập
                return RedirectToAction("Login", "Auth");
            }

            return View(user);
        }

        // GET: /Auth/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home"); // Sau khi logout thì về trang login
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using WedNightFury.Models;
using System.Linq;

namespace WedNightFury.Controllers
{
    [Route("User")]
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;

        public ProfileController(AppDbContext context)
        {
            _context = context;
        }

        // ========== [GET] /User/Profile ==========
        [HttpGet("Profile")]
        public IActionResult Index()
        {
            // 🔹 Lấy username trong session
            var username = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Auth"); // nếu chưa đăng nhập

            // 🔹 Lấy thông tin user
            var user = _context.Users.FirstOrDefault(u => u.UserName == username);
            if (user == null)
                return NotFound("Không tìm thấy người dùng.");

            // 🔹 Tìm profile tương ứng
            var profile = _context.Profiles.FirstOrDefault(p => p.UserId == user.Id);

            if (profile == null)
            {
                // Nếu chưa có thì tạo mới theo thông tin user
                profile = new Profile
                {
                    UserId = user.Id,
                    FullName = user.CompanyName ?? user.UserName,
                    Email = user.Email,
                    Phone = user.Phone,
                    City = "Chưa cập nhật",
                    District = "",
                    Ward = ""
                };

                _context.Profiles.Add(profile);
                _context.SaveChanges();
            }

            return View("Index", profile);
        }

        // ========== [POST] /User/Profile ==========
        [HttpPost("Profile")]
        [ValidateAntiForgeryToken]
        public IActionResult Index(Profile model)
        {
            if (!ModelState.IsValid)
                return View("Index", model);

            var profile = _context.Profiles.FirstOrDefault(p => p.Id == model.Id);
            if (profile == null)
                return NotFound();

            // ✅ Cập nhật dữ liệu
            profile.FullName = model.FullName;
            profile.Email = model.Email;
            profile.Phone = model.Phone;
            profile.BirthDate = model.BirthDate;
            profile.TaxCode = model.TaxCode;
            profile.Address = model.Address;
            profile.City = model.City;
            profile.District = model.District;
            profile.Ward = model.Ward;
            profile.CompanyName = model.CompanyName;

            _context.SaveChanges();
            ViewBag.Message = "✅ Cập nhật thông tin thành công!";
            return View("Index", profile);
        }
    }
}

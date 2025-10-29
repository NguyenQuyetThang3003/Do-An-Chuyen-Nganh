using Microsoft.AspNetCore.Mvc;
using WedNightFury.Models;
using System;
using System.Linq;

namespace WedNightFury.Controllers
{
    public class CustomerController : Controller
    {
        private readonly AppDbContext _context;

        public CustomerController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Dashboard(DateTime? fromDate, DateTime? toDate)
        {
            // 🔒 Kiểm tra đăng nhập (Session)
            var username = HttpContext.Session.GetString("UserName");
            var role = HttpContext.Session.GetString("Role");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(role) || role.ToLower() != "customer")
            {
                return RedirectToAction("Login", "Auth");
            }

            var orders = _context.Orders.AsQueryable();

            // ✅ Lọc theo ngày
            if (fromDate.HasValue && toDate.HasValue)
            {
                orders = orders.Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);
            }

            // ✅ Thống kê số lượng đơn
            ViewBag.SuccessCount = orders.Count(o => o.Status == "Giao thành công");
            ViewBag.FailCount = orders.Count(o => o.Status == "Đã hoàn");

            // ✅ Biểu đồ cột theo tháng
            ViewBag.BarData = orders
                .Where(o => o.CreatedAt != null)
                .GroupBy(o => o.CreatedAt.Value.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Success = g.Count(x => x.Status == "Giao thành công"),
                    Fail = g.Count(x => x.Status == "Đã hoàn")
                })
                .OrderBy(x => x.Month)
                .ToList();

            // ✅ Biểu đồ tròn
            if (_context.Orders.Any(o => o.Province != null))
            {
                ViewBag.PieData = orders
                    .GroupBy(o => o.Province ?? "Khác")
                    .Select(g => new { Province = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .Take(10)
                    .ToList();
            }
            else
            {
                ViewBag.PieData = orders
                    .GroupBy(o => o.Status ?? "Không rõ")
                    .Select(g => new { Province = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .Take(10)
                    .ToList();
            }

            // Truyền tên user cho View
            ViewBag.CustomerName = username;

            return View();
        }
    }
}

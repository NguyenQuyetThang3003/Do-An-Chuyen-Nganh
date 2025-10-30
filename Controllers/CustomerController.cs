using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
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
            // 🔒 Kiểm tra đăng nhập
            var username = HttpContext.Session.GetString("UserName");
            var role = HttpContext.Session.GetString("Role");
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(role) || role.ToLower() != "customer")
                return RedirectToAction("Login", "Auth");

            // 🔍 Lọc đơn hàng
            var orders = _context.Orders.AsQueryable();

            if (fromDate.HasValue && toDate.HasValue)
                orders = orders.Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);

            // ✅ Tổng quan
            ViewBag.SuccessCount = orders.Count(o => o.Status.ToLower() == "done");
            ViewBag.FailCount = orders.Count(o => o.Status.ToLower() == "cancelled");
            ViewBag.TotalOrders = orders.Count();

            // ✅ Biểu đồ cột theo tháng/năm
            var rawBarData = orders
                .Where(o => o.CreatedAt != null)
                .GroupBy(o => new { o.CreatedAt.Value.Year, o.CreatedAt.Value.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Success = g.Count(x => x.Status.ToLower() == "done"),
                    Fail = g.Count(x => x.Status.ToLower() == "cancelled"),
                    Shipping = g.Count(x => x.Status.ToLower() == "shipping"),
                    Pending = g.Count(x => x.Status.ToLower() == "pending")
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToList();

            ViewBag.BarData = rawBarData
                .Select(x => new
                {
                    Month = $"{x.Month:D2}/{x.Year}",
                    x.Success,
                    x.Fail,
                    x.Shipping,
                    x.Pending
                })
                .ToList();

            // ✅ Biểu đồ tròn: Top 10 theo tỉnh
            var pieByProvince = orders
                .Where(o => !string.IsNullOrEmpty(o.Province))
                .GroupBy(o => o.Province)
                .Select(g => new { Province = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(10)
                .ToList();

            if (pieByProvince.Any())
                ViewBag.PieData = pieByProvince;
            else
                ViewBag.PieData = orders
                    .GroupBy(o => o.Status ?? "Không rõ")
                    .Select(g => new { Province = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .ToList();

            // 👤 Thông tin chung
            ViewBag.CustomerName = username;
            ViewBag.LastUpdate = DateTime.Now.ToString("HH:mm");

            return View();
        }
    }
}

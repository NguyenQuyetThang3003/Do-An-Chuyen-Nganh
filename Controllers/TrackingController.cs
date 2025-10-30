using Microsoft.AspNetCore.Mvc;
using WedNightFury.Models;
using System.Linq;

namespace WedNightFury.Controllers
{
    public class TrackingController : Controller
    {
        private readonly AppDbContext _context;

        public TrackingController(AppDbContext context)
        {
            _context = context;
        }

        // ================== [GET] /Tracking/Index ==================
        [HttpGet]
        public IActionResult Index() => View();

        // ================== [POST] /Tracking/Find ==================
        [HttpPost]
        public IActionResult Find(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                ViewBag.Message = "⚠️ Vui lòng nhập mã đơn hoặc số điện thoại!";
                return View("Index");
            }

            var orders = _context.Orders
                .Where(o => o.Code == keyword || o.ReceiverPhone == keyword || o.SenderPhone == keyword)
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            if (!orders.Any())
            {
                ViewBag.Message = "❌ Không tìm thấy đơn hàng nào.";
                return View("Index");
            }

            return View("Result", orders);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using WedNightFury.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Linq;
using QRCoder;
using ZXing;
using ZXing.Common;
using System.Drawing;
using System.Drawing.Imaging;

namespace WedNightFury.Controllers
{
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;

        public OrderController(AppDbContext context)
        {
            _context = context;
        }

        // ================== [GET] /Order/Create ==================
        [HttpGet]
        public IActionResult Create() => View();

        // ================== [POST] /Order/Create ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Order model)
        {
            if (ModelState.IsValid)
            {
                var random = new Random();
                string orderCode = random.Next(100000000, int.MaxValue).ToString();

                // ✅ Sinh mã đơn hàng (Tracking Code)
                model.Code = $"NF-{DateTime.Now:yyyyMMddHHmmss}";

                model.CustomerId ??= 1;
                model.Status = "pending";
                model.CreatedAt = DateTime.Now;

                _context.Orders.Add(model);
                _context.SaveChanges();

                // 🧩 Tự động thêm người nhận mới
                if (!string.IsNullOrEmpty(model.ReceiverName))
                {
                    var existingReceiver = _context.Receivers
                        .FirstOrDefault(r => r.Phone == model.ReceiverPhone && r.Name == model.ReceiverName);

                    if (existingReceiver == null)
                    {
                        var receiver = new Receiver
                        {
                            Name = model.ReceiverName,
                            Phone = model.ReceiverPhone,
                            Address = model.ReceiverAddress,
                            SuccessRate = "100%",
                            CreatedAt = DateTime.Now
                        };
                        _context.Receivers.Add(receiver);
                        _context.SaveChanges();
                    }
                }

                TempData["OrderId"] = model.Id;
                TempData["OrderCode"] = orderCode;

                return RedirectToAction("Success");
            }

            return View(model);
        }

        // ================== [GET] /Order/Success ==================
        public IActionResult Success()
        {
            ViewBag.OrderId = TempData["OrderId"];
            ViewBag.OrderCode = TempData["OrderCode"];
            return View();
        }

        // ================== [GET] /Order/Manage ==================
        public IActionResult Manage(string status, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.Orders.AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "all")
                query = query.Where(o => o.Status == status);

            if (startDate.HasValue)
                query = query.Where(o => o.CreatedAt >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(o => o.CreatedAt <= endDate.Value);

            ViewBag.TotalOrders = _context.Orders.Count();
            ViewBag.PendingOrders = _context.Orders.Count(o => o.Status == "pending");
            ViewBag.ShippingOrders = _context.Orders.Count(o => o.Status == "shipping");
            ViewBag.DoneOrders = _context.Orders.Count(o => o.Status == "done");
            ViewBag.CancelledOrders = _context.Orders.Count(o => o.Status == "cancelled");

            var orders = query.OrderByDescending(o => o.CreatedAt).ToList();
            return View(orders);
        }

        // ================== [GET] /Order/Pending ==================
        [HttpGet]
        public IActionResult Pending(DateTime? startDate, DateTime? endDate)
        {
            var query = _context.Orders.AsQueryable()
                .Where(o => o.Status == "pending" || o.Status == "shipping");

            if (startDate.HasValue)
                query = query.Where(o => o.CreatedAt >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(o => o.CreatedAt <= endDate.Value);

            var orders = query.OrderByDescending(o => o.CreatedAt).ToList();

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return View(orders);
        }

        // ================== [POST] /Order/ChangeStatus ==================
        [HttpPost]
        public IActionResult ChangeStatus(int id, string newStatus)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound();

            order.Status = newStatus;
            _context.SaveChanges();

            TempData["Message"] = "✅ Cập nhật trạng thái thành công!";
            return RedirectToAction("Pending");
        }

        // ================== [GET] /Order/Details/{id} ==================
        public IActionResult Details(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound();

            return View(order);
        }

        // ================== [POST] /Order/UpdateStatus ==================
        [HttpPost]
        public IActionResult UpdateStatus(int id, string newStatus)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound();

            order.Status = newStatus;
            _context.SaveChanges();

            TempData["Message"] = "✅ Cập nhật trạng thái thành công!";
            return RedirectToAction("Manage");
        }

        // ================== [GET] /Order/Print/{id} ==================
        [HttpGet]
        public IActionResult Print(int id, string size = "A5")
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound("Không tìm thấy đơn hàng!");

            // --- Barcode ---
            var barcodeWriter = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions { Height = 70, Width = 300, Margin = 2 }
            };

            var pixelData = barcodeWriter.Write($"12300{order.Id}");
            byte[] barcodeBytes;
            using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb))
            {
                var bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, pixelData.Width, pixelData.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppRgb);

                System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bmpData.Scan0, pixelData.Pixels.Length);
                bitmap.UnlockBits(bmpData);

                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

                    barcodeBytes = ms.ToArray();
                }
            }

            // --- QR ---
            var qrGen = new QRCodeGenerator();
            var qrData = qrGen.CreateQrCode($"OrderID:{order.Id} - {order.ReceiverName}", QRCodeGenerator.ECCLevel.Q);
            var qrCode = new QRCode(qrData);
            byte[] qrBytes;
            using (var qrBmp = qrCode.GetGraphic(4))
            {
                using (var ms = new MemoryStream())
                {
                    qrBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

                    qrBytes = ms.ToArray();
                }
            }

            // --- PDF ---
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(15);
                    page.DefaultTextStyle(TextStyle.Default.FontSize(10));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Image(barcodeBytes);
                            col.Item().Text($"12300{order.Id}").AlignCenter();
                        });

                        row.RelativeItem(2).Column(col =>
                        {
                            col.Item().Text("PHIẾU GỬI").Bold().FontSize(16).AlignCenter();
                            col.Item().Text("BILL OF CONSIGNMENT").FontSize(11).AlignCenter();
                        });

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text("Night").FontSize(18).FontColor("#d32f2f").Bold().AlignRight();
                            col.Item().Text("Fury").FontSize(12).FontColor("#d32f2f").AlignRight();
                        });
                    });

                    page.Content().PaddingTop(10).Column(col =>
                    {
                        col.Item().Text($"Mã đơn: {order.Code}").Bold();
                        col.Item().Text($"Ngày gửi: {order.CreatedAt:dd/MM/yyyy HH:mm}");
                        col.Item().Text($"Trạng thái: {order.Status?.ToUpper()}");
                    });

                    page.Footer().AlignCenter()
                        .Text("Cảm ơn bạn đã sử dụng dịch vụ NightFury Express!")
                        .FontSize(9);
                });
            });

            using var stream = new MemoryStream();
            doc.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/pdf", $"Order_{order.Id}.pdf");
        }
    }
}

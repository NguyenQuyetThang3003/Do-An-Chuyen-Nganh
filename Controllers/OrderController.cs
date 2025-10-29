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

                model.CustomerId = model.CustomerId == 0 ? 1 : model.CustomerId;
                model.Status = "pending";
                model.CreatedAt = DateTime.Now;

                _context.Orders.Add(model);
                _context.SaveChanges();

                // ================================
                // 🧩 Thêm người nhận vào danh sách (nếu chưa có)
                // ================================
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

        // ================== [GET] /Order/Print/{id}?size=A5 ==================
        [HttpGet]
        public IActionResult Print(int id, string size = "A5")
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound("Không tìm thấy đơn hàng!");

            // ----- TẠO BARCODE -----
            var barcodeWriter = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = 70,
                    Width = 300,
                    Margin = 2
                }
            };

            var pixelData = barcodeWriter.Write($"12300{order.Id}");
            byte[] barcodeBytes;

            using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb))
            {
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, pixelData.Width, pixelData.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppRgb);

                System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
                bitmap.UnlockBits(bitmapData);

                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    barcodeBytes = ms.ToArray();
                }
            }

            // ----- TẠO QR CODE -----
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

            // ----- TẠO FILE PDF -----
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(15);
                    page.DefaultTextStyle(TextStyle.Default.FontSize(10));

                    // HEADER
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

                    // BODY
                    page.Content().PaddingTop(10).Column(col =>
                    {
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Border(1).Padding(4).Column(c =>
                            {
                                c.Item().Text("1. NGƯỜI GỬI").Bold();
                                c.Item().Text($"Họ tên: {order.SenderName}");
                                c.Item().Text($"Địa chỉ: {order.SenderAddress}");
                                c.Item().Text($"Điện thoại: {order.SenderPhone}");
                            });

                            r.RelativeItem().Border(1).Padding(4).Column(c =>
                            {
                                c.Item().Text("2. NGƯỜI NHẬN").Bold();
                                c.Item().Text($"Họ tên: {order.ReceiverName}");
                                c.Item().Text($"Địa chỉ: {order.ReceiverAddress}");
                                c.Item().Text($"Điện thoại: {order.ReceiverPhone}");
                                c.Item().Text($"Tỉnh/Thành phố: {order.Province}");
                            });
                        });

                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Border(1).Padding(4).Column(c =>
                            {
                                c.Item().Text("3. NỘI DUNG HÀNG HÓA").Bold();
                                c.Item().Text($"Tên hàng: {order.ProductName}");
                                c.Item().Text($"Trọng lượng: {order.Weight} gram");
                                c.Item().Text($"Giá trị: {order.Value:N0} VNĐ");
                                c.Item().Text($"Ghi chú: {order.Note ?? "Không có"}");
                            });

                            r.RelativeItem().Border(1).Padding(4).Column(c =>
                            {
                                c.Item().Text("4. DỊCH VỤ / GHI CHÚ").Bold();
                                c.Item().Text("• Giao hàng giờ hành chính");
                                c.Item().Text("• Cho khách xem hàng (nếu có)");
                            });
                        });

                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Border(1).Padding(4).Column(c =>
                            {
                                c.Item().Text("5. CƯỚC PHÍ").Bold();
                                c.Item().Text("Tổng cước: 23.000 đ");
                                c.Item().Text("Thanh toán: Người nhận");
                            });

                            r.ConstantItem(100).Border(1).Padding(4).Column(c =>
                            {
                                c.Item().Image(qrBytes);
                                c.Item().Text($"#{order.Id}").AlignCenter();
                            });
                        });

                        col.Item().Border(1).Padding(4).Column(c =>
                        {
                            c.Item().Text("6. NGÀY GỬI / NGƯỜI GỬI").Bold();
                            c.Item().Text($"Ngày gửi: {DateTime.Now:dd/MM/yyyy HH:mm}");
                        });
                    });

                    // FOOTER
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

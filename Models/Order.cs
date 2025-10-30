using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WedNightFury.Models
{
    [Table("orders")]
    public class Order
    {
        [Key]
        public int Id { get; set; }

        // Liên kết đến người dùng (Customer)
        [ForeignKey("User")]
        public int? CustomerId { get; set; }
        public virtual User? User { get; set; }

        // ✅ Mã đơn hàng để tracking / barcode
        [StringLength(50)]
        [Column("code")]
        public string? Code { get; set; }

        // ======= Người gửi =======
        [StringLength(100)]
        public string? SenderName { get; set; }

        [StringLength(20)]
        public string? SenderPhone { get; set; }

        [StringLength(200)]
        public string? SenderAddress { get; set; }

        // ======= Người nhận =======
        [StringLength(100)]
        public string? ReceiverName { get; set; }

        [StringLength(20)]
        public string? ReceiverPhone { get; set; }

        [StringLength(200)]
        public string? ReceiverAddress { get; set; }

        // ======= Thông tin hàng hoá =======
        [StringLength(200)]
        public string? ProductName { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Weight { get; set; }

        [Column(TypeName = "decimal(15,2)")]
        public decimal Value { get; set; }

        [StringLength(200)]
        public string? Note { get; set; }

        // ======= Quản lý =======
        [StringLength(20)]
        public string? Status { get; set; } = "pending";

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? Province { get; set; }
    }
}

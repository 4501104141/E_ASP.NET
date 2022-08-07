namespace Mood.EF2
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Sanpham")]
    public partial class Sanpham
    {
        [Key]
        public long IDContent { get; set; }
        
        [Required]
        [StringLength(250)]
        [Display(Name = "Tên sản phẩm")]
        public string Name { get; set; }

        [StringLength(250)]
        [Display(Name = "Thẻ SEO")]
        public string MetaTitle { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Thương hiệu")]
        public string ThuongHieu { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "nhà sản xuất")]
        public string NhaSanXuat { get; set; }

        [Display(Name = "Số lượng")]
        public int Soluong { get; set; }

        [Display(Name = "Hình ảnh")]
        public string Images { get; set; }

        [Display(Name = "Loại Sản Phẩm")]
        public long? CategoryID { get; set; }

        [StringLength(50)]
        [Display(Name = "Chất lượng")]
        public string Quanlity { get; set; }

        [Column(TypeName = "date")]
        public DateTime NgayTao { get; set; }

        [Display(Name = "Người Tạo")]
        public long? IDNguoiTao { get; set; }

        [Display(Name = "Trạng thái")]
        public bool Status { get; set; }

       
        public int Tophot { get; set; }

        [Display(Name = "Mô tả")]
        public string Mota { get; set; }

        [Display(Name = "Chi tiết")]
        public string ChiTiet { get; set; }

        [Display(Name = "Nhà Cung Cấp")]
        public long? IDNCC { get; set; }

        [Display(Name = "Giá Tiền")]
        public int GiaTien { get; set; }

        [Display(Name = "Giá Nhập")]
        public int? GiaNhap { get; set; }

        [Display(Name = "Giá Khuyến Mại")]
        public int? PriceSale { get; set; }

        [Display(Name = "Tồn Kho")]
        public int? TonKho { get; set; }
    }
}

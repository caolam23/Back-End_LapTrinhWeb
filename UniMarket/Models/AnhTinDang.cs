using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace UniMarket.Models
{
    public class AnhTinDang
    {
        [Key]
        public int MaAnh { get; set; }

        [Required]
        [DisplayName("Mã tin đăng")]
        public int MaTinDang { get; set; }

        [Required(ErrorMessage = "Đường dẫn ảnh không được để trống.")]
        [StringLength(255, ErrorMessage = "Đường dẫn ảnh không được vượt quá 255 ký tự.")]
        [DisplayName("Đường dẫn ảnh")]
        public string DuongDan { get; set; }

        [ForeignKey("MaTinDang")]
        public TinDang? TinDang { get; set; }
    }
}

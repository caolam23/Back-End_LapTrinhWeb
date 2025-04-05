using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniMarket.DataAccess;
using UniMarket.Models;
using Microsoft.AspNetCore.Http;
using System.IO;
using UniMarket.DTO;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace UniMarket.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TinDangController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly string _imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "categories");

        public TinDangController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: api/tindang/get-posts
        [HttpGet("get-posts")]
        public IActionResult GetPosts()
        {
            var posts = _context.TinDangs
                .Include(p => p.NguoiBan)
                .Include(p => p.TinhThanh) // Bao gồm thông tin tỉnh thành
                .Include(p => p.QuanHuyen) // Bao gồm thông tin quận huyện
                .Select(p => new
                {
                    p.MaTinDang,
                    p.TieuDe,
                    p.TrangThai,
                    NguoiBan = p.NguoiBan.FullName,
                    p.NgayDang,
                    TinhThanh = p.TinhThanh.TenTinhThanh, // Lấy tên tỉnh thành
                    QuanHuyen = p.QuanHuyen.TenQuanHuyen // Lấy tên quận huyện
                })
                .ToList();

            if (posts == null || !posts.Any())
            {
                return NotFound("Không có tin đăng nào.");
            }

            return Ok(posts);
        }
        [HttpPost("add-post")]
        public async Task<IActionResult> AddPost(
    [FromForm] string title,
    [FromForm] string description,
    [FromForm] decimal price,
    [FromForm] string contactInfo,
    [FromForm] string condition,
    [FromForm] int province,
    [FromForm] int district,
    [FromForm] IFormFile image,
    [FromForm] string userId,
    [FromForm] int categoryId,  // Nhận categoryId từ frontend
    [FromForm] string categoryName, // Nhận categoryName từ frontend
    [FromForm] bool canNegotiate) // Nhận trường "Có thể thương lượng"
        {
            // Kiểm tra xem người bán có tồn tại không
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return BadRequest("Người bán không tồn tại!");
            }

            // Kiểm tra xem tỉnh và quận có tồn tại không
            var provinceExists = await _context.TinhThanhs.AnyAsync(t => t.MaTinhThanh == province);
            if (!provinceExists)
            {
                return BadRequest("Tỉnh thành không hợp lệ!");
            }

            var districtExists = await _context.QuanHuyens.AnyAsync(q => q.MaQuanHuyen == district);
            if (!districtExists)
            {
                return BadRequest("Quận huyện không hợp lệ!");
            }

            // Tạo bài đăng
            var post = new TinDang
            {
                TieuDe = title,
                MoTa = description,
                Gia = price,
                CoTheThoaThuan = canNegotiate, // Gán giá trị "Có thể thương lượng"
                TinhTrang = condition,
                DiaChi = contactInfo,
                MaTinhThanh = province,
                MaQuanHuyen = district,
                MaNguoiBan = userId, // Gắn mã người bán
                NgayDang = DateTime.Now,
                TrangThai = TrangThaiTinDang.ChoDuyet, // Mặc định là Chờ duyệt
                MaDanhMuc = categoryId // Gắn mã danh mục
            };

            // Lưu ảnh nếu có
            if (image != null)
            {
                string uploadPath = Path.Combine("wwwroot", "images", "Posts");

                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                var filePath = Path.Combine(uploadPath, image.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                var postImage = new AnhTinDang
                {
                    DuongDan = $"/images/Posts/{image.FileName}",
                    TinDang = post
                };

                post.AnhTinDangs = new List<AnhTinDang> { postImage };
            }

            // Lưu bài đăng vào cơ sở dữ liệu
            _context.TinDangs.Add(post);
            await _context.SaveChangesAsync();

            // Trả về MaTinDang của bài đăng vừa tạo
            return Ok(new { MaTinDang = post.MaTinDang });
        }


        // PUT: api/tindang/{id} (Cập nhật tin đăng)
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTinDang(int id, [FromBody] TinDang tinDang)
        {
            if (id != tinDang.MaTinDang)
            {
                return BadRequest(new { message = "Mã tin đăng không khớp" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingTinDang = await _context.TinDangs.FindAsync(id);
            if (existingTinDang == null)
            {
                return NotFound(new { message = "Không tìm thấy tin đăng" });
            }

            existingTinDang.TieuDe = tinDang.TieuDe;
            existingTinDang.MoTa = tinDang.MoTa;
            existingTinDang.Gia = tinDang.Gia;
            existingTinDang.CoTheThoaThuan = tinDang.CoTheThoaThuan;
            existingTinDang.TinhTrang = tinDang.TinhTrang;
            existingTinDang.DiaChi = tinDang.DiaChi;
            existingTinDang.TrangThai = tinDang.TrangThai;
            existingTinDang.NgayCapNhat = DateTime.Now;

            _context.Entry(existingTinDang).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, new { message = "Lỗi khi cập nhật tin đăng" });
            }

            return NoContent();
        }

        // DELETE: api/tindang/{id} (Xóa tin đăng)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTinDang(int id)
        {
            var tinDang = await _context.TinDangs.FindAsync(id);
            if (tinDang == null)
            {
                return NotFound(new { message = "Không tìm thấy tin đăng" });
            }

            _context.TinDangs.Remove(tinDang);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa tin đăng thành công" });
        }

        // Cấu hình lại route API backend để đảm bảo nó nhận yêu cầu từ frontend
        [HttpGet("get-post/{id}")]
        public IActionResult GetPost(int id)
        {
            var post = _context.TinDangs
                .Include(p => p.NguoiBan)
                .Include(p => p.TinhThanh)
                .Include(p => p.QuanHuyen)
                .Where(p => p.MaTinDang == id) // Lọc theo MaTinDang
                .Select(p => new
                {
                    p.MaTinDang,
                    p.MaDanhMuc,
                    p.TieuDe,
                    p.MoTa,
                    p.Gia,
                    p.TinhTrang,
                    p.DiaChi,
                    TinhThanh = p.TinhThanh.TenTinhThanh,
                    QuanHuyen = p.QuanHuyen.TenQuanHuyen,
                    p.NgayDang,
                    p.CoTheThoaThuan,
                    p.MaNguoiBan
                })
                .FirstOrDefault();

            if (post == null)
            {
                return NotFound("Không tìm thấy tin đăng.");
            }

            return Ok(post);
        }










        // GET: api/tindang/tinhthanh
        [HttpGet("tinhthanh")]
        public async Task<ActionResult<IEnumerable<TinhThanhDTO>>> GetTinhThanhs()
        {
            var tinhThanhs = await _context.TinhThanhs
                .Include(tt => tt.QuanHuyens)  // Load danh sách quận/huyện
                .Select(tt => new TinhThanhDTO
                {
                    MaTinhThanh = tt.MaTinhThanh,
                    TenTinhThanh = tt.TenTinhThanh,
                    QuanHuyens = tt.QuanHuyens.Select(qh => new QuanHuyenDTO
                    {
                        MaQuanHuyen = qh.MaQuanHuyen,
                        TenQuanHuyen = qh.TenQuanHuyen
                    }).ToList()
                })
                .ToListAsync();

            if (!tinhThanhs.Any())
            {
                return NotFound(new { message = "Không có tỉnh thành nào trong cơ sở dữ liệu" });
            }

            return Ok(tinhThanhs);
        }

        // GET: api/tindang/tinhthanh/{maTinhThanh}/quanhuynh
        [HttpGet("tinhthanh/{maTinhThanh}/quanhuynh")]
        public async Task<ActionResult<IEnumerable<QuanHuyenDTO>>> GetQuanHuyensByTinhThanh(int maTinhThanh)
        {
            var quanHuyens = await _context.QuanHuyens
                .Where(qh => qh.MaTinhThanh == maTinhThanh)
                .Select(qh => new QuanHuyenDTO
                {
                    MaQuanHuyen = qh.MaQuanHuyen,
                    TenQuanHuyen = qh.TenQuanHuyen
                })
                .ToListAsync();

            if (!quanHuyens.Any())
            {
                return NotFound(new { message = "Không tìm thấy quận/huyện cho tỉnh/thành này." });
            }

            return Ok(quanHuyens);
        }
    }
}

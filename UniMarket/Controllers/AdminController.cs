﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniMarket.Models;
using Microsoft.AspNetCore.Cors;
using System.Linq;
using System.Threading.Tasks;
using static AuthController;
using UniMarket.DataAccess;

namespace UniMarket.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [EnableCors("_myAllowSpecificOrigins")] // Áp dụng CORS cho controller này
    public class AdminController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context; // ✅ Định nghĩa biến _context

        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context) // 🔥 Thêm ApplicationDbContext vào DI
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context; // ✅ Gán _context
        }


        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var userList = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                bool isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTime.Now;

                userList.Add(new
                {
                    user.Id,
                    FullName = user.FullName ?? "Không có",
                    user.UserName,
                    user.Email,
                    user.PhoneNumber,
                    Role = roles.Any() ? string.Join(", ", roles) : "Chưa có",
                    isLocked = isLocked // Trả về trạng thái khóa
                });
            }

            return Ok(userList);
        }

        [HttpPost("add-employee")]
        public async Task<IActionResult> AddEmployee([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true,
                FullName = model.FullName,  // Thêm dòng này
                PhoneNumber = model.PhoneNumber  // Thêm dòng này
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, SD.Role_Employee);
            return Ok(new { message = "Nhân viên đã được thêm thành công!" });
        }

        [HttpDelete("delete-user/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound("Không tìm thấy người dùng!");

            var isAdmin = await _userManager.IsInRoleAsync(user, SD.Role_Admin);
            if (isAdmin)
                return BadRequest("Không thể xóa tài khoản Admin!");

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok("Xóa người dùng thành công!");
        }

        [HttpPost("toggle-lock/{id}")]
        public async Task<IActionResult> ToggleUserLock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound("Không tìm thấy người dùng!");

            bool isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTime.Now;

            if (isLocked)
            {
                user.LockoutEnd = null;
            }
            else
            {
                user.LockoutEnd = DateTime.Now.AddYears(100);
            }

            await _userManager.UpdateAsync(user);
            await _userManager.UpdateSecurityStampAsync(user);

            return Ok(new
            {
                message = isLocked ? "Tài khoản đã được mở khóa!" : "Tài khoản đã bị khóa!",
                isLocked = !isLocked
            });
        }

        [HttpPost("change-role")]
        public async Task<IActionResult> ChangeUserRole([FromBody] ChangeRoleModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
                return NotFound("Không tìm thấy người dùng!");

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            await _userManager.AddToRoleAsync(user, model.NewRole);

            return Ok(new { message = $"Vai trò của {user.Email} đã được thay đổi thành {model.NewRole}." });
        }

        /// <summary>
        /// ✅ API gộp thêm nhân viên mới hoặc cập nhật vai trò
        /// </summary>
        [HttpPost("add-or-update-employee")]
        public async Task<IActionResult> AddOrUpdateEmployee([FromBody] EmployeeRoleModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var roleExists = await _roleManager.RoleExistsAsync(model.Role);
            if (!roleExists)
                return BadRequest("Vai trò không hợp lệ!");

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    EmailConfirmed = true,
                    FullName = model.FullName,
                    PhoneNumber = model.PhoneNumber
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                    return BadRequest(result.Errors);
            }
            else
            {
                user.FullName = model.FullName;
                user.PhoneNumber = model.PhoneNumber;
                await _userManager.UpdateAsync(user);
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, model.Role);

            return Ok(new { message = $"Nhân viên {user.Email} đã được cập nhật với vai trò {model.Role}." });
        }

        [HttpGet("employees")]
        public async Task<IActionResult> GetEmployees()
        {
            var users = await _userManager.Users.ToListAsync();
            var employeeList = new List<object>();
            int count = 1; // Tạo mã NV001, NV002...

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Employee"))
                {
                    bool isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTime.Now;
                    employeeList.Add(new
                    {
                        EmployeeCode = $"NV{count:D3}", // Mã NV001, NV002,...
                        UserId = user.Id, // ID thực tế để gửi API
                        FullName = user.FullName ?? "Không có",
                        user.Email,
                        user.PhoneNumber,
                        Role = roles.FirstOrDefault() ?? "Chưa có",
                        isLocked
                    });
                    count++;
                }
            }
            return Ok(employeeList);
        }

        [HttpGet("get-parent-categories")]
        public async Task<IActionResult> GetParentCategories()
        {
            var parentCategories = await _context.DanhMucChas
                .Select(d => new
                {
                    d.MaDanhMucCha,
                    d.TenDanhMucCha,
                    d.AnhDanhMucCha,
                    d.Icon // Thêm icon vào query
                })
                .ToListAsync();

            var baseUrl = $"{Request.Scheme}://{Request.Host}"; // Lấy base URL của server

            var result = parentCategories.Select(c => new
            {
                c.MaDanhMucCha,
                c.TenDanhMucCha,
                AnhDanhMucCha = string.IsNullOrEmpty(c.AnhDanhMucCha)
                    ? $"{baseUrl}/uploads/default-image.jpg"
                    : (c.AnhDanhMucCha.StartsWith("http") ? c.AnhDanhMucCha : $"{baseUrl}/{c.AnhDanhMucCha}"),
                Icon = string.IsNullOrEmpty(c.Icon)
                    ? $"{baseUrl}/uploads/default-icon.jpg" // Nếu không có icon, dùng icon mặc định
                    : (c.Icon.StartsWith("http") ? c.Icon : $"{baseUrl}/{c.Icon}")
            }).ToList();

            return Ok(result);
        }


        [HttpPost("add-category")]
        public async Task<IActionResult> AddCategory(
        [FromForm] string tenDanhMuc,
        [FromForm] int maDanhMucCha)
        {
            if (maDanhMucCha == 0)
            {
                return BadRequest("Danh mục con bắt buộc phải có danh mục cha!");
            }

            var parentCategory = await _context.DanhMucChas.FindAsync(maDanhMucCha);
            if (parentCategory == null)
            {
                return BadRequest("Mã danh mục cha không hợp lệ!");
            }

            // **Lưu vào Database**
            var danhMucMoi = new DanhMuc
            {
                TenDanhMuc = tenDanhMuc,
                MaDanhMucCha = maDanhMucCha
            };

            _context.DanhMucs.Add(danhMucMoi);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Danh mục đã được thêm thành công!" });
        }



        [HttpPost("add-parent-category")]
        public async Task<IActionResult> AddParentCategory(
    [FromForm] string tenDanhMucCha,
    [FromForm] IFormFile? anhDanhMucCha,
    [FromForm] IFormFile? icon)
        {
            if (string.IsNullOrWhiteSpace(tenDanhMucCha))
                return BadRequest("Tên danh mục không được để trống!");

            bool exists = await _context.DanhMucChas.AnyAsync(d => d.TenDanhMucCha == tenDanhMucCha);
            if (exists)
                return BadRequest("Danh mục cha đã tồn tại!");

            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/categories");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string? imageUrl = null;
            if (anhDanhMucCha != null)
            {
                var imageFileName = $"{Guid.NewGuid()}_{Path.GetFileName(anhDanhMucCha.FileName)}";
                var imagePath = Path.Combine(folderPath, imageFileName);

                using (var stream = new FileStream(imagePath, FileMode.Create))
                {
                    await anhDanhMucCha.CopyToAsync(stream);
                }

                imageUrl = $"/images/categories/{imageFileName}";
            }

            string? iconUrl = null;
            if (icon != null)
            {
                var iconFileName = $"{Guid.NewGuid()}_{Path.GetFileName(icon.FileName)}";
                var iconPath = Path.Combine(folderPath, iconFileName);

                using (var stream = new FileStream(iconPath, FileMode.Create))
                {
                    await icon.CopyToAsync(stream);
                }

                iconUrl = $"/images/categories/{iconFileName}";
            }

            var newCategory = new DanhMucCha
            {
                TenDanhMucCha = tenDanhMucCha,
                AnhDanhMucCha = imageUrl,
                Icon = iconUrl
            };

            _context.DanhMucChas.Add(newCategory);
            await _context.SaveChangesAsync();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            return Ok(new
            {
                Message = "Thêm danh mục cha thành công!",
                AnhDanhMucCha = imageUrl != null ? $"{baseUrl}{imageUrl}" : null,
                Icon = iconUrl != null ? $"{baseUrl}{iconUrl}" : null
            });
        }
        //hàm update danh mục con
        [HttpPut("update-category/{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TenDanhMuc))
            {
                return BadRequest("Thông tin danh mục không hợp lệ.");
            }

            var danhMuc = await _context.DanhMucs.FindAsync(id);
            if (danhMuc == null)
            {
                return NotFound("Danh mục không tồn tại.");
            }

            danhMuc.TenDanhMuc = model.TenDanhMuc;
            danhMuc.MaDanhMucCha = model.DanhMucChaId;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật danh mục thành công!" });
        }
        // xóa danh mục con
        [HttpDelete("delete-category/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.DanhMucs.FindAsync(id);
            if (category == null)
            {
                return NotFound(new { message = "Danh mục không tồn tại" });
            }

            // Kiểm tra xem danh mục con có chứa sản phẩm hoặc danh mục con khác không
            bool hasSubCategories = await _context.DanhMucs.AnyAsync(d => d.MaDanhMucCha == id);
            if (hasSubCategories)
            {
                return BadRequest(new { message = "Không thể xóa danh mục này vì có danh mục con liên quan!" });
            }

            _context.DanhMucs.Remove(category);

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Xóa danh mục thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server khi xóa danh mục!", error = ex.Message });
            }
        }
        // lấy danh sách danh mục con
        [HttpGet("get-categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.DanhMucs
                .Select(dm => new {
                    dm.MaDanhMuc,
                    dm.TenDanhMuc,
                    dm.MaDanhMucCha
                })
                .ToListAsync();

            return Ok(categories);
        }
        // cập nhập danh mục cha 
        [HttpPut("update-parent-category/{id}")]
        public async Task<IActionResult> UpdateParentCategory(int id, [FromForm] string tenDanhMucCha, [FromForm] IFormFile? anhDanhMuc, [FromForm] IFormFile? icon)
        {
            var category = await _context.DanhMucChas.FindAsync(id);
            if (category == null)
            {
                return NotFound(new { message = "Danh mục cha không tồn tại!" });
            }

            if (string.IsNullOrWhiteSpace(tenDanhMucCha))
            {
                return BadRequest(new { message = "Tên danh mục không được để trống!" });
            }

            category.TenDanhMucCha = tenDanhMucCha;

            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/categories");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string baseUrl = $"{Request.Scheme}://{Request.Host}";

            // Xử lý cập nhật ảnh
            if (anhDanhMuc != null)
            {
                string imageFileName = $"{Guid.NewGuid()}_{Path.GetFileName(anhDanhMuc.FileName)}";
                string imagePath = Path.Combine(folderPath, imageFileName);

                using (var stream = new FileStream(imagePath, FileMode.Create))
                {
                    await anhDanhMuc.CopyToAsync(stream);
                }

                category.AnhDanhMucCha = $"/images/categories/{imageFileName}";
            }

            // Xử lý cập nhật icon
            if (icon != null)
            {
                string iconFileName = $"{Guid.NewGuid()}_{Path.GetFileName(icon.FileName)}";
                string iconPath = Path.Combine(folderPath, iconFileName);

                using (var stream = new FileStream(iconPath, FileMode.Create))
                {
                    await icon.CopyToAsync(stream);
                }

                category.Icon = $"/images/categories/{iconFileName}";
            }

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new
                {
                    message = "Cập nhật danh mục cha thành công!",
                    AnhDanhMucCha = category.AnhDanhMucCha != null ? $"{baseUrl}{category.AnhDanhMucCha}" : null,
                    Icon = category.Icon != null ? $"{baseUrl}{category.Icon}" : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server khi cập nhật danh mục!", error = ex.Message });
            }
        }
        // xóa danh mục cha 
        [HttpDelete("delete-parent-category/{id}")]
        public async Task<IActionResult> DeleteParentCategory(int id)
        {
            var category = await _context.DanhMucChas.FindAsync(id);
            if (category == null)
            {
                return NotFound(new { message = "Danh mục cha không tồn tại!" });
            }

            // Kiểm tra xem danh mục cha có danh mục con không
            bool hasSubCategories = await _context.DanhMucs.AnyAsync(d => d.MaDanhMucCha == id);
            if (hasSubCategories)
            {
                return BadRequest(new { message = "Không thể xóa danh mục cha vì có danh mục con liên quan!" });
            }

            // Xóa ảnh và icon khỏi thư mục lưu trữ nếu có
            if (!string.IsNullOrEmpty(category.AnhDanhMucCha))
            {
                string imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", category.AnhDanhMucCha.TrimStart('/'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }
            }

            if (!string.IsNullOrEmpty(category.Icon))
            {
                string iconPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", category.Icon.TrimStart('/'));
                if (System.IO.File.Exists(iconPath))
                {
                    System.IO.File.Delete(iconPath);
                }
            }

            _context.DanhMucChas.Remove(category);

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Xóa danh mục cha thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server khi xóa danh mục!", error = ex.Message });
            }
        }
        public class UpdateCategoryModel
        {
            public string TenDanhMuc { get; set; }
            public int DanhMucChaId { get; set; }
        }
        // duyệt tin đăng 
        [HttpPost]
        public async Task<IActionResult> DuyetTinDang(int maTinDang)
        {
            var tinDang = await _context.TinDangs.FindAsync(maTinDang);
            if (tinDang == null)
            {
                return NotFound("Tin đăng không tồn tại.");
            }

            // Thay đổi trạng thái thành "Đã Duyệt"
            tinDang.TrangThai = TrangThaiTinDang.DaDuyet;

            // Lưu thay đổi vào cơ sở dữ liệu
            await _context.SaveChangesAsync();

            return RedirectToAction("DanhSachTinDang", "Admin"); // Quay lại danh sách tin đăng
        }
        // từ chối tin đăng 
        [HttpPost]
        public async Task<IActionResult> TuChoiTinDang(int maTinDang)
        {
            var tinDang = await _context.TinDangs.FindAsync(maTinDang);
            if (tinDang == null)
            {
                return NotFound("Tin đăng không tồn tại.");
            }

            // Thay đổi trạng thái thành "Từ Chối"
            tinDang.TrangThai = TrangThaiTinDang.TuChoi;

            // Lưu thay đổi vào cơ sở dữ liệu
            await _context.SaveChangesAsync();

            return RedirectToAction("DanhSachTinDang", "Admin"); // Quay lại danh sách tin đăng
        }
        // xóa tin đăng 
        [HttpPost]
        public async Task<IActionResult> XoaTinDang(int maTinDang)
        {
            var tinDang = await _context.TinDangs.FindAsync(maTinDang);
            if (tinDang == null)
            {
                return NotFound("Tin đăng không tồn tại.");
            }

            // Xóa tin đăng khỏi cơ sở dữ liệu
            _context.TinDangs.Remove(tinDang);
            await _context.SaveChangesAsync();

            return RedirectToAction("DanhSachTinDang", "Admin"); // Quay lại danh sách tin đăng
        }


        // Model thay đổi vai trò
        public class ChangeRoleModel
        {
            public string UserId { get; set; }
            public string NewRole { get; set; }
        }

        // Model cho API gộp thêm nhân viên và thay đổi vai trò
        public class EmployeeRoleModel
        {
            public string FullName { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string Role { get; set; }
            public string Password { get; set; }
        }

    }
}

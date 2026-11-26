using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    [Route("Admin/Students")]
    public sealed class AdminStudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ApplicationIdentityDbContext _identityContext;
        private readonly StudentCodeGenerator _codeGenerator;
        private readonly ILogger<AdminStudentController> _logger;

        public AdminStudentController(
            ApplicationDbContext context,
            ApplicationIdentityDbContext identityContext,
            StudentCodeGenerator codeGenerator,
            ILogger<AdminStudentController> logger)
        {
            _context = context;
            _identityContext = identityContext;
            _codeGenerator = codeGenerator;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var currentYear = DateTime.Now.Year;
            var model = new StudentIndexViewModel
            {
                Majors = await _context.Majors.AsNoTracking()
                    .OrderBy(major => major.MajorName)
                    .Select(major => new MajorOptionViewModel
                    {
                        MajorCode = major.MajorCode,
                        MajorName = major.MajorName
                    })
                    .ToListAsync(),
                EnrollmentYears = Enumerable.Range(currentYear - 5, 11)
                    .OrderByDescending(year => year)
                    .ToList(),
                CurrentYear = currentYear
            };

            return View("~/Areas/Admin/Views/AdminStudent/Index.cshtml", model);
        }

        [HttpGet("list")]
        public async Task<IActionResult> List(
            string? search,
            string? majorCode,
            int? enrollmentYear,
            string? trainingSystem,
            string? approvalStatus,
            string? sortBy = "studentCode",
            bool descending = false,
            int page = 1,
            int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Students
                .AsNoTracking()
                .Include(student => student.Major)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(student =>
                    student.StudentCode.Contains(term) ||
                    student.Name.Contains(term) ||
                    student.Email.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(majorCode))
            {
                query = query.Where(student => student.MajorCode == majorCode);
            }

            if (enrollmentYear.HasValue)
            {
                query = query.Where(student => student.EnrollmentYear == enrollmentYear.Value);
            }

            if (!string.IsNullOrWhiteSpace(trainingSystem))
            {
                query = query.Where(student => student.TrainingSystem == trainingSystem);
            }

            if (string.Equals(approvalStatus, "pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(student => !student.IsApproved);
            }
            else if (string.Equals(approvalStatus, "approved", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(student => student.IsApproved);
            }

            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Min(page, totalPages);

            var orderedQuery = (sortBy?.ToLowerInvariant(), descending) switch
            {
                ("name", false) => query.OrderBy(student => student.Name).ThenBy(student => student.StudentCode),
                ("name", true) => query.OrderByDescending(student => student.Name).ThenBy(student => student.StudentCode),
                ("enrollmentyear", false) => query.OrderBy(student => student.EnrollmentYear).ThenBy(student => student.StudentCode),
                ("enrollmentyear", true) => query.OrderByDescending(student => student.EnrollmentYear).ThenBy(student => student.StudentCode),
                ("trainingsystem", false) => query.OrderBy(student => student.TrainingSystem).ThenBy(student => student.StudentCode),
                ("trainingsystem", true) => query.OrderByDescending(student => student.TrainingSystem).ThenBy(student => student.StudentCode),
                ("majorcode", false) => query.OrderBy(student => student.MajorCode).ThenBy(student => student.StudentCode),
                ("majorcode", true) => query.OrderByDescending(student => student.MajorCode).ThenBy(student => student.StudentCode),
                ("email", false) => query.OrderBy(student => student.Email).ThenBy(student => student.StudentCode),
                ("email", true) => query.OrderByDescending(student => student.Email).ThenBy(student => student.StudentCode),
                (_, true) => query.OrderByDescending(student => student.StudentCode),
                _ => query.OrderBy(student => student.StudentCode)
            };

            var items = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(student => new StudentListItemViewModel
                {
                    StudentCode = student.StudentCode,
                    Name = student.Name,
                    EnrollmentYear = student.EnrollmentYear,
                    TrainingSystem = student.TrainingSystem,
                    MajorCode = student.MajorCode,
                    MajorName = student.Major == null ? string.Empty : student.Major.MajorName,
                    UserId = student.UserId,
                    IsApproved = student.IsApproved,
                    DateOfBirth = student.DateOfBirth,
                    Address = student.Address,
                    Email = student.Email
                })
                .ToListAsync();

            var userIds = items
                .Where(student => !string.IsNullOrEmpty(student.UserId))
                .Select(student => student.UserId!)
                .Distinct()
                .ToArray();

            if (userIds.Length > 0)
            {
                var creators = await _identityContext.Users
                    .Where(user => userIds.Contains(user.Id))
                    .Select(user => new { user.Id, user.Email })
                    .ToDictionaryAsync(user => user.Id, user => user.Email);

                foreach (var item in items)
                {
                    if (item.UserId != null && creators.TryGetValue(item.UserId, out var email))
                    {
                        item.CreatorEmail = email;
                    }
                }
            }

            return Json(new StudentListResponseViewModel
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            });
        }

        [HttpGet("{id}/details")]
        public async Task<IActionResult> Details(string id)
        {
            var student = await _context.Students.AsNoTracking()
                .Include(item => item.Major)
                .FirstOrDefaultAsync(item => item.StudentCode == id);

            if (student == null)
            {
                return NotFound(new ProblemDetails { Title = "Không tìm thấy sinh viên." });
            }

            var model = ToViewModel(student);
            if (!string.IsNullOrEmpty(student.UserId))
            {
                model.CreatorEmail = await _identityContext.Users
                    .Where(user => user.Id == student.UserId)
                    .Select(user => user.Email)
                    .FirstOrDefaultAsync();
            }

            return Json(model);
        }

        [HttpPost("{id}/approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string id)
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(item => item.StudentCode == id);

            if (student == null)
            {
                return NotFound(new ProblemDetails { Title = "Không tìm thấy hồ sơ sinh viên." });
            }

            if (student.IsApproved)
            {
                return Ok(new { studentCode = student.StudentCode, isApproved = true });
            }

            student.IsApproved = true;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { studentCode = student.StudentCode, isApproved = true });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not approve student {StudentCode}.", id);
                return Conflict(new ProblemDetails
                {
                    Title = "Không thể duyệt hồ sơ.",
                    Detail = "Đã xảy ra lỗi khi cập nhật trạng thái hồ sơ."
                });
            }
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] StudentCreateViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return ValidationError();
            }

            if (!await _context.Majors.AnyAsync(major => major.MajorCode == input.MajorCode))
            {
                ModelState.AddModelError(nameof(input.MajorCode), "Ngành học không tồn tại.");
                return ValidationError();
            }

            try
            {
                var student = new Student
                {
                    StudentCode = await _codeGenerator.GenerateAsync(
                        input.EnrollmentYear,
                        input.TrainingSystem,
                        input.MajorCode),
                    Name = input.Name.Trim(),
                    EnrollmentYear = input.EnrollmentYear,
                    TrainingSystem = input.TrainingSystem,
                    MajorCode = input.MajorCode,
                    DateOfBirth = input.DateOfBirth!.Value,
                    Address = input.Address?.Trim(),
                    Email = input.Email.Trim(),
                    IsApproved = true
                };

                _context.Students.Add(student);
                await _context.SaveChangesAsync();
                await _context.Entry(student).Reference(item => item.Major).LoadAsync();

                return CreatedAtAction(nameof(Details), new { id = student.StudentCode }, ToViewModel(student));
            }
            catch (ArgumentException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Message);
                return ValidationError();
            }
            catch (InvalidOperationException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Message);
                return ValidationError();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not create student in Admin Portal.");
                return Conflict(new ProblemDetails
                {
                    Title = "Không thể lưu sinh viên.",
                    Detail = "Dữ liệu có thể đã thay đổi hoặc vi phạm ràng buộc trong cơ sở dữ liệu."
                });
            }
        }

        [HttpPut("{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(string id, [FromBody] StudentUpdateViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return ValidationError();
            }

            var student = await _context.Students.FirstOrDefaultAsync(item => item.StudentCode == id);
            if (student == null)
            {
                return NotFound(new ProblemDetails { Title = "Không tìm thấy sinh viên." });
            }

            student.Name = input.Name.Trim();
            student.DateOfBirth = input.DateOfBirth!.Value;
            student.Address = input.Address?.Trim();
            student.Email = input.Email.Trim();

            try
            {
                await _context.SaveChangesAsync();
                await _context.Entry(student).Reference(item => item.Major).LoadAsync();
                return Ok(ToViewModel(student));
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not update student {StudentCode} in Admin Portal.", id);
                return Conflict(new ProblemDetails
                {
                    Title = "Không thể lưu thay đổi.",
                    Detail = "Dữ liệu có thể vi phạm ràng buộc trong cơ sở dữ liệu."
                });
            }
        }

        [HttpDelete("{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var student = await _context.Students.FirstOrDefaultAsync(item => item.StudentCode == id);
            if (student == null)
            {
                return NotFound(new ProblemDetails { Title = "Sinh viên không còn tồn tại." });
            }

            _context.Students.Remove(student);
            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogWarning(exception, "Could not delete student {StudentCode}.", id);
                return Conflict(new ProblemDetails
                {
                    Title = "Không thể xóa sinh viên.",
                    Detail = "Sinh viên có thể đang được dữ liệu khác tham chiếu. Hãy xử lý dữ liệu liên quan trước."
                });
            }
        }

        private IActionResult ValidationError()
        {
            return BadRequest(new ValidationProblemDetails(ModelState)
            {
                Title = "Vui lòng kiểm tra lại thông tin đã nhập.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        private static StudentListItemViewModel ToViewModel(Student student)
        {
            return new StudentListItemViewModel
            {
                StudentCode = student.StudentCode,
                Name = student.Name,
                EnrollmentYear = student.EnrollmentYear,
                TrainingSystem = student.TrainingSystem,
                MajorCode = student.MajorCode,
                MajorName = student.Major?.MajorName ?? string.Empty,
                UserId = student.UserId,
                IsApproved = student.IsApproved,
                DateOfBirth = student.DateOfBirth,
                Address = student.Address,
                Email = student.Email
            };
        }
    }
}

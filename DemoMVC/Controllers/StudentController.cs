using System.Security.Claims;
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers
{
    [Authorize(Roles = "User")]
    [Route("[controller]")]
    public sealed class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly StudentCodeGenerator _codeGenerator;
        private readonly ILogger<StudentController> _logger;

        public StudentController(
            ApplicationDbContext context,
            StudentCodeGenerator codeGenerator,
            ILogger<StudentController> logger)
        {
            _context = context;
            _codeGenerator = codeGenerator;
            _logger = logger;
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            if (User.HasClaim(StudentProfilePermissions.ClaimType, StudentProfilePermissions.View))
            {
                return RedirectToAction(nameof(Profiles));
            }

            if (User.HasClaim(StudentProfilePermissions.ClaimType, StudentProfilePermissions.Create))
            {
                return RedirectToAction(nameof(Register));
            }

            return RedirectToAction("AccessDenied", "Account");
        }

        [Authorize(Policy = "StudentProfiles.View")]
        [HttpGet("Profiles")]
        public async Task<IActionResult> Profiles()
        {
            var userId = GetCurrentUserId();
            var students = await _context.Students
                .AsNoTracking()
                .Include(student => student.Major)
                .Where(student => student.UserId == userId)
                .OrderByDescending(student => student.EnrollmentYear)
                .ThenBy(student => student.StudentCode)
                .ToListAsync();

            return View("Index", students);
        }

        [Authorize(Policy = "StudentProfiles.Create")]
        [HttpGet("Register")]
        public async Task<IActionResult> Register()
        {
            await LoadFormOptionsAsync(DateTime.Now.Year);
            return View(new StudentPortalCreateViewModel
            {
                EnrollmentYear = DateTime.Now.Year
            });
        }

        [Authorize(Policy = "StudentProfiles.Create")]
        [HttpPost("Register")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(StudentPortalCreateViewModel input)
        {
            if (ModelState.IsValid &&
                !await _context.Majors.AnyAsync(major => major.MajorCode == input.MajorCode))
            {
                ModelState.AddModelError(nameof(input.MajorCode), "Ngành học không tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                await LoadFormOptionsAsync(input.EnrollmentYear, input.TrainingSystem, input.MajorCode);
                return View(input);
            }

            try
            {
                var studentCode = await _codeGenerator.GenerateAsync(
                    input.EnrollmentYear,
                    input.TrainingSystem,
                    input.MajorCode);

                var student = new Student
                {
                    StudentCode = studentCode,
                    Name = input.Name.Trim(),
                    EnrollmentYear = input.EnrollmentYear,
                    TrainingSystem = input.TrainingSystem,
                    MajorCode = input.MajorCode,
                    DateOfBirth = input.DateOfBirth!.Value,
                    Address = input.Address?.Trim(),
                    Email = input.Email.Trim(),
                    UserId = GetCurrentUserId(),
                    IsApproved = false
                };

                _context.Students.Add(student);
                await _context.SaveChangesAsync();

                var message =
                    $"Đã gửi hồ sơ thành công. Mã sinh viên: {student.StudentCode}. Hồ sơ đang chờ Admin duyệt.";
                if (User.HasClaim(StudentProfilePermissions.ClaimType, StudentProfilePermissions.View))
                {
                    TempData["SuccessMessage"] = message;
                    return RedirectToAction(nameof(Profile), new { id = student.StudentCode });
                }

                TempData["SuccessMessage"] = message;
                return RedirectToAction(nameof(Register));
            }
            catch (ArgumentException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Message);
                await LoadFormOptionsAsync(input.EnrollmentYear, input.TrainingSystem, input.MajorCode);
                return View(input);
            }
            catch (InvalidOperationException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Message);
                await LoadFormOptionsAsync(input.EnrollmentYear, input.TrainingSystem, input.MajorCode);
                return View(input);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not register student for account {UserId}.", GetCurrentUserId());
                ModelState.AddModelError(
                    string.Empty,
                    "Không thể lưu hồ sơ. Mã sinh viên có thể vừa được tạo đồng thời; vui lòng thử lại.");
                await LoadFormOptionsAsync(input.EnrollmentYear, input.TrainingSystem, input.MajorCode);
                return View(input);
            }
        }

        [Authorize(Policy = "StudentProfiles.View")]
        [HttpGet("Profile/{id?}")]
        public async Task<IActionResult> Profile(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction(nameof(Profiles));
            }

            var student = await _context.Students
                .AsNoTracking()
                .Include(item => item.Major)
                .FirstOrDefaultAsync(item =>
                    item.StudentCode == id && item.UserId == GetCurrentUserId());

            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        [Authorize(Policy = "StudentProfiles.Edit")]
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(string id)
        {
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.StudentCode == id && item.UserId == GetCurrentUserId());

            if (student == null)
            {
                return NotFound();
            }

            var input = new StudentPortalEditViewModel
            {
                Name = student.Name,
                EnrollmentYear = student.EnrollmentYear,
                TrainingSystem = student.TrainingSystem,
                MajorCode = student.MajorCode,
                DateOfBirth = student.DateOfBirth,
                Address = student.Address,
                Email = student.Email
            };
            await LoadFormOptionsAsync(
                input.EnrollmentYear,
                input.TrainingSystem,
                input.MajorCode);
            ViewBag.StudentCode = student.StudentCode;
            ViewBag.WasApproved = student.IsApproved;
            return View(input);
        }

        [Authorize(Policy = "StudentProfiles.Edit")]
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, StudentPortalEditViewModel input)
        {
            if (ModelState.IsValid &&
                !await _context.Majors.AnyAsync(major => major.MajorCode == input.MajorCode))
            {
                ModelState.AddModelError(nameof(input.MajorCode), "Ngành học không tồn tại.");
            }

            var student = await _context.Students.FirstOrDefaultAsync(item =>
                item.StudentCode == id && item.UserId == GetCurrentUserId());
            if (student == null)
            {
                return NotFound();
            }

            var wasApproved = student.IsApproved;
            if (!ModelState.IsValid)
            {
                await LoadFormOptionsAsync(
                    input.EnrollmentYear,
                    input.TrainingSystem,
                    input.MajorCode);
                ViewBag.StudentCode = student.StudentCode;
                ViewBag.WasApproved = student.IsApproved;
                return View(input);
            }

            student.Name = input.Name.Trim();
            student.EnrollmentYear = input.EnrollmentYear;
            student.TrainingSystem = input.TrainingSystem;
            student.MajorCode = input.MajorCode;
            student.DateOfBirth = input.DateOfBirth!.Value;
            student.Address = input.Address?.Trim();
            student.Email = input.Email.Trim();
            student.IsApproved = false;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Could not update student {StudentCode} for account {UserId}.",
                    id,
                    GetCurrentUserId());
                ModelState.AddModelError(
                    string.Empty,
                    "Không thể cập nhật hồ sơ lúc này. Vui lòng thử lại.");
                await LoadFormOptionsAsync(
                    input.EnrollmentYear,
                    input.TrainingSystem,
                    input.MajorCode);
                ViewBag.StudentCode = student.StudentCode;
                ViewBag.WasApproved = wasApproved;
                return View(input);
            }

            TempData["SuccessMessage"] =
                "Hồ sơ đã được cập nhật và chuyển về trạng thái Chờ Admin duyệt.";
            if (User.HasClaim(StudentProfilePermissions.ClaimType, StudentProfilePermissions.View))
            {
                return RedirectToAction(nameof(Profile), new { id });
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        [Authorize(Policy = "StudentProfiles.Delete")]
        [HttpGet("Delete/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var student = await _context.Students
                .AsNoTracking()
                .Where(item => item.StudentCode == id && item.UserId == GetCurrentUserId())
                .Select(item => new StudentDeleteViewModel
                {
                    StudentCode = item.StudentCode,
                    Name = item.Name
                })
                .FirstOrDefaultAsync();

            return student == null ? NotFound() : View(student);
        }

        [Authorize(Policy = "StudentProfiles.Delete")]
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var student = await _context.Students.FirstOrDefaultAsync(item =>
                item.StudentCode == id && item.UserId == GetCurrentUserId());
            if (student == null)
            {
                return NotFound();
            }

            var deleted = new StudentDeleteViewModel
            {
                StudentCode = student.StudentCode,
                Name = student.Name
            };
            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            ViewBag.StudentCanCreate =
                User.HasClaim(StudentProfilePermissions.ClaimType, StudentProfilePermissions.Create);
            ViewBag.StudentCanView =
                User.HasClaim(StudentProfilePermissions.ClaimType, StudentProfilePermissions.View);
            return View("DeleteCompleted", deleted);
        }

        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("The authenticated account has no user identifier.");
        }

        private async Task LoadFormOptionsAsync(
            int enrollmentYear,
            string? trainingSystem = null,
            string? majorCode = null)
        {
            var currentYear = DateTime.Now.Year;
            var years = Enumerable.Range(currentYear - 5, 11)
                .OrderByDescending(year => year)
                .Select(year => new SelectListItem(year.ToString(), year.ToString()));

            ViewBag.EnrollmentYears = new SelectList(
                years,
                nameof(SelectListItem.Value),
                nameof(SelectListItem.Text),
                enrollmentYear);

            ViewBag.TrainingSystems = new SelectList(
                new[]
                {
                    new SelectListItem("Đại học không chính quy", "20"),
                    new SelectListItem("Đại học chính quy", "21")
                },
                nameof(SelectListItem.Value),
                nameof(SelectListItem.Text),
                trainingSystem);

            ViewBag.Majors = new SelectList(
                await _context.Majors.AsNoTracking()
                    .OrderBy(major => major.MajorName)
                    .Select(major => new SelectListItem(major.MajorName, major.MajorCode))
                    .ToListAsync(),
                nameof(SelectListItem.Value),
                nameof(SelectListItem.Text),
                majorCode);
        }
    }
}


using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;

namespace DemoMVC.Controllers
{
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Tải danh sách ngành học
        private void LoadMajors(object? selectedMajor = null)
        {
            ViewBag.MajorCode = new SelectList(
                _context.Majors.OrderBy(m => m.MajorName).ToList(),
                "MajorCode",
                "MajorName",
                selectedMajor
            );
        }

        // Tải danh sách năm nhập học
        private void LoadEnrollmentYears(int? selectedYear = null)
        {
            int currentYear = DateTime.Now.Year;

            var years = Enumerable.Range(currentYear - 5, 11)
                .OrderByDescending(y => y)
                .Select(y => new
                {
                    Value = y,
                    Text = y.ToString()
                })
                .ToList();

            ViewBag.EnrollmentYear = new SelectList(
                years,
                "Value",
                "Text",
                selectedYear ?? currentYear
            );
        }

        // Tải danh sách hệ đào tạo
        private void LoadTrainingSystems(string? selectedSystem = null)
        {
            var systems = new[]
            {
                new { Value = "20", Text = "Đại học không chính quy" },
                new { Value = "21", Text = "Đại học chính quy" }
            };

            ViewBag.TrainingSystem = new SelectList(
                systems,
                "Value",
                "Text",
                selectedSystem
            );
        }

        // Tạo mã sinh viên tự động
        private async Task<string> GenerateStudentCode(
            int enrollmentYear,
            string trainingSystem,
            string majorCode)
        {
            string yearCode = (enrollmentYear % 100).ToString("D2");

            string majorNumber = majorCode switch
            {
                "QTKD" => "01",
                "KT" => "02",
                "NNA" => "03",
                "KHAC" => "04",
                "CNTT" => "05",
                _ => throw new ArgumentException("Mã ngành không hợp lệ.")
            };

            if (trainingSystem != "20" && trainingSystem != "21")
            {
                throw new ArgumentException("Hệ đào tạo không hợp lệ.");
            }

            string prefix = yearCode + trainingSystem + majorNumber;

            var existingCodes = await _context.Students
                .Where(s => s.StudentCode.StartsWith(prefix))
                .Select(s => s.StudentCode)
                .ToListAsync();

            int maxNumber = existingCodes
                .Where(code => code.Length == prefix.Length + 4)
                .Select(code =>
                {
                    string suffix = code.Substring(prefix.Length);

                    return int.TryParse(suffix, out int number)
                        ? number
                        : 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            if (maxNumber >= 9999)
            {
                throw new InvalidOperationException(
                    "Nhóm sinh viên này đã đạt giới hạn 9999 mã."
                );
            }

            return prefix + (maxNumber + 1).ToString("D4");
        }

        // GET: Student
        public async Task<IActionResult> Index()
        {
            var students = await _context.Students
                .Include(s => s.Major)
                .ToListAsync();

            return View(students);
        }

        // GET: Student/Details
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Students
                .Include(s => s.Major)
                .FirstOrDefaultAsync(s => s.StudentCode == id);

            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        // GET: Student/Create
        public IActionResult Create()
        {
            LoadMajors();
            LoadEnrollmentYears();
            LoadTrainingSystems();

            return View();
        }

        // POST: Student/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,EnrollmentYear,TrainingSystem,MajorCode,DateOfBirth,Address,Email")]
            Student student)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    student.StudentCode = await GenerateStudentCode(
                        student.EnrollmentYear,
                        student.TrainingSystem,
                        student.MajorCode
                    );

                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Không thể lưu sinh viên. Hãy kiểm tra mã ngành và dữ liệu đã nhập."
                    );
                }
            }

            LoadMajors(student.MajorCode);
            LoadEnrollmentYears(student.EnrollmentYear);
            LoadTrainingSystems(student.TrainingSystem);

            return View(student);
        }

        // GET: Student/Edit
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Students.FindAsync(id);

            if (student == null)
            {
                return NotFound();
            }

            LoadMajors(student.MajorCode);
            LoadEnrollmentYears(student.EnrollmentYear);
            LoadTrainingSystems(student.TrainingSystem);

            return View(student);
        }

        // POST: Student/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string id,
            [Bind("StudentCode,Name,EnrollmentYear,TrainingSystem,MajorCode,DateOfBirth,Address,Email")]
            Student student)
        {
            if (id != student.StudentCode)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors.Select(e =>
                        $"{x.Key}: {e.ErrorMessage}"));

                foreach (var error in errors)
                {
                    Console.WriteLine("LỖI KIỂM TRA: " + error);
                }
            }
            else
            {
                try
                {
                    student.StudentCode = await GenerateStudentCode(
                        student.EnrollmentYear,
                        student.TrainingSystem,
                        student.MajorCode
                    );

                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Không thể lưu sinh viên. Hãy kiểm tra mã ngành và dữ liệu đã nhập."
                    );
                }
            }
            {
                try
                {
                    _context.Update(student);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StudentExists(student.StudentCode))
                    {
                        return NotFound();
                    }

                    throw;
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Không thể lưu thay đổi. Hãy kiểm tra dữ liệu đã nhập."
                    );
                }
            }

            LoadMajors(student.MajorCode);
            LoadEnrollmentYears(student.EnrollmentYear);
            LoadTrainingSystems(student.TrainingSystem);

            return View(student);
        }

        // GET: Student/Delete
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Students
                .Include(s => s.Major)
                .FirstOrDefaultAsync(s => s.StudentCode == id);

            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        // POST: Student/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var student = await _context.Students.FindAsync(id);

            if (student != null)
            {
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool StudentExists(string id)
        {
            return _context.Students.Any(s => s.StudentCode == id);
        }
    }
}

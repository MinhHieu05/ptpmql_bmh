using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers;

[AllowAnonymous]
[Route("PublicStudents")]
public sealed class PublicStudentsController : Controller
{
    private const int PageSize = 10;

    private readonly ApplicationDbContext _context;

    public PublicStudentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var query = _context.Students
            .AsNoTracking()
            .Where(student => student.IsApproved)
            .AsQueryable();

        var term = search?.Trim() ?? string.Empty;
        if (term.Length > 0)
        {
            var escapedTerm = term
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var pattern = $"%{escapedTerm}%";
            query = query.Where(student =>
                EF.Functions.Like(student.StudentCode, pattern, "\\") ||
                EF.Functions.Like(student.Name, pattern, "\\"));
        }

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var model = new PublicStudentListViewModel
        {
            Search = term,
            Page = page,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Students = await query
                .OrderBy(student => student.Name)
                .ThenBy(student => student.StudentCode)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(student => new PublicStudentViewModel
                {
                    StudentCode = student.StudentCode,
                    Name = student.Name,
                    MajorName = student.Major == null ? student.MajorCode : student.Major.MajorName,
                    EnrollmentYear = student.EnrollmentYear
                })
                .ToListAsync()
        };

        return View(model);
    }
}

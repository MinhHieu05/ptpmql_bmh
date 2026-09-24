using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Models;
using DemoMVC.Data;

namespace DemoMVC.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        //Xem danh sach NV
        {
            var employees = await _context.Employees.ToListAsync();

            return View(employees);
        }

        [HttpGet]
        //Tao moi NV
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost] //phan biet du lieu gui len tu form ket noi giua CRUD va Validate
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee)
        {
            if (ModelState.IsValid)
            {
                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(employee);
        }

        [HttpGet]
        //Sua NV
        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _context.Employees.FindAsync(id);

            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Employee employee)
        {
            if (id != employee.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                _context.Employees.Update(employee);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(employee);
        }

        [HttpGet]
        //Xem chi tiet NV
        public async Task<IActionResult> Details(int id)
        {
            var employee = await _context.Employees.FindAsync(id);

            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        [HttpGet]
        //Xoa NV
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _context.Employees.FindAsync(id);

            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employees.FindAsync(id);

            if (employee != null)
            {
                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
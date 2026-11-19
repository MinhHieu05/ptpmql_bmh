using DemoMVC.Data;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Services
{
    public sealed class StudentCodeGenerator
    {
        private readonly ApplicationDbContext _context;

        public StudentCodeGenerator(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateAsync(
            int enrollmentYear,
            string trainingSystem,
            string majorCode)
        {
            var yearCode = (enrollmentYear % 100).ToString("D2");

            var majorNumber = majorCode switch
            {
                "QTKD" => "01",
                "KT" => "02",
                "NNA" => "03",
                "KHAC" => "04",
                "CNTT" => "05",
                _ => throw new ArgumentException("Mã ngành không hợp lệ.", nameof(majorCode))
            };

            if (trainingSystem != "20" && trainingSystem != "21")
            {
                throw new ArgumentException("Hệ đào tạo không hợp lệ.", nameof(trainingSystem));
            }

            var prefix = yearCode + trainingSystem + majorNumber;
            var existingCodes = await _context.Students
                .Where(student => student.StudentCode.StartsWith(prefix))
                .Select(student => student.StudentCode)
                .ToListAsync();

            var maxNumber = existingCodes
                .Where(code => code.Length == prefix.Length + 4)
                .Select(code =>
                {
                    var suffix = code[prefix.Length..];
                    return int.TryParse(suffix, out var number) ? number : 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            if (maxNumber >= 9999)
            {
                throw new InvalidOperationException("Nhóm sinh viên này đã đạt giới hạn 9999 mã.");
            }

            return prefix + (maxNumber + 1).ToString("D4");
        }
    }
}

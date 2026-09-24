using DemoMVC.Models;
using Microsoft.EntityFrameworkCore;
namespace DemoMVC.Data
{
     public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext (DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; } = default!;
        public DbSet<Employee> Employees { get; set; } = default!;
        public DbSet<Score> Scores { get; set; } = default!;
    }
}
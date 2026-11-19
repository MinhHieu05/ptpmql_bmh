
using DemoMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; } = default!;
        public DbSet<Employee> Employees { get; set; } = default!;
        public DbSet<Score> Scores { get; set; } = default!;
        public DbSet<Major> Majors { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Student>()
                .HasOne(student => student.Owner)
                .WithMany()
                .HasForeignKey(student => student.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

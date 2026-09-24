using System.ComponentModel.DataAnnotations;
namespace DemoMVC.Models
{
    public class Student
    {
        [Key]
        public string StudentCode { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Age { get; set; } = default!;
        public string? Address { get; set; }
        public string Email { get; set; } = default!;
    }
}
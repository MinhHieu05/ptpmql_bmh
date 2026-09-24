using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public class Employee
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(50, ErrorMessage = "Họ tên không được quá 50 ký tự")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tuổi")]
        [Range(18, 60, ErrorMessage = "Tuổi phải từ 18 đến 60")]
        public int Age { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoMVC.Models
{
    public class Student
    {
        [Key]
        
        [Display(Name = "Mã sinh viên")]
        public string StudentCode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Họ và tên")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Năm nhập học")]
        public int EnrollmentYear { get; set; }

        [Required]
        [Display(Name = "Hệ đào tạo")]
        public string TrainingSystem { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Mã ngành học")]
        public string MajorCode { get; set; } = string.Empty;

        [ForeignKey(nameof(MajorCode))]
        public Major? Major { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Ngày tháng năm sinh")]
        public DateTime DateOfBirth { get; set; }

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }
}

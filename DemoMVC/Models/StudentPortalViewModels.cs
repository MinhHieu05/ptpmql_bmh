using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public sealed class StudentPortalCreateViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [Display(Name = "Họ và tên")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn năm nhập học.")]
        [Range(1, 9999)]
        [Display(Name = "Năm nhập học")]
        public int EnrollmentYear { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn hệ đào tạo.")]
        [RegularExpression("^(20|21)$", ErrorMessage = "Hệ đào tạo không hợp lệ.")]
        [Display(Name = "Hệ đào tạo")]
        public string TrainingSystem { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn ngành học.")]
        [Display(Name = "Ngành học")]
        public string MajorCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập ngày sinh.")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày sinh")]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public sealed class StudentPortalEditViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [Display(Name = "Họ và tên")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn năm nhập học.")]
        [Range(1, 9999)]
        [Display(Name = "Năm nhập học")]
        public int EnrollmentYear { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn hệ đào tạo.")]
        [RegularExpression("^(20|21)$", ErrorMessage = "Hệ đào tạo không hợp lệ.")]
        [Display(Name = "Hệ đào tạo")]
        public string TrainingSystem { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn ngành học.")]
        [Display(Name = "Ngành học")]
        public string MajorCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập ngày sinh.")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày sinh")]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public sealed class StudentDeleteViewModel
    {
        public string StudentCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }
}

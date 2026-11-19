using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public sealed class StudentIndexViewModel
    {
        public IReadOnlyList<MajorOptionViewModel> Majors { get; init; }
            = Array.Empty<MajorOptionViewModel>();

        public IReadOnlyList<int> EnrollmentYears { get; init; }
            = Array.Empty<int>();

        public int CurrentYear { get; init; }
    }

    public sealed class MajorOptionViewModel
    {
        public string MajorCode { get; init; } = string.Empty;
        public string MajorName { get; init; } = string.Empty;
    }

    public sealed class StudentCreateViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(1, 9999)]
        public int EnrollmentYear { get; set; }

        [Required]
        [RegularExpression("^(20|21)$", ErrorMessage = "Hệ đào tạo không hợp lệ.")]
        public string TrainingSystem { get; set; } = string.Empty;

        [Required]
        public string MajorCode { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public string? Address { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public sealed class StudentUpdateViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public string? Address { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public sealed class StudentListItemViewModel
    {
        public string StudentCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int EnrollmentYear { get; init; }
        public string TrainingSystem { get; init; } = string.Empty;
        public string MajorCode { get; init; } = string.Empty;
        public string MajorName { get; init; } = string.Empty;
        public string? UserId { get; init; }
        public string? CreatorEmail { get; set; }
        public bool IsApproved { get; init; }
        public DateTime DateOfBirth { get; init; }
        public string? Address { get; init; }
        public string Email { get; init; } = string.Empty;
    }

    public sealed class PublicStudentViewModel
    {
        public string StudentCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string MajorName { get; init; } = string.Empty;
        public int EnrollmentYear { get; init; }
    }

    public sealed class PublicStudentListViewModel
    {
        public IReadOnlyList<PublicStudentViewModel> Students { get; init; }
            = Array.Empty<PublicStudentViewModel>();

        public string Search { get; init; } = string.Empty;
        public int Page { get; init; }
        public int TotalItems { get; init; }
        public int TotalPages { get; init; }
    }

    public sealed class StudentListResponseViewModel
    {
        public IReadOnlyList<StudentListItemViewModel> Items { get; init; }
            = Array.Empty<StudentListItemViewModel>();

        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalItems { get; init; }
        public int TotalPages { get; init; }
    }
}

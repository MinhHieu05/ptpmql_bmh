
using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public class Major
    {
        [Key]
        [Required]
        [Display(Name = "Mã ngành học")]
        public string MajorCode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Tên ngành học")]
        public string MajorName { get; set; } = string.Empty;

        public ICollection<Student> Students { get; set; }
            = new List<Student>();
    }
}

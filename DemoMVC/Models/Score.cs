using System.ComponentModel.DataAnnotations;
namespace DemoMVC.Models
{
    public class Score
    {
        [Key]
        public string Name { get; set; } = default!;
        public string Subject { get; set; } = default!;
        public string Result { get; set; } = default!;
    }
}

using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Controllers
{
    public class Lesson5Controller : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Index(string name)
        {
            TempData["Message"] = "Chào bạn " + name + " đã gửi dữ liệu thành công!";

            return RedirectToAction("Index");
        }
    }
}

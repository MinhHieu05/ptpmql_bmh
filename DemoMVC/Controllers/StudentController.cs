using Microsoft.AspNetCore.Mvc;
using DemoMVC.Models;

namespace DemoMVC.Controllers;

public class StudentController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
     public IActionResult Index(Student std)
    {
        var result="Hello" + std.Name + " - " + std.Age + " - " + std.Address;
        ViewBag.Result = result;
        return View();
    }

    public IActionResult Create()
    {
        return View();
    }
}
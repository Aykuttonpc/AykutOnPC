using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AykutOnPC.Web.Controllers;

[Authorize]
public class AdminController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}

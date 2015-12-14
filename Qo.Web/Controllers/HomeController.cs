namespace Qo.Web.Controllers
{
    using Qo.Parsing;
    using System.Web.Mvc;

    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Home Page";

            return View();
        }

        [HttpPost]
        public JsonResult SubmitQuery(string sqlQuery)
        {
            var parser = new QoParser();

            var response = new { Test = "Heyo" };

            return Json(response, JsonRequestBehavior.AllowGet);
        } 
    }
}

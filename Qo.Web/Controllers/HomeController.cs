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
        public ActionResult SubmitQuery(QoPackage model)
        {
            if (string.IsNullOrEmpty(model.SqlQuery))
            {
                Response.StatusCode = 400;
                return Json("No query was received!", JsonRequestBehavior.AllowGet);
            }

            var qoParser = new QoParser();
            var qoOptimizer = new QoOptimizer();
            var package = qoParser.Parse(model.SqlQuery);
            qoOptimizer.Run(package);
            
            return Json(package, JsonRequestBehavior.AllowGet);
        } 
    }

    public class QoPackage
    {
        public string SqlQuery { get; set; }
        public Table[] Tables { get; set; }
    }

    public class Table
    {
        public string Name { get; set; }
        public Attribute[] Attributes { get; set; }
    }

    public class Attribute
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsFk { get; set; }
    }
}

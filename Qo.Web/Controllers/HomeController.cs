namespace Qo.Web.Controllers
{
    using Parsing.QueryModel;
    using Parsing;
    using System.Web.Mvc;
    using System.Linq;
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

            var schema = new Schema();
            if(model.Tables.Any())
            {
                foreach(var t in model.Tables)
                {
                    var rel = new Relation { Name = t.Name };
                    foreach (var a in t.Attributes)
                    {
                        var att = new Parsing.QueryModel.Attribute { Name = a.Name, Type = a.Type };
                        rel.Attributes.Add(att);
                        if(a.IsPk)
                        {
                            rel.PrimaryKey.Add(att);
                        }
                    }
                    schema.Relations.Add(rel);
                }
            }

            var qoParser = new QoParser();
            var qoOptimizer = new QoOptimizer();
            if (schema.Relations.Any())
            {
                qoParser = new QoParser(schema);
                qoOptimizer = new QoOptimizer(schema);
            }
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
        public bool IsPk { get; set; }
    }
}

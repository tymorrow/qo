namespace Qo.Web.Controllers
{
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    using Qo.Parsing;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Web;
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

            var p = new TSql120Parser(false);
            IList<ParseError> errors;
            var result = p.Parse(new StringReader(model.SqlQuery), out errors);

            if(errors.Any())
            {
                var sb = new StringBuilder();
                foreach(var e in errors)
                {
                    sb.AppendLine(e.Message);
                }
                Response.StatusCode = 400;
                return Json(sb.ToString(), JsonRequestBehavior.AllowGet);
            }
            else
            {

            }

            var response = new QoResponse
            {
                Tokens = result.ScriptTokenStream,
                Tables = model.Tables
            };
            return Json(response, JsonRequestBehavior.AllowGet);
        } 

    }

    public class QoResponse
    {
        public IList<TSqlParserToken> Tokens { get; set; }
        public Table[] Tables { get; set; }
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

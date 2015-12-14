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
        public ActionResult SubmitQuery(string sqlQuery)
        {
            if (string.IsNullOrEmpty(sqlQuery))
            {
                Response.StatusCode = 400;
                return Json("No query was received!", JsonRequestBehavior.AllowGet);
            }

            var p = new TSql120Parser(false);
            IList<ParseError> errors = new List<ParseError>();
            var result = p.Parse(new StringReader(sqlQuery), out errors);

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

            return Json(result.ScriptTokenStream, JsonRequestBehavior.AllowGet);
        } 
    }
}

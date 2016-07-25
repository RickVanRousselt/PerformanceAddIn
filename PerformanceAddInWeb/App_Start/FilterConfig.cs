using System.Web;
using System.Web.Mvc;
using PerformanceAddInWeb.Filters;

namespace PerformanceAddInWeb
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new AiHandleErrorAttribute());
        }
    }
}

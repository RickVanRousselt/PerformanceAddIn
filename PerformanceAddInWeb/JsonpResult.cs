using System;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace PerformanceAddInWeb
{
    public class JsonpResult : JsonResult
    {
        private readonly object _data;

        public JsonpResult()
        {
        }

        public JsonpResult(object data)
        {
            _data = data;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var response = context.HttpContext.Response;
            var request = context.HttpContext.Request;

            var callbackfunction = request["callback"];

            if (string.IsNullOrEmpty(callbackfunction))
            {
                throw new InvalidOperationException("Callback function name must be provided in the request!");
            }
            response.ContentType = "application/x-javascript";
            if (_data != null)
            {
                var serializer = new JavaScriptSerializer {MaxJsonLength = int.MaxValue};
                response.Write($"{callbackfunction}({serializer.Serialize(_data)});");
            }

        }

    }

}
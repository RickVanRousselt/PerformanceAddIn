using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PerformanceAddInWeb.Models
{

    public class NewsModel
    {
        public string Title { get; set; }
        public string Content { get; set; }

        public NewsModel(string title, string content)
        {
            Title = title;
            Content = content;
        }
    }
}
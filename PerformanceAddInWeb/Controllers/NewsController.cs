using System;
using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.Taxonomy;
using Microsoft.SharePoint.Client.UserProfiles;
using PerformanceAddInWeb.Models;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Security;
using Microsoft.ApplicationInsights;

namespace PerformanceAddInWeb.Controllers
{
    public class NewsController : Controller
    {
        public ActionResult Index()
        {
            var sw = Stopwatch.StartNew();
            var ai = new TelemetryClient();
            var startTime = DateTime.UtcNow;
            var newsViewModel = new List<NewsModel>();
            var spContext = SharePointContextProvider.Current.GetSharePointContext(HttpContext);

            using (var clientContext = spContext.CreateUserClientContextForSPHost())
            {
                if (clientContext == null) return View(newsViewModel);

                //get user info
                var spUser = clientContext.Web.CurrentUser;
                clientContext.Load(spUser, user => user.LoginName);
                clientContext.ExecuteQuery();

                IDatabase cache = RedisHelper.Connection.GetDatabase();
                var cacheKey = $"news_{spUser.LoginName}";

                if (cache.KeyExists(cacheKey))
                {
                    newsViewModel = JsonConvert.DeserializeObject<List<NewsModel>>(cache.StringGet(cacheKey));
                    sw.Stop();
                    var timespanCache = sw.Elapsed;
                    ViewBag.Time = $"{timespanCache.Minutes:00}m:{timespanCache.Seconds:00}s:{timespanCache.Milliseconds / 10:00}ms";
                    ai.TrackDependency("Redis", cacheKey, startTime, sw.Elapsed, true);
                    return View(newsViewModel);
                }

                //Get news items
                Web web = clientContext.Web;
                List newsList = web.GetList("sites/Portal/Lists/News");
                clientContext.Load(newsList);
                var items = newsList.GetItems(CamlQuery.CreateAllItemsQuery());
                clientContext.Load(items,
                    includes =>
                        includes.Include(y => y["Device"], y => y["Department"], y => y.Client_Title,
                            y => y["NewsContent"]));
                clientContext.ExecuteQuery();



                //get user profile property
                PeopleManager peopleManager = new PeopleManager(clientContext);

                var profilePropertyNames = new[] { "Device", "SPS-Department", "Title" };
                UserProfilePropertiesForUser profilePropertiesForUser = new UserProfilePropertiesForUser(
                    clientContext, spUser.LoginName, profilePropertyNames);
                IEnumerable<string> profilePropertyValues =
                    peopleManager.GetUserProfilePropertiesFor(profilePropertiesForUser);

                // Load the request and run it on the server.
                clientContext.Load(profilePropertiesForUser);
                clientContext.ExecuteQuery();

                var propertyValues = profilePropertyValues as IList<string> ?? profilePropertyValues.ToList();
                var devices = propertyValues.ToList()[0];
                var department = propertyValues.ToList()[1];

                foreach (var item in items)
                {
                    TaxonomyFieldValueCollection col = item["Device"] as TaxonomyFieldValueCollection;

                    foreach (var taxItem in col)
                    {
                        if (devices.Contains(taxItem.Label))
                        {
                            int index = newsViewModel.FindIndex(i => i.Title == item.Client_Title);
                            if (index != 0)
                            {
                                newsViewModel.Add(new NewsModel(item.Client_Title, item["NewsContent"].ToString()));
                            }
                        }
                    }
                }
                cache.StringSet(cacheKey, JsonConvert.SerializeObject(newsViewModel), new TimeSpan(0, 0, 1, 0));
            }


            sw.Stop();
            var timespan = sw.Elapsed;
            ViewBag.Time = $"{timespan.Minutes:00}m:{timespan.Seconds:00}s:{timespan.Milliseconds / 10:00}ms";
            ai.TrackDependency("SharePoint", "GetNews", startTime, sw.Elapsed, true);
            return View(newsViewModel);
        }


        public async Task<JsonpResult> GetTips()
        {
            IDatabase cache = RedisHelper.Connection.GetDatabase();
            var cacheKey = "tip";
            var ai = new TelemetryClient();
            var startTime = DateTime.UtcNow;
            var timer = System.Diagnostics.Stopwatch.StartNew();

            if (cache.KeyExists(cacheKey))
            {
                timer.Stop();
                ai.TrackDependency("Redis", "GetTip", startTime, timer.Elapsed, true);
                return new JsonpResult(JsonConvert.DeserializeObject<TipsModel>(await cache.StringGetAsync(cacheKey)));
            }

            var tip = GetTipForCurrentUser();

            await cache.StringSetAsync(cacheKey, JsonConvert.SerializeObject(tip), new TimeSpan(0, 0, 10, 0));
            timer.Stop();
            ai.TrackDependency("SharePoint", "GetTip", startTime, timer.Elapsed, true);
            return new JsonpResult(tip);
        }



        private TipsModel GetTipForCurrentUser()
        {

            ClientContext clientContext = new ClientContext(ConfigurationManager.AppSettings["SiteCollectionUrl"])
            {
                RequestTimeout = 1000000,
                Credentials =
                    new SharePointOnlineCredentials(ConfigurationManager.AppSettings["UserName"],
                        ConvertToSecureString(ConfigurationManager.AppSettings["Password"]))
            };


            //get user info
            var spUser = clientContext.Web.CurrentUser;
            clientContext.Load(spUser, user => user.LoginName);
            clientContext.ExecuteQuery();

            

            //Get tip items
            Web web = clientContext.Web;
            List newsList = web.GetList("sites/Portal/Lists/Tips");
            clientContext.Load(newsList);
            var items = newsList.GetItems(CamlQuery.CreateAllItemsQuery());
            clientContext.Load(items,
                includes =>
                    includes.Include(y => y["Device"], y => y["Department"], y => y.Client_Title, y => y["Content"]));
            clientContext.ExecuteQuery();

            //get user profile property
            PeopleManager peopleManager = new PeopleManager(clientContext);

            var profilePropertyNames = new string[] { "Device", "SPS-Department", "Title" };
            UserProfilePropertiesForUser profilePropertiesForUser = new UserProfilePropertiesForUser(
                clientContext, spUser.LoginName, profilePropertyNames);
            IEnumerable<string> profilePropertyValues =
                peopleManager.GetUserProfilePropertiesFor(profilePropertiesForUser);

            // Load the request and run it on the server.
            clientContext.Load(profilePropertiesForUser);
            clientContext.ExecuteQuery();

            var devices = profilePropertyValues.ToList()[0];
            var department = profilePropertyValues.ToList()[1];
            var tip = new TipsModel();

            foreach (var item in items)
            {
                TaxonomyFieldValueCollection col = item["Device"] as TaxonomyFieldValueCollection;

                foreach (var taxItem in col)
                {
                    if (devices.Contains(taxItem.Label))
                    {
                       
                      tip = new TipsModel(item.Client_Title, item["Content"].ToString());
                    }
                }
            }
         

            return tip;
        }
        
        private SecureString ConvertToSecureString(string password)
        {
            if (password == null)
                throw new ArgumentNullException("password");

            var securePassword = new SecureString();

            foreach (char c in password)
                securePassword.AppendChar(c);

            securePassword.MakeReadOnly();
            return securePassword;
        }
    }
}

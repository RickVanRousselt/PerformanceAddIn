using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.Taxonomy;
using Microsoft.SharePoint.Client.UserProfiles;
using Newtonsoft.Json;
using PerformanceAddInWeb;
using PerformanceAddInWeb.Models;
using StackExchange.Redis;

namespace NewsJob
{
    // To learn more about Microsoft Azure WebJobs SDK, please see http://go.microsoft.com/fwlink/?LinkID=320976
    class Program
    {
        static void Main()
        {
            var newsViewModel = new List<NewsModel>();

            ClientContext clientContext = new ClientContext("https://mod738287.sharepoint.com/sites/Portal")
            {
                RequestTimeout = 1000000,
                Credentials =
                    new SharePointOnlineCredentials("admin@MOD738287.onmicrosoft.com",
                        ConvertToSecureString("pass@word1"))
            };


            //get user info
            var spUser = clientContext.Web.CurrentUser;
            clientContext.Load(spUser, user => user.LoginName);
            clientContext.ExecuteQuery();

            IDatabase cache = RedisHelper.Connection.GetDatabase();
            var cacheKey = $"news_{spUser.LoginName}";

            //Get news items
            Web web = clientContext.Web;
            List newsList = web.GetList("sites/Portal/Lists/News");
            clientContext.Load(newsList);
            var items = newsList.GetItems(CamlQuery.CreateAllItemsQuery());
            clientContext.Load(items,
                includes =>
                    includes.Include(y => y["Device"], y => y["Department"], y => y.Client_Title, y => y["NewsContent"]));
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




        private static SecureString ConvertToSecureString(string password)
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

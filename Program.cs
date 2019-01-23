using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace toggl_export
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
  .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
  .AddEnvironmentVariables()
  .AddCommandLine(args)
  .Build();

            Run(config.Get<Config>()).Wait();
        }
        private static async Task<T> GetData<T>(string url, string api_key, object query = null)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri("https://www.toggl.com");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Basic",
        Convert.ToBase64String(
            System.Text.ASCIIEncoding.ASCII.GetBytes(
                string.Format("{0}:{1}", api_key, "api_token"))));
            var q = "";
            if (query != null)
            {
                ToQueryString(query);
            }
            var req = url + q;
            var result = await client.GetAsync(req);
            var json = await result.Content.ReadAsStringAsync();
            result.EnsureSuccessStatusCode();

            return JsonConvert.DeserializeObject<T>(json);
        }

        private async static Task Run(Config config)
        {
            //var workspaces = await GetData<Workspace[]>("/api/v8/workspaces", api_key);
            //var projects = (await GetData<Project[]>($"/api/v8/workspaces/{workspaces.First().id}/projects", api_key)).ToDictionary(o=>o.id, o=>o.name);



            var start_date = new DateTime(DateTime.Today.Year, config.Month, 1).ToString("yyyy-MM-ddTHH\\:mm\\:sszzz");
            var end_date = new DateTime(DateTime.Today.Year, config.Month + 1, 1).ToString("yyyy-MM-ddTHH\\:mm\\:sszzz");

            var entries = await GetData<TimeEntry[]>("/api/v8/time_entries", config.Api_Key, new { start_date, end_date });
            var grouped = entries
                .Select(o=>new { start = o.start.ToString("yyyy-MM-dd"), budget = config.BudgetTags.FirstOrDefault(b => o.tags.Contains(b))??"other", duration = ((o.stop == DateTime.MinValue ? DateTime.Now : o.stop) - o.start).TotalSeconds } )
                .GroupBy(o => new { o.start, o.budget })
                .Select(o => new GroupedTimeEntry { start = o.Key.start, budget = o.Key.budget, duration = o.Sum(p => p.duration) }).ToList();
            var daySeconds = 8 * 60 * 60;
            foreach (var group in grouped.GroupBy(o => o.start))
            {
                var duration = group.Sum(o => o.duration);
                var scale = daySeconds / duration;
                foreach (var time in group)
                {
                    time.duration = time.duration * scale;
                }
            }

            foreach (var item in grouped)
            {
                Console.WriteLine($"{item.start}\t{item.budget}\t{TimeSpan.FromSeconds(item.duration).ToString("hh\\:mm")}");
            }
            Console.ReadLine();
        }

        private static string ToQueryString(object input)
        {
            var nvc = input.GetType().GetProperties().ToDictionary(o => o.Name, o => o.GetValue(input)?.ToString());
            var array = nvc.Select(o=>string.Format("{0}={1}", HttpUtility.UrlEncode(o.Key), HttpUtility.UrlEncode(o.Value)))
                .ToArray();
            return "?" + string.Join("&", array);
        }
    }
    public class GroupedTimeEntry
    {
        public string budget { get; set; }
        public string start { get; set; }
        public double duration { get; set; }
    }
    public class TimeEntry
    {
        public int id { get; set; }
        public string guid { get; set; }
        public int wid { get; set; }
        public int pid { get; set; }
        public bool billable { get; set; }
        public DateTime start { get; set; }
        public DateTime stop { get; set; }
        public int duration { get; set; }
        public List<string> tags { get; set; }
        public bool duronly { get; set; }
        public DateTime at { get; set; }
        public int uid { get; set; }
        public string description { get; set; }
    }
    public class Project
    {
        public int id { get; set; }
        public int wid { get; set; }
        public int cid { get; set; }
        public string name { get; set; }
        public bool billable { get; set; }
        public bool is_private { get; set; }
        public bool active { get; set; }
        public DateTime at { get; set; }
    }
    public class Workspace
    {
        public int id { get; set; }
        public string name { get; set; }
        public bool premium { get; set; }
        public bool admin { get; set; }
        public int default_hourly_rate { get; set; }
        public string default_currency { get; set; }
        public bool only_admins_may_create_projects { get; set; }
        public bool only_admins_see_billable_rates { get; set; }
        public int rounding { get; set; }
        public int rounding_minutes { get; set; }
        public DateTime at { get; set; }
        public string logo_url { get; set; }
    }

    public class Config
    {
        public string[] BudgetTags { get; set; }
        public int HoursPerDay { get; set; }
        public string Api_Key { get; set; }
        public int Month { get; set; }
    }
}

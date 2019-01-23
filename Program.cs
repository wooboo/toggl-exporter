using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace toggl_export
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Run(args[0], int.Parse(args[1])).Wait();
        }

        private async static Task Run(string api_key, int month)
        {
            var start_date = new DateTime(DateTime.Today.Year, month, 1).ToString("yyyy-MM-ddTHH\\:mm\\:sszzz");
            var end_date = new DateTime(DateTime.Today.Year, month + 1, 1).ToString("yyyy-MM-ddTHH\\:mm\\:sszzz");

            var client = new HttpClient();
            client.BaseAddress = new Uri("https://www.toggl.com");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Basic",
        Convert.ToBase64String(
            System.Text.ASCIIEncoding.ASCII.GetBytes(
                string.Format("{0}:{1}", api_key, "api_token"))));
            var req = $"/api/v8/time_entries{ToQueryString(new { start_date, end_date })}";
            var result = await client.GetAsync(req);
            var json = await result.Content.ReadAsStringAsync();
            result.EnsureSuccessStatusCode();

            var entries = JsonConvert.DeserializeObject<TimeEntry[]>(json);

            var grouped = entries
                .Select(o=>new { start = o.start.ToString("yyyy-MM-dd"), budget = o.tags.FirstOrDefault(), duration = ((o.stop == DateTime.MinValue ? DateTime.Now : o.stop) - o.start).TotalSeconds } )
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
}

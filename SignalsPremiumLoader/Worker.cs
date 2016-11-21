
namespace SignalsPremiumLoader
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using FSharp.Data;
    using FSharp.Data.Runtime;

    internal class Worker
    {
        private static FormUrlEncodedContent CreateRequestContent(string homePage, string login, string password)
        {
            Console.WriteLine("Parsing home page...");

            var doc = HtmlDocument.Parse(homePage);
            var inputs = doc.Descendants("input");

            var cbsecuritym3 = string.Empty;
            var ret = string.Empty;

            foreach (var htmlNode in inputs)
            {
                if (htmlNode.AttributeValue("name") == "cbsecuritym3")
                {
                    cbsecuritym3 = htmlNode.AttributeValue("value");
                }
                if (htmlNode.AttributeValue("name") == "return")
                {
                    ret = htmlNode.AttributeValue("value");
                }
            }

            var content =
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("option", "com_comprofiler"), new KeyValuePair<string, string>("view", "login"),
                    new KeyValuePair<string, string>("op2", "login"), new KeyValuePair<string, string>("return", ret),
                    new KeyValuePair<string, string>("message", "0"), new KeyValuePair<string, string>("loginfrom", "loginmodule"),
                    new KeyValuePair<string, string>("cbsecuritym3", cbsecuritym3), new KeyValuePair<string, string>("username", login),
                    new KeyValuePair<string, string>("passwd", password), new KeyValuePair<string, string>("Submit", ""),
                });
            return content;
        }

        internal async void DoGetAndParseAsync(string login, string password)
        {
            await DoGetAndParseAsyncInternal(login, password);
        }

        private async Task DoGetAndParseAsyncInternal(string login, string password)
        {
            var baseAddress = new Uri("http://signalspremium.com/");
            var loginUrl = @"cb-login/login";
            var tablesUrl = @"trading-resources-information-center/?tmpl=component";

            var cookieContainer = new CookieContainer();
            using (var handler = new HttpClientHandler {CookieContainer = cookieContainer, UseCookies = true})
            {
                using (var client = new HttpClient(handler) {BaseAddress = baseAddress})
                {
                    SetDefaultHeaders(client);

                    Console.WriteLine("Getting home page...");

                    var homePageResult = await client.GetAsync("/");
                    var homePage = await homePageResult.Content.ReadAsStringAsync();

                    var content = CreateRequestContent(homePage, login, password);

                    Console.WriteLine("Try login...");

                    var loginResult = client.PostAsync(loginUrl, content);
                    loginResult.Wait();

                    Console.WriteLine(loginResult.Result);

                    loginResult.Result.EnsureSuccessStatusCode();

                    Console.WriteLine("Getting tables page...");

                    var text = await client.GetStringAsync(tablesUrl);

                    ParseAndSaveTables(text);
                }
            }

            Console.WriteLine("The End! Press Esc key...");
        }

        private static DataTable ParseAndFillTable(HtmlTable table, ref int tableCount)
        {
            var dt = new DataTable($"Table{++tableCount}");

            if (table.HasHeaders?.Value ?? false)
            {
                foreach (var col in table.HeaderNamesAndUnits.Value)
                {
                    dt.Columns.Add(col.Item1, col.Item2.Value);
                }
            }
            else
            {
                foreach (var col in new[] {"NAME", "RATE", "DATE", "TIME", "DIRECTION", "EXPIRY"})
                {
                    dt.Columns.Add(col);
                }
            }

            foreach (var tableRow in table.Rows)
            {
                var row = dt.NewRow();
                var c = 0;

                foreach (var s in tableRow)
                {
                    row[c++] = s;
                }
                dt.Rows.Add(row);
                row.AcceptChanges();
            }
            return dt;
        }

        private static void ParseAndSaveTables(string text)
        {
            Console.WriteLine("Parsing tables page...");

            //Change direction images to text
            var regexUp = new Regex("\\<img src=.*\\/up.png\">");
            var regexDown = new Regex("\\<img src=.*\\/down.png\">");

            text = regexUp.Replace(text, "Up");
            text = regexDown.Replace(text, "Down");


            var html = HtmlDocument.Parse(text);
            var tables = HtmlRuntime.getTables(null, true, html);

            var tableCount = 0;

            foreach (var table in tables)
            {
                var dt = ParseAndFillTable(table, ref tableCount);


                var filename = Path.Combine(Environment.CurrentDirectory, $@"table{tableCount}.xml");

                Console.WriteLine("Saving table {0} to {1}", dt.TableName, filename);
                
                var stream = File.Create(filename);

                dt.WriteXml(stream, XmlWriteMode.WriteSchema, true);

                stream.FlushAsync().ContinueWith(t=> stream.Close()).Wait();
            }
        }

        private static void SetDefaultHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/53.0.2785.143 Safari/537.36");
            client.DefaultRequestHeaders.Add("Origin", client.BaseAddress.Host);
            client.DefaultRequestHeaders.Add("Referer", client.BaseAddress.Host);
            client.DefaultRequestHeaders.Add("DNT", "1");
            client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using ShellProgressBar;

namespace code
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var program = new Program();
            await program.Download();
        }

        public async Task Download()
        {
            var web = new HtmlWeb();
            var client = new HttpClient();
            var currentPage = 1;
            var pageSize = 10;

            var progressBarOptions = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Green,
                BackgroundColor = ConsoleColor.DarkGreen,
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };

            while (true)
            {
                var url = $"http://www.k8smeetup.com/downloadAll?type=pdf&skip={(currentPage - 1) * pageSize}";
                Console.WriteLine($"Handling {url}");

                var doc = web.Load(url);

                var items = doc
                            .QuerySelectorAll("div.list ul.bd li.item")
                            .Select(x => new
                            {
                                Name = HttpUtility.HtmlDecode(x.QuerySelector("div.left div.name").InnerText),
                                Link = HttpUtility.HtmlDecode(x.QuerySelector("a.right").Attributes["href"].Value)
                            });

                if (!items.Any())
                {
                    break;
                }
                else
                {
                    foreach (var item in items)
                    {
                        var ext = Path.GetExtension(item.Link);
                        var fileName = item.Name.EndsWith(ext) ? item.Name : item.Name + ext;
                        var filePath = "./" + fileName;


                        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, item.Link));
                        var totalSize = response.Content.Headers.ContentLength;
                        var lastModified = response.Content.Headers.LastModified;

                        if (File.Exists(filePath))
                        {
                            var fileInfo = new FileInfo(filePath);

                            if (fileInfo.Length == totalSize || (lastModified.HasValue && fileInfo.LastWriteTime == lastModified.Value.DateTime))
                            {
                                Console.WriteLine($"Not Modified, skipping {fileName} ...");
                                continue;
                            }
                        }

                        Console.WriteLine($"Downloading {fileName} ...");

                        using (var progressBar = new ProgressBar((int)totalSize.Value, $"Downloading ...", progressBarOptions))
                        using (var source = await client.GetStreamAsync(item.Link))
                        using (var destination = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                        {
                            var cancellationToken = new CancellationToken();
                            var buffer = new byte[8196];
                            var totalBytesRead = 0L;
                            var bytesRead = 0;

                            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
                            {
                                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                totalBytesRead += bytesRead;
                                progressBar.Tick((int)totalBytesRead);
                            }
                        }

                        if (lastModified.HasValue)
                        {
                            File.SetLastWriteTime(filePath, lastModified.Value.DateTime);
                        }
                    }

                    currentPage++;
                }
            }
        }
    }
}

using HtmlAgilityPack;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NugetBot
{
    public static class NugetService
    {
        public static async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string query)
        {
            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;

            SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            PackageSearchResource resource = await repository.GetResourceAsync<PackageSearchResource>();
            SearchFilter searchFilter = new SearchFilter(includePrerelease: true);

            IEnumerable<IPackageSearchMetadata> results = await resource.SearchAsync(
                query,
                searchFilter,
                skip: 0,
                take: 20,
                logger,
                cancellationToken);

            return results;
        }

        public static async Task<bool> PackageExists(string id)
        {
            using(HttpClient c = new HttpClient())
            {
                var response = await c.GetAsync($"https://www.nuget.org/packages/{id}/").ConfigureAwait(false);

                return response.IsSuccessStatusCode;
            }
        }

        public static async Task<NugetPackageInfo> GetPackageInfoAsync(string name)
        {
            var url = GetPackageUrl(name);

            var web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync(url);

            var vers = doc.DocumentNode.SelectNodes("//tr[@class='bg-info']").First().ParentNode.ChildNodes.Where(x => x.Name == "tr").Select(x => x.ChildNodes.First(x => x.Name == "td")).Select(x => x.ChildNodes.First(x => x.Name == "a")).Select(x => (Regex.Match(x.ParentNode.ParentNode.ChildNodes[3].InnerHtml, @"(\d+)").Groups[1].Value, x.GetAttributeValue("title", null)));
            var total = doc.DocumentNode.SelectNodes("//span[@class='download-info-content']");
            var mpg = await GetNugetMetadata(name);

            var totalDownloads = total[0].InnerHtml.ParseFormattedNumber();
            var currentVersion = total[1].InnerHtml.ParseFormattedNumber();
            var perDay = total[2].InnerHtml.ParseFormattedNumber();

            mpg.Versions.ForEach(x => x.Downloads = long.Parse(vers.FirstOrDefault(y => y.Item2 == x.Name).Value));
            mpg.CurrentVersionDownloads = currentVersion;
            mpg.PerDayAverage = perDay;
            mpg.TotalDownloads = totalDownloads;
            return mpg;
        }

        private static async Task<NugetPackageInfo> GetNugetMetadata(string name)
        {
            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;

            SourceCacheContext cache = new SourceCacheContext();
            SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();

            IEnumerable<PackageSearchMetadataRegistration> versions = (await resource.GetMetadataAsync(
                name,
                includePrerelease: true,
                includeUnlisted: false,
                cache,
                logger,
                cancellationToken)).Cast<PackageSearchMetadataRegistration>().Where(x => x.PackageId == name);


            return new NugetPackageInfo()
            {
                Name = versions.FirstOrDefault()?.Title,
                Author = versions.FirstOrDefault()?.Authors,
                Versions = versions.Select(x => new NugetPackageVersion() { DatePublished = x.Published, Name = x.Version.OriginalVersion, Downloads = x.DownloadCount }).ToList()
            };
        }

        private static string GetPackageUrl(string name)
        {
            return $"https://www.nuget.org/packages/{name}/";
        }
    }

    public class NugetPackageInfo
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public List<NugetPackageVersion> Versions { get; set; }
        public long TotalDownloads { get; set; }
        public long CurrentVersionDownloads { get; set; }
        public long PerDayAverage { get; set; }
        public NugetPackageVersion Current => Versions.OrderByDescending(x => (x.DatePublished ?? DateTimeOffset.Now).Ticks).FirstOrDefault();

    }

    public class NugetPackageVersion
    {
        public long? Downloads { get; set; }
        public string Name { get; set; }
        public DateTimeOffset? DatePublished { get; set; }
    }
}

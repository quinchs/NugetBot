using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Driver;
using System.Threading.Tasks;
using System.Diagnostics;
using MongoDB.Bson;
using System.Collections.Immutable;

namespace NugetBot.Models
{
    public class NugetPackageTracker : MongoCachedEntity<NugetPackageTracker, (ulong, string)>, IEntity<(ulong, string)>
    {
        public ulong GuildId { get; set; }
        public string PackageName { get; set; }
        public string PackageId { get; set; }
        public string Author { get; set; }
        public List<VersionStats> Stats { get; set; } = new();

        [BsonRepresentation(BsonType.DateTime)]
        public DateTime LastUpdated { get; set; }
        public long TotalDownloads { get; set; }
        public long PerDayAverage { get; set; }

        [BsonIgnore]
        public long TotalVerionDownloads
            => Stats.Sum(x => x.Downloads);

        public long AvgTotalDownloads
            => (TotalVerionDownloads + TotalDownloads) / 2;

        [BsonIgnore]
        public IOrderedEnumerable<VersionStats> OrderedStats
            => Stats.OrderByDescending(x => x.DateReleased.Ticks);

        [BsonIgnore]
        public VersionStats CurrentVersion 
            => OrderedStats.FirstOrDefault();

        [BsonIgnore]
        public string IconUrl
            => $"https://api.nuget.org/v3-flatcontainer/{PackageId}/{CurrentVersion.Version}/icon";

        [BsonIgnore]
        public VersionStats MostDownloadedVersion
            => Stats.OrderByDescending(x => x.Downloads).FirstOrDefault();

        [BsonIgnore]
        private readonly Logger _log;

        public NugetPackageTracker() : base(MongoService.PackageTracker, PackageTrackerCache.Cache) 
        {
            _log = Logger.GetLogger<NugetPackageTracker>();
        }

        public static async Task<NugetPackageTracker> GetOrCreateAsync(string packageId, ulong guildId)
        {
            return await GetAsync(packageId, guildId) ?? await CreateAsync(packageId, guildId);
        }

        public IReadOnlyCollection<VersionStats> GetVersionInfo(string version)
            => Stats.Where(x => x.Version == version).OrderByDescending(x => x.DateReleased).ToImmutableArray();
        public static async Task<NugetPackageTracker> CreateAsync(string packageId, ulong guildId)
        {
            Stopwatch sw = Stopwatch.StartNew();

            var stats = await NugetService.GetPackageInfoAsync(packageId);

            var entity = new NugetPackageTracker()
            {
                PackageId = packageId,
                PackageName = stats.Name,
                GuildId = guildId,
                Author = stats.Author,
                LastUpdated = DateTime.UtcNow,
                PerDayAverage = stats.PerDayAverage,
                TotalDownloads = stats.TotalDownloads,
            };

            foreach(var ver in stats.Versions)
            {
                var newerVersionDate = stats.Versions.FirstOrDefault(x => x.DatePublished > ver.DatePublished)?.DatePublished;

                entity.Stats.Add(new VersionStats()
                {
                    DateReleased = ver.DatePublished?.UtcDateTime ?? DateTime.UtcNow,
                    DateUpdated = newerVersionDate?.UtcDateTime,
                    Downloads = ver.Downloads ?? 0,
                    Version = ver.Name,
                    DataPoints = new List<DownloadDataPoint>()
                    {
                        new DownloadDataPoint()
                        {
                            Date = ver.DatePublished?.UtcDateTime ?? DateTime.UtcNow,
                            Downloads = ver.Downloads ?? 0,
                            Inferred = true
                        }
                    }
                });
            }

            await entity.SaveAsync();

            sw.Stop();

            entity._log.Trace($"Creating tracker for {packageId} took {sw.ElapsedMilliseconds}ms");

            return entity;
        }

        public static async Task<NugetPackageTracker> GetAsync(string packageId, ulong guildId)
        {
            var sw = Stopwatch.StartNew();
            Logger log = Logger.GetLogger<NugetPackageTracker>();
            try
            {
                if (PackageTrackerCache.TryGet(packageId, guildId, out var entity))
                    return entity;

                var result = await MongoService.PackageTracker.Find(x => x.PackageId == packageId && guildId == x.GuildId).ToListAsync().ConfigureAwait(false);

                if (result.Any())
                {
                    entity = result.FirstOrDefault();
                    PackageTrackerCache.TryAddOrUpdate(entity);
                    return entity;
                }

                return null;
            }
            finally
            {
                sw.Stop();
                log.Trace($"Get package took {sw.ElapsedMilliseconds}ms");
            }
        }

        (ulong, string) IEntity<(ulong, string)>.Id => (GuildId, PackageName);
    }
}

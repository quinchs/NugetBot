using NugetBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot
{
    public static class PackageTrackerCache
    {
        public static int Size
        {
            get => Cache.Size;
            set => Cache.Size = value;
        }

        public static BaseCache<NugetPackageTracker, (ulong, string)> Cache;

        public static void Configure(int concurrency, int size)
        {
            Cache = new BaseCache<NugetPackageTracker, (ulong, string)>(concurrency, size);
        }

        public static bool TryAddOrUpdate(NugetPackageTracker entity)
            => Cache.TryAddOrUpdate(entity);

        public static bool TryAdd(NugetPackageTracker entity)
            => Cache.TryAdd(entity);

        public static bool TryRemove(string id, ulong guildId, out NugetPackageTracker removed)
            => Cache.TryRemove((guildId, id), out removed);

        public static bool TryUpdate(NugetPackageTracker entity, out NugetPackageTracker old)
            => Cache.TryUpdate(entity, out old);

        public static bool TryGet(string id, ulong guildId, out NugetPackageTracker entity)
            => Cache.TryGet((guildId, id), out entity);

        public static bool Contains(string id, ulong guildId)
            => Cache.Contains((guildId, id));
    }
}

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot.Models
{
    public class VersionStats
    {
        [BsonRepresentation(BsonType.DateTime)]
        public DateTime? DateUpdated { get; set; }
        [BsonRepresentation(BsonType.DateTime)]
        public DateTime DateReleased { get; set; }
        public string Version { get; set; }


        public List<DownloadDataPoint> DataPoints = new();

        [BsonIgnore]
        public long Downloads => DataPoints.Sum(x => x.Downloads);

        [BsonIgnore]
        public long AverageDownloadsPerDay
            => Downloads / (long)Math.Ceiling(((DateUpdated ?? DateTime.UtcNow) - DateReleased).TotalDays);
    }

    public class DownloadDataPoint
    {
        public bool Inferred { get; set; }
        public long Downloads { get; set; }
        public DateTime Date { get; set; }

    }
}

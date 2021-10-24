using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using NugetBot.Models;

namespace NugetBot
{
    public static class MongoService
    {
        public static MongoClient Client = new MongoClient(ConfigService.Config.MongoCS);

        static MongoService()
        {

        }

        public static IMongoDatabase Database
            => Client.GetDatabase("nuget-bot");

        public static IMongoCollection<NugetPackageTracker> PackageTracker
            => Database.GetCollection<NugetPackageTracker>("package-tracker");
    }
}

using Discord.WebSocket;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace NugetBot.Handlers
{
    public class TrackerUpdaterHandler : DiscordHandler
    {
        private Timer _timer;
        private Logger _log;

        public override void Initialize(DiscordShardedClient client)
        {
            _log = Logger.GetLogger<TrackerUpdaterHandler>();
            _timer = new Timer();

            _timer.Interval = TimeSpan.FromHours(6).TotalMilliseconds;
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();
        }

        private async void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                var packagesToUpdate = await MongoService.PackageTracker.Find(x => (DateTime.UtcNow - x.LastUpdated).TotalHours >= 6).ToListAsync().ConfigureAwait(false);
            }
            catch(Exception x)
            {
                
            }
            finally
            {

            }
        }
    }
}

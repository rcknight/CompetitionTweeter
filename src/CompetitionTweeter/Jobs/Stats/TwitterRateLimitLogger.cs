using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToTwitter;
using Quartz;
using log4net;

namespace CompetitionTweeter.Jobs.Stats
{
    public class TwitterRateLimitLogger : IJob
    {
        private TwitterContext _twitter;
        private ILog _logger = LogManager.GetLogger("Rate Limit Logger");

        public TwitterRateLimitLogger(TwitterContext ctx)
        {
            _twitter = ctx;
        }

        public void Execute(IJobExecutionContext context)
        {
            var helpResult =
                (from help in _twitter.Help
                 where help.Type == HelpType.RateLimits
                 //&&
                 //help.Resources == "search,users"
                 select help)
                    .SingleOrDefault();

            var logString = "";
            if (helpResult != null)
            {
                foreach (var category in helpResult.RateLimits)
                {
                    foreach (var limit in category.Value)
                    {
                        if (limit.Remaining != limit.Limit)
                        {
                            logString +=
                                String.Format("\nResource: {0}\n    Remaining: {1}    Reset: {2}    Limit: {3}",
                                              limit.Resource, limit.Remaining,
                                              FromUnixTime(limit.Reset).ToShortDateString() + " " +
                                              FromUnixTime(limit.Reset).ToShortTimeString(), limit.Limit);
                        }
                    }
                }

                _logger.Info("Twitter Rate Limits:" + Environment.NewLine + logString);
            }
        }

        private DateTime FromUnixTime(ulong unixTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime);
        }
    }
}

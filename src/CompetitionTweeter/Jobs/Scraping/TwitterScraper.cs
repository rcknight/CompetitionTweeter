using System;
using System.Collections.Generic;
using System.Linq;
using CompetitionTweeter.Storage.Tasks;
using Quartz;
using TweetinCore.Interfaces;
using Tweetinvi;
using TwitterToken;
using log4net;

namespace CompetitionTweeter.Jobs.Scraping
{
    [DisallowConcurrentExecution]
    public class TwitterScraper : IJob
    {
        private ITwitterActionQueue _queue;
        private static Dictionary<string,long> _lastStatus = new Dictionary<string, long>()
            {
                {"jbask14", 1},
                {"bockingselmbabe", 1},
                {"gemmagwynne", 1},
                {"dorotheecomp77", 1}
            };

        private Token _twitterToken; 
        private ILog _logger = LogManager.GetLogger("Twitter Scraper");

        public TwitterScraper(ITwitterActionQueue queue, Token twitterToken)
        {
            _twitterToken = twitterToken;
            _queue = queue;
        }

        public void Execute(IJobExecutionContext context)
        {
            int skippedOld = 0;

            var users = new List<String> {"jbask14", "bockingselmbabe", "gemmagwynne"};
            foreach (var username in users)
            {
                try
                {
                    var user = new User(username, _twitterToken);
                    List<ITweet> statuses =
                        user.GetUserTimeline(true, _twitterToken).Where(s => s.Id > _lastStatus[username]).ToList();
                    _logger.InfoFormat("User {0}: {1} new statuses", username, statuses.Count);
                    foreach (var status in statuses)
                    {
                        if (status.CreatedAt.ToUniversalTime() < DateTime.UtcNow.AddHours(-12))
                        {
                            skippedOld++;
                            continue;
                        }
                        if (status.Retweeting != null)
                        {
                            var source = String.Format("https://twitter.com/{0}/status/{1}",
                                                       status.Creator.ScreenName, status.IdStr);
                            //this is a retweet
                            var targetStatus = status.Retweeting;

                            _queue.EnqueueRetweet(targetStatus.IdStr, source);
                            _queue.EnqueueFollow(targetStatus.Creator.ScreenName.ToLower(), source);

                            //also follow any mentioned users
                            foreach (var entity in targetStatus.UserMentions)
                            {
                                _queue.EnqueueFollow(entity.ScreenName.ToLower(), source);
                            }
                        }
                    }

                    try
                    {
                        if (statuses.Any() && statuses.First().Id.HasValue)
                        {
                            _lastStatus[username] = statuses.First().Id.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorFormat("Error setting last staus id:\n {0}", ex);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            }


            if (skippedOld > 0)
                _logger.InfoFormat("Skipped {0} old tweets", skippedOld);

            _logger.Info("Twitter scrape complete");
        }
    }
}

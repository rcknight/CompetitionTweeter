using System;
using System.Collections.Generic;
using System.Linq;
using CompetitionTweeter.Storage.Tasks;
using LinqToTwitter;
using Quartz;
using log4net;

namespace CompetitionTweeter.Jobs.Scraping
{
    [DisallowConcurrentExecution]
    public class TwitterScraper : IJob
    {
        private TwitterContext _twitter;
        private ITwitterActionQueue _queue;
        private static ulong _lastStatus = 1;

        private ILog _logger = LogManager.GetLogger("Twitter Scraper");

        public TwitterScraper(ITwitterActionQueue queue, TwitterContext ctx)
        {
            _twitter = ctx;
            _queue = queue;
        }

        public void Execute(IJobExecutionContext context)
        {
            _logger.InfoFormat("Twitter scrape started, lastStatus: {0}", _lastStatus);
            try
            {
                var query =
                    (from list in _twitter.List
                     where list.Type == ListType.Statuses &&
                           list.OwnerScreenName == "Richk1986" &&
                           list.Slug == "other-compers" &&
                           list.SinceID == _lastStatus &&
                           list.Count == 100
                     select list);

                List<Status> statuses = new List<Status>();

                if (!query.Any())
                {
                    _logger.Info("No new items on twitter list");
                }
                else
                {
                    statuses.AddRange(query.First().Statuses);
                    _logger.InfoFormat("{0} new statuses", statuses.Count);
                }

                int skippedOld = 0;

                foreach (var status in statuses)
                {
                    if (status.CreatedAt.ToUniversalTime() < DateTime.UtcNow.AddHours(-12))
                    {
                        skippedOld++;
                        continue;
                    }
                    var targetStatus = status.RetweetedStatus;
                    if (targetStatus != null && targetStatus.StatusID != null)
                    {
                        //this is a retweet
                        //follow the person
                        var source = String.Format("https://twitter.com/{0}/status/{1}",
                                                   status.User.Identifier.ScreenName, status.ID);

                        _queue.EnqueueFollow(targetStatus.User.Identifier.ScreenName.ToLower(), source);
                        //retweet this status
                        _queue.EnqueueRetweet(targetStatus.StatusID,source);
                        
                        //also follow any mentioned users
                        foreach (var entity in targetStatus.Entities.UserMentionEntities)
                        {
                            _queue.EnqueueFollow(entity.ScreenName.ToLower(), source);
                        }  
                    }
                }

                if(skippedOld > 0)
                    _logger.InfoFormat("Skipped {0} old tweets", skippedOld);

                try
                {
                    _lastStatus = ulong.Parse(statuses.First().StatusID);
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

            _logger.Info("Twitter scrape complete");

        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CompetitionTweeter.DTO;
using CompetitionTweeter.Storage.Tasks;
using Quartz;
using Tweetinvi;
using TwitterToken;
using log4net;

namespace CompetitionTweeter.Jobs.TwitterActions
{
    [DisallowConcurrentExecution]
    public class TwitterActionHandler : IJob
    {
        private ITwitterActionQueue _queue;
        private Token _twitterToken;
        private ILog _logger = LogManager.GetLogger("Twitter Action Handler");

        public TwitterActionHandler(ITwitterActionQueue queue, Token twitterToken)
        {
            _queue = queue;
            _twitterToken = twitterToken;
        }

        public void Execute(IJobExecutionContext context)
        {
            _logger.Info("Twitter Action Handler Starter");
            //loop while there are more tasks to do
            while(_queue.TryPerformTask(DoTask)){}

            _logger.Info("Twitter Action Handler Completed");
        }

        private void DoTask(TwitterAction action)
        {
            var random = new Random();
            //sleep a few random seconds before performing
            var sleepFor = random.Next(5000, 15000);
            _logger.InfoFormat("Sleeping for {0}ms", sleepFor);
            Thread.Sleep(sleepFor);

            switch (action.ActionType)
            {
                case TwitterActionType.Follow:
                    ExecuteWithRetries(() => Follow(action.Id), "Following " + action.Id);
                    break;
                case TwitterActionType.Retweet:
                    ExecuteWithRetries(() => Retweet(action.Id), "Retweeting " + action.Id);
                    break;
            }
        }

        private void Follow(string userId)
        {
            _logger.InfoFormat("Attempting to follow {0}", userId);
            var me = new TokenUser(_twitterToken);
            _logger.Info("Got tokenuser");
            var toFollow = new User(userId, _twitterToken);
            _logger.Info("Got toFollow");
            me.Follow(toFollow, true);
            _logger.InfoFormat("Followed user {0}", userId);
        }

        private void Retweet(string statusId)
        {
            _logger.InfoFormat("Attempting to retweet {0}", statusId);
            var result = _twitterToken.ExecutePOSTQuery("https://api.twitter.com/1.1/statuses/retweet/" + statusId + ".json");
            _logger.InfoFormat("Posted retweet at https://twitter.com/RichK1985/status/{0}", result["id_str"]);
        }

        private const int retryCount = 5;
        private const int retryDelay = 5000;
        private void ExecuteWithRetries(Action a, string operationDescription = "")
        {
            List<Exception> errors = new List<Exception>();
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    a();
                    return;
                }
                catch (WebException ex)
                {
                    _logger.Error("Web Exception");
                    _logger.Error(ex);
                    if (ex.Response != null)
                    {
                        if (ex.Response.Headers["Status"].Contains("429"))
                        {
                            //rate limit
                            Console.WriteLine("429. (rate limit) Sleeping for 15 mins");
                            var rateLimitResetHeader = ex.Response.Headers["X-Rate-Limit-Reset"];
                            var rateLimitLimitHeader = ex.Response.Headers["X-Rate-Limit-Limit"];

                            if (rateLimitResetHeader != null)
                            {
                                var limitResetTime = FromUnixTime(ulong.Parse(rateLimitResetHeader));
                                Console.WriteLine("Limit: {0}", rateLimitLimitHeader);
                                Console.WriteLine("Reset at {0} {1}", limitResetTime.ToShortDateString(), limitResetTime.ToShortTimeString());
                                var difference = limitResetTime.ToUniversalTime() - DateTime.Now.ToUniversalTime();
                                Console.WriteLine("Sleeping thread until then ({0} mins)",difference.TotalMinutes);
                                Thread.Sleep(difference);
                            }

                        }
                        var responseStream = ex.Response.GetResponseStream();
                        if (responseStream != null)
                        {
                            StreamReader reader = new StreamReader(responseStream, Encoding.UTF8);
                            String responseString = reader.ReadToEnd();
                            Console.WriteLine(responseString);
                            errors.Add(new RetweetException(responseString));
                        }
                    }
                    
                    errors.Add(ex);
                }
                catch (Exception ex)
                {
                    _logger.Error("Other Exception");
                    errors.Add(ex);
                }
                Thread.Sleep(retryDelay);
            }

            _logger.ErrorFormat("Operation reached retries count: \n\t {0} \n\t Last Error:\n\t{1}", operationDescription, errors.Last());
        }

        private DateTime FromUnixTime(ulong unixTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime);
        }
    }

    internal class RetweetException : Exception
    {
        public RetweetException(string response) : base(response)
        {
        }
    }

    internal class TweetNotFoundException : Exception
    {
    }
}

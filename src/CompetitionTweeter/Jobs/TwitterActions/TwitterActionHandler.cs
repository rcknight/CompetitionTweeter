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
            var me = new TokenUser(_twitterToken);
            var toFollow = new User(userId, _twitterToken);
            me.Follow(toFollow, true);
            _logger.InfoFormat("Followed user {0}", userId);
        }

        private void Retweet(string statusId)
        {
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
                    if (ex.Response != null)
                    {
                        if (ex.Response.Headers["Status"].Contains("429"))
                        {
                            //rate limit
                            Console.WriteLine("429. (rate limit) Sleeping for 15 mins");
                            Thread.Sleep(1000 * 60 * 15);
                        }
                        var responseStream = ex.Response.GetResponseStream();
                        if (responseStream != null)
                        {
                            StreamReader reader = new StreamReader(responseStream, Encoding.UTF8);
                            String responseString = reader.ReadToEnd();
                            if(responseString.Contains("rate limit"))
                            Console.WriteLine(responseString);
                            errors.Add(new RetweetException(responseString));
                        }
                    }
                    
                    errors.Add(ex);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
                Thread.Sleep(retryDelay);
            }

            _logger.ErrorFormat("Operation reached retries count: \n\t {0} \n\t Last Error:\n\t{1}", operationDescription, errors.Last());
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

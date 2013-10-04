using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        private void Retweet(string statusId)
        {
            var longId = long.Parse(statusId);
            var toRetweet = new Tweet(longId, _twitterToken);
            var myTweet = toRetweet.PublishRetweet();
            _logger.InfoFormat("Posted retweet at https://twitter.com/RichK1985/status/{0}", myTweet.IdStr);
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
                    a(); return;
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

    internal class TweetNotFoundException : Exception
    {
    }
}

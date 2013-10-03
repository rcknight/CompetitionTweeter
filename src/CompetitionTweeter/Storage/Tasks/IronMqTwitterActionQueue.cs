using System;
using Blacksmith.Core;
using CompetitionTweeter.DTO;
using CompetitionTweeter.Storage.TwitterHistory;
using log4net;

namespace CompetitionTweeter.Storage.Tasks
{
    public class IronMqTwitterActionQueue : ITwitterActionQueue
    {
        private Client.QueueWrapper<TwitterAction> _queue;
        private ITwitterHistoryRepository _history;
        private ILog _logger = LogManager.GetLogger("Action Queue");

        public IronMqTwitterActionQueue(Client ironMqclient, ITwitterHistoryRepository history)
        {
            _queue = ironMqclient.Queue<TwitterAction>();
            _history = history;
        }

        private void EnqueueAction(TwitterAction action, string source)
        {
            _queue.Push(action);
            _logger.InfoFormat("Enqueued action {0} {1} (Source {2})", action.ActionType.ToString(), action.Id, source);
        }

        public void EnqueueRetweet(string statusId)
        {
            EnqueueRetweet(statusId, "Not specified");
        }

        public void EnqueueRetweet(string statusId, string source)
        {
            if (!_history.HasRetweeted(statusId))
            {
                EnqueueAction(new TwitterAction(statusId, TwitterActionType.Retweet), source);
                _history.RecordReTweet(statusId);
            }
        }

        public void EnqueueFollow(string userId)
        {
            EnqueueFollow(userId, "Not Specified");
        }

        public void EnqueueFollow(string userId, string source)
        {
            if (!_history.HasFollowed(userId))
            {
                EnqueueAction(new TwitterAction(userId, TwitterActionType.Follow), source);
                _history.RecordFollow(userId);
            }
        }

        public bool TryPerformTask(Action<TwitterAction> action)
        {
            if (_queue.IsEmpty())
                return false;

            _queue.Next(600).Consume((message, ctx) =>
                {
                    //don't dispatch duplicates
                    var type = message.Target.ActionType;
                    var id = message.Target.Id.ToLower();
                    if ((type == TwitterActionType.Follow && _history.HasFollowed(id)) ||
                        type == TwitterActionType.Retweet && _history.HasRetweeted(id))
                        return;

                    action(message.Target);
                });
            return true;
        }
    }
}
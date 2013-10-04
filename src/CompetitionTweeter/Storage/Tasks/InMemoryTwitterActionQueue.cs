using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using CompetitionTweeter.DTO;
using CompetitionTweeter.Storage.TwitterHistory;
using log4net;

namespace CompetitionTweeter.Storage.Tasks
{
    public class InMemoryTwitterActionQueue : ITwitterActionQueue
    {
        private Queue<TwitterAction> _queue;
        private ITwitterHistoryRepository _history;
        private ILog _logger = LogManager.GetLogger("Action Queue");

        public InMemoryTwitterActionQueue(ITwitterHistoryRepository history)
        {
            _queue = new Queue<TwitterAction>();
            _history = history;
        }

        private void EnqueueAction(TwitterAction action, string source)
        {
            lock (_queue)
            {
                _queue.Enqueue(action);
                _logger.InfoFormat("Enqueued handler {0} {1} (Source {2})", action.ActionType.ToString(), action.Id, source);    
            }
        }

        public void EnqueueRetweet(string statusId)
        {
            EnqueueRetweet(statusId, "Not specified");
        }

        public void EnqueueRetweet(string statusId, string source)
        {
            lock (_queue)
            {
                if (!_history.HasRetweeted(statusId))
                {
                    EnqueueAction(new TwitterAction(statusId, TwitterActionType.Retweet), source);
                    _history.RecordReTweet(statusId);
                }
            }
        }

        public void EnqueueFollow(string userId)
        {
            EnqueueFollow(userId, "Not Specified");
        }

        public void EnqueueFollow(string userId, string source)
        {
            lock (_queue)
            {
                if (!_history.HasFollowed(userId))
                {
                    EnqueueAction(new TwitterAction(userId, TwitterActionType.Follow), source);
                    _history.RecordFollow(userId);
                }
            }
        }

        public bool TryPerformTask(Action<TwitterAction> handler)
        {
            lock (_queue)
            {
                if (_queue.Count > 0)
                    return false;

                var message = _queue.Dequeue();

                //don't dispatch duplicates
                var type = message.ActionType;
                var id = message.Id.ToLower();
                if ((type == TwitterActionType.Follow && _history.HasFollowed(id)) ||
                    type == TwitterActionType.Retweet && _history.HasRetweeted(id))
                    return true;

                handler(_queue.Dequeue());

                return true;
            }
        }
    }
}
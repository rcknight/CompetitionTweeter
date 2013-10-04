using System;
using System.Linq;
using Blacksmith.Core;
using Blacksmith.Core.Responses;
using CompetitionTweeter.DTO;
using CompetitionTweeter.Storage.TwitterHistory;
using System.Threading;
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
            _logger.InfoFormat("Enqueued handler {0} {1} (Source {2})", action.ActionType.ToString(), action.Id, source);
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

        public bool TryPerformTask(Action<TwitterAction> handler)
        {
            try {
                var messages = _queue.Get(1, 600).ToList();
                if (!messages.Any())
                    return false;
                var message = messages.First();
                var action = message.Payload.Target;
                handler(action);
                _queue.Delete(message.Payload.Id);
                return true;
            } catch (Exception ex) {
                _logger.Error("Error Dequeueing");
                _logger.ErrorFormat(ex);
                _logger.Info("Retrying in 2s");
                Thread.Sleep(2000);
                return true;
            }

        }
    }
}

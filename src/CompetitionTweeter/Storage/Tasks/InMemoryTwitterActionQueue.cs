using System;
using System.Collections.Concurrent;
using System.Configuration;
using Blacksmith.Core;
using CompetitionTweeter.DTO;
using CompetitionTweeter.Storage.TwitterHistory;

namespace CompetitionTweeter.Storage.Tasks
{
    public class InMemoryTwitterActionQueue : ITwitterActionQueue
    {
        private ConcurrentQueue<TwitterAction> _queue;
        private ITwitterHistoryRepository _history;

        public InMemoryTwitterActionQueue(ITwitterHistoryRepository history)
        {
            _queue = new ConcurrentQueue<TwitterAction>();
            _history = history;
        }

        private void EnqueueAction(TwitterAction action)
        {
            _queue.Enqueue(action);
        }

        public void EnqueueRetweet(string statusId)
        {
            if (!_history.HasRetweeted(statusId))
            {
                EnqueueAction(new TwitterAction(statusId, TwitterActionType.Retweet));
            }
        }

        public void EnqueueFollow(string userId)
        {
            if (!_history.HasFollowed(userId))
            {
                EnqueueAction(new TwitterAction(userId, TwitterActionType.Follow));
            }
        }

        public bool TryPerformTask(Action<TwitterAction> action)
        {
            throw new NotImplementedException();
        }
    }

    public class IronMqTwitterActionQueue : ITwitterActionQueue
    {
        private Client.QueueWrapper<TwitterAction> _queue;
        private ITwitterHistoryRepository _history;

        public IronMqTwitterActionQueue(Client ironMqclient, ITwitterHistoryRepository history)
        {
            _queue = ironMqclient.Queue<TwitterAction>();
            _history = history;
        }

        private void EnqueueAction(TwitterAction action)
        {
            _queue.Push(action);
        }

        public void EnqueueRetweet(string statusId)
        {
            if (!_history.HasRetweeted(statusId))
            {
                EnqueueAction(new TwitterAction(statusId, TwitterActionType.Retweet));
            }
        }

        public void EnqueueFollow(string userId)
        {
            if (!_history.HasFollowed(userId))
            {
                EnqueueAction(new TwitterAction(userId, TwitterActionType.Follow));
            }
        }

        public bool TryPerformTask(Action<TwitterAction> action)
        {
            throw new NotImplementedException();
        }
    }
}
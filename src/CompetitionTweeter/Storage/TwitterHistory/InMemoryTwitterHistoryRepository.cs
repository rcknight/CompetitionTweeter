using System;
using System.Collections.Generic;

namespace CompetitionTweeter.Storage.TwitterHistory
{
    public class InMemoryTwitterHistoryRepository : ITwitterHistoryRepository
    {
        private readonly List<String> _retweets;
        private readonly List<String> _follows;

        public InMemoryTwitterHistoryRepository()
        {
            _retweets = new List<string>();
            _follows = new List<string>();
        }

        public bool HasRetweeted(string statusId)
        {
            lock (_retweets)
            {
                return _retweets.Contains(statusId);    
            }
        }

        public bool HasFollowed(string userId)
        {
            lock (_follows)
            {
                return _follows.Contains(userId);
            }
        }

        public void RecordFollow(string userId)
        {
            lock (_follows)
            {
                _follows.Add(userId);    
            }
        }

        public void RecordReTweet(string statusId)
        {
            lock (_retweets)
            {
                _retweets.Add(statusId);
            }
        }
    }
}
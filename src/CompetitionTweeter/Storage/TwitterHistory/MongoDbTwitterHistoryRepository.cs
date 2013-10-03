using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace CompetitionTweeter.Storage.TwitterHistory
{
    public class MongoDbTwitterHistoryRepository : ITwitterHistoryRepository
    {
        private readonly MongoDatabase _db;

        public MongoDbTwitterHistoryRepository(MongoDatabase mongoDb)
        {
            _db = mongoDb;
        }

        public bool HasRetweeted(string statusId)
        {
            var doc = FollowCollection().FindOneById(statusId);
            return doc != null;
        }

        public bool HasFollowed(string userId)
        {
            var doc = FollowCollection().FindOneById(userId);
            return doc != null;
        }

        public void RecordFollow(string userId)
        {
            FollowCollection().Save(new DBFollow(userId));
        }

        public void RecordReTweet(string statusId)
        {
            RetweetCollection().Save(new DBRetweet(statusId));
        }

        private MongoCollection<DBFollow> FollowCollection()
        {
            return _db.GetCollection<DBFollow>("Follows");
        }

        private MongoCollection<DBRetweet> RetweetCollection()
        {
            return _db.GetCollection<DBRetweet>("Retweets");
        }

        public class DBFollow
        {
            public String Id { get; set; }
            public string UserId { get; set; }

            public DBFollow(string userId)
            {
                Id = userId;
                UserId = userId;
            }
        }

        public class DBRetweet
        {
            public String Id { get; set; }
            public string StatusId { get; set; }

            public DBRetweet(string statusId)
            {
                Id = statusId;
                StatusId = statusId;
            }
        }
    }


}
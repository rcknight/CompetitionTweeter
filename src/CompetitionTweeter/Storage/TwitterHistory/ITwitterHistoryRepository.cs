namespace CompetitionTweeter.Storage.TwitterHistory
{
    public interface ITwitterHistoryRepository
    {
        bool HasRetweeted(string statusId);
        bool HasFollowed(string userId);
        void RecordFollow(string userId);
        void RecordReTweet(string statusId);
    }
}
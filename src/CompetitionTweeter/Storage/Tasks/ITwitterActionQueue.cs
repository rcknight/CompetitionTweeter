using System;
using CompetitionTweeter.DTO;

namespace CompetitionTweeter.Storage.Tasks
{
    public interface ITwitterActionQueue
    {
        void EnqueueRetweet(string statusId);
        void EnqueueRetweet(string statusId, string source);
        void EnqueueFollow(string userId);
        void EnqueueFollow(string userId, string source);

        bool TryPerformTask(Action<TwitterAction> action);
    }
}
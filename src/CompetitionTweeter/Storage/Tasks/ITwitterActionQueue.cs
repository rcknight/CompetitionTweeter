using System;
using CompetitionTweeter.DTO;

namespace CompetitionTweeter.Storage.Tasks
{
    public interface ITwitterActionQueue
    {
        void EnqueueRetweet(string statusId);
        void EnqueueFollow(string userId);

        bool TryPerformTask(Action<TwitterAction> action);
    }
}
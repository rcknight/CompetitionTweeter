using System;
using CompetitionTweeter.Storage.Tasks;
using Quartz;

namespace CompetitionTweeter.Jobs.Scraping
{
    [DisallowConcurrentExecution]
    public class TwitterScraper : IJob
    {
        public TwitterScraper(ITwitterActionQueue queue)
        {
            
        }

        public void Execute(IJobExecutionContext context)
        {
            Console.WriteLine("Twitter scrape executed");
            if (context.Trigger.StartTimeUtc < DateTime.UtcNow.AddMinutes(-10))
            {
                //_logger.Error("Delayed job execution, ignoring");
                return;
            }
        }
    }
}

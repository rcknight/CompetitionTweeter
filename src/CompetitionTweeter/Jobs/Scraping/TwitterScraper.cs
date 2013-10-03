using System;
using CompetitionTweeter.Storage.Tasks;
using Quartz;

namespace CompetitionTweeter.Jobs.Scraping
{
    public class TwitterScraper : IJob
    {
        public TwitterScraper(ITwitterActionQueue queue)
        {
            
        }

        public void Execute(IJobExecutionContext context)
        {
            Console.WriteLine("Twitter scrape executed");
        }
    }
}

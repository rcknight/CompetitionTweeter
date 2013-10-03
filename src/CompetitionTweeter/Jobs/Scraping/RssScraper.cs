using System;
using System.ServiceModel.Syndication;
using System.Xml;
using CompetitionTweeter.Storage.Tasks;
using Quartz;
using log4net;

namespace CompetitionTweeter.Jobs.Scraping
{
    public class RssScraper : IJob
    {
        private ITwitterActionQueue _queue;
        private ILog _logger = LogManager.GetLogger("RSS Scraper");

        public RssScraper(ITwitterActionQueue queue)
        {
            _queue = queue;
        }

        public void Execute(IJobExecutionContext context)
        {
            try
            {
                _logger.Info("RSS Parser started");
                throw new InvalidOperationException("You cant do this!");
                _logger.Info("RSS Parser Started");
                var reader = XmlReader.Create("http://forums.moneysavingexpert.com/external.html?type=rss2&forumids=72");
                var threads = SyndicationFeed.Load(reader);
            }
            catch (Exception ex)
            {
                
                _logger.Error("Something went wrong while parsing",ex);
                
            }
        }
    }
}

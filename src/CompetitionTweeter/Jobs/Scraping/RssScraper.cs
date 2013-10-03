using System;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Xml;
using CompetitionTweeter.HtmlParser;
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
                _logger.Info("RSS Parser starting");
                
                var reader = XmlReader.Create("http://forums.moneysavingexpert.com/external.html?type=rss2&forumids=72");
                var threads = SyndicationFeed.Load(reader);
                if (threads == null)
                {
                    _logger.Error("Feed could not be parsed, threads null");
                    return;
                }

                var interestingThreads = threads.Items.Where(i => IsInterestingTitle(i.Title.Text));

                foreach (var thread in interestingThreads)
                {
                    //get html summary
                    var extension = thread.ElementExtensions.FirstOrDefault(e => e.OuterName.Equals("encoded"));
                    if (extension == null)
                    {
                        _logger.ErrorFormat("No encoded text found for thread {0}", thread.Links[0].Uri.ToString());
                        return;
                    }

                    var html = extension.GetObject<String>();

                    var links = LinkFinder.Find(html);
                    var twitterLinks = links.Where(l => l.Href.ToLower().Contains("twitter.com/") && !l.Href.ToLower().Contains("twitter.com/search")).Select(l => l.Href.ToLower()).ToList();

                    foreach (var url in twitterLinks)
                    {
                        var split = url.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (split.Count() != 5 && split.Count() != 3)
                        {
                            _logger.DebugFormat("Malformed Link: {0}", url);
                            return;
                        }

                        if (split.Count() == 5 && url.Contains("status"))
                        {
                            //retweet
                            var statusId = split[4];
                            ulong result;
                            if(ulong.TryParse(statusId, out result))
                                _queue.EnqueueRetweet(statusId, thread.Links[0].Uri.ToString());
                        }

                        _queue.EnqueueFollow(split[2], thread.Links[0].Uri.ToString());
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.Error("Something went wrong while parsing",ex);   
            }

            _logger.Info("RSS Parser complete");
        }

        private bool IsInterestingTitle(string title)
        {
            title = title.ToLower();
            if (title.Contains("(fb)") || title.Contains("fb ") || title.Contains("fb)") || title.Contains("(fb") || title.Contains("facebook"))
                return false;
            if (title.Contains("twitter") || title.Contains("(tw)"))
                return true;

            return false;
        }
    }
}

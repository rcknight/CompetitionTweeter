using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Xml;

namespace Sources
{
    public class RssCompetitionSource : CompetitionSource
    {
        public RssCompetitionSource(int checkInterval) : base(checkInterval, "RSS Source")
        {
        }

        protected override IEnumerable<Competition> GetCompetitions()
        {
            SyndicationFeed feed = null;

            var reader = XmlReader.Create("http://forums.moneysavingexpert.com/external.html?type=rss2&forumids=72");
            feed = SyndicationFeed.Load(reader);

            if (feed == null)
            {
                _logger.Error("Feed could not be parsed, threads null");
                yield break;
            }

            var interestingThreads = feed.Items.Where(i => IsInterestingTitle(i.Title.Text));

            foreach (var thread in interestingThreads)
            {
                //get html summary
                var extension = thread.ElementExtensions.FirstOrDefault(e => e.OuterName.Equals("encoded"));
                if (extension == null)
                {
                    _logger.ErrorFormat("No encoded text found for thread {0}", thread.Links[0].Uri.ToString());
                    continue;
                }

                var html = extension.GetObject<String>();

                var links = LinkFinder.Find(html);
                var twitterLinks =
                    links.Where(
                        l =>
                            l.Href.ToLower().Contains("twitter.com/") &&
                            !l.Href.ToLower().Contains("twitter.com/search")).Select(l => l.Href.ToLower()).ToList();

                foreach (var url in twitterLinks)
                {
                    var split = url.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
                    if (split.Count() != 5 || !url.Contains("status")) continue;
                    
                    var statusId = split[4];
                    ulong result;
                    if (ulong.TryParse(statusId, out result))
                    {
                        yield return new Competition(result, split[2], String.Format("RSS ({0})", thread.Id), thread.Title.Text);
                    }
                }
            }
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
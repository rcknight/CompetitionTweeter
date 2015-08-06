using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net.Config;
using Sinks;
using Sources;

namespace TweeterHost
{
    class Program
    {
        private static readonly string AccessToken = ConfigurationManager.AppSettings["TWITTER_ACCESS_TOKEN"];
        private static readonly string AccessTokenSecret = ConfigurationManager.AppSettings["TWITTER_ACCESS_TOKEN_SECRET"];
        private static readonly string ConsumerKey = ConfigurationManager.AppSettings["TWITTER_CONSUMER_KEY"];
        private static readonly string ConsumerSecret = ConfigurationManager.AppSettings["TWITTER_CONSUMER_SECRET"];

        static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            var twitterSearches = new List<Tuple<string, bool>>()
            {
                new Tuple<string, bool>("RT follow win", true),
                new Tuple<string, bool>("retweet follow win", true),
                new Tuple<string, bool>("RT follow win UK", false),
                new Tuple<string, bool>("retweet follow win UK", false),
            };

            //follow some search terms
            var searchFrequency = Decimal.Floor((((15*60*1000)/ 100) * twitterSearches.Count));
            var searchSources = twitterSearches.Select(s => 
                new TwitterSearchCompetitionSource((int)searchFrequency, s.Item1, s.Item2, ConsumerKey, ConsumerSecret, AccessToken, AccessTokenSecret)).ToList();

            //follow the moneysavingexpert forum
            var rssSource = new RssCompetitionSource(60000);

            //for now just one tweeter
            //TODO: Multiplex accounts
            var tweeter = new TwitterAccount("RichK1986", ConsumerKey, ConsumerSecret, AccessToken, AccessTokenSecret);

            var toEnter = rssSource
                             .Merge(searchSources.Merge())
                             .Distinct(c => c.Retweet);

            toEnter.Subscribe(tweeter);

            foreach (var search in searchSources)
            {
                search.Start();
                Thread.Sleep((int)(searchFrequency / twitterSearches.Count));
            }

            rssSource.Start();

            new ManualResetEvent(false).WaitOne();           
        }
    }
}

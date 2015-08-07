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

        private static readonly string[] BlackListedTerms =
            ConfigurationManager.AppSettings["BLACKLISTED_TERMS"].ToLower().Split(',');

        private static readonly string[] BlackListedUsers =
            ConfigurationManager.AppSettings["BLACKLISTED_USERS"].ToLower().Split(',');

        private static readonly string[] Searches =
            ConfigurationManager.AppSettings["SEARCH_QUERIES"].Split(',');

        static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            //follow some search terms
            var searchFrequency = (int)Decimal.Floor((((15*60*1000)/ 100) * (Searches.Length * 2)));
            var searchSources = Searches.SelectMany(search => new List<TwitterSearchCompetitionSource>
            {
                new TwitterSearchCompetitionSource(searchFrequency, search.Trim(), BlackListedUsers.ToList(), BlackListedTerms.ToList(), true, ConsumerKey, ConsumerSecret, AccessToken, AccessTokenSecret),
                new TwitterSearchCompetitionSource(searchFrequency, search.Trim() + " UK", BlackListedUsers.ToList(), BlackListedTerms.ToList(), false, ConsumerKey, ConsumerSecret, AccessToken, AccessTokenSecret),
            }).ToList();

            //follow the moneysavingexpert forum
            var rssSource = new RssCompetitionSource(60000);

            var toEnter = rssSource
                             .Merge(searchSources.Merge()
                                                 .Where(c => c.WasRetweet)) //filtering non retweets to reduce false positives
                             .Distinct(c => c.Retweet);

            //also print out the skipped retweets 
            searchSources.Merge().Distinct(c => c.Retweet).Where(c => !c.WasRetweet).Subscribe(c =>
            {
                Console.WriteLine("Skipping Original tweet");
                Console.WriteLine(c);
                Console.WriteLine(c.Text);
            });

            //for now just one tweeter
            //TODO: Multiplex accounts
            toEnter.Subscribe(new TwitterAccount("RichK1986", ConsumerKey, ConsumerSecret, AccessToken, AccessTokenSecret));
            //toEnter.Subscribe(Console.WriteLine);

            foreach (var search in searchSources)
            {
                search.Start();
                Thread.Sleep((int)(searchFrequency / searchSources.Count()));
            }

            rssSource.Start();

            new ManualResetEvent(false).WaitOne();           
        }
    }
}

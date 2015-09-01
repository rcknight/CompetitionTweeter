﻿using System;
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
        private static readonly string AccessToken = ConfigurationManager.AppSettings["SEARCH_ACCESS_TOKEN"];
        private static readonly string AccessTokenSecret = ConfigurationManager.AppSettings["SEARCH_ACCESS_TOKEN_SECRET"];
        private static readonly string ConsumerKey = ConfigurationManager.AppSettings["SEARCH_CONSUMER_KEY"];
        private static readonly string ConsumerSecret = ConfigurationManager.AppSettings["SEARCH_CONSUMER_SECRET"];

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
            var searchSources = Searches.SelectMany(search =>
            {
                //special case the highest volume search
                var freq = search.Trim() == "RT win" ? 20000 : searchFrequency;

                return new List<TwitterSearchCompetitionSource>
                {
                    new TwitterSearchCompetitionSource(freq, search.Trim(), BlackListedUsers.ToList(),
                        BlackListedTerms.ToList(), true, ConsumerKey, ConsumerSecret, AccessToken, AccessTokenSecret),
                    new TwitterSearchCompetitionSource(freq, search.Trim() + " UK", BlackListedUsers.ToList(),
                        BlackListedTerms.ToList(), false, ConsumerKey, ConsumerSecret, AccessToken, AccessTokenSecret),
                };
            }).ToList();

            //follow the moneysavingexpert forum
            var rssSource = new RssCompetitionSource(60000);

            var toEnter = rssSource
                             .Merge(searchSources.Merge())
                             .Distinct(c => c.Retweet);

            //for now just one tweeter
            //TODO: Multiplex accounts


            var sinkConfigs = ConfigurationManager.AppSettings.AllKeys.Where(k => k.StartsWith("TWITTER_SINK"));

            var accounts = sinkConfigs.Select(key =>
            {
                var conf = ConfigurationManager.AppSettings[key].Split(',');
                var accountName = conf[0];
                var consumerKey = conf[1];
                var consumerSecret = conf[2];
                var accessToken = conf[3];
                var accessTokenSecret = conf[4];
                return new TwitterAccount(accountName, consumerKey, consumerSecret, accessToken, accessTokenSecret);
            });

            //Multiplexing Dispatch
            toEnter.Subscribe(competition =>
            {
                //if one account already follows, dispatch to that one
                var existing = accounts.Where(a => a.Follows(competition.Follow)).ToList();
                if (existing.Any())
                {
                    existing.First().OnNext(competition);
                    return;
                }

                accounts.OrderBy(o => Guid.NewGuid()).First().OnNext(competition);
            });
            

            //Kick off the twitter searches
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

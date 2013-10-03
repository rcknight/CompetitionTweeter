using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using Blacksmith.Core;
using CompetitionTweeter.Jobs;
using CompetitionTweeter.Jobs.Scraping;
using CompetitionTweeter.Storage;
using CompetitionTweeter.Storage.Tasks;
using CompetitionTweeter.Storage.TwitterHistory;
using LinqToTwitter;
using MongoDB.Driver;
using Quartz;
using Quartz.Impl;
using TinyIoC;

namespace CompetitionTweeter
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            ConfigureIoc();

            var schedFact = new StdSchedulerFactory();
            var scheduler = schedFact.GetScheduler();

            //BootStrapTwitterHistoryFromFile(TinyIoCContainer.Current.Resolve<ITwitterHistoryRepository>());

            scheduler.JobFactory = new InjectingJobFactory();
            scheduler.Start();

            var rssScraperJob = JobBuilder.Create<RssScraper>().Build();
            var rssScraperTrigger =
                TriggerBuilder.Create().WithSimpleSchedule(x => x.WithIntervalInSeconds(10).RepeatForever()).Build();

            scheduler.ScheduleJob(rssScraperJob, rssScraperTrigger);

            Console.ReadLine();
        }

        private static void ConfigureIoc()
        {
            var container = TinyIoCContainer.Current;
            container.Register(ConfigureIronMq());
            container.Register(ConfigureMongo());
            container.Register(ConfigureTwitterAuth());
            container.Register<ITwitterHistoryRepository, MongoDbTwitterHistoryRepository>();
            container.Register<ITwitterActionQueue, InMemoryTwitterActionQueue>();
            container.Register<RssScraper>();
            container.Register<TwitterScraper>();
        }

        private static MongoDatabase ConfigureMongo()
        {
            var url = new MongoUrl(ConfigurationManager.AppSettings["MONGOLAB_URI"]);
            var client = new MongoClient(url);
            var server = client.GetServer();
            var db = server.GetDatabase(url.DatabaseName);
            return db;
        }

        private static Client ConfigureIronMq()
        {
            var projId = ConfigurationManager.AppSettings["IRON_MQ_PROJECT_ID"];
            var token = ConfigurationManager.AppSettings["IRON_MQ_TOKEN"];
            return new Client(projId, token);
        }

        private static ITwitterAuthorizer ConfigureTwitterAuth()
        {
            return new SingleUserAuthorizer()
                {
                    Credentials = new SingleUserInMemoryCredentials()
                        {
                            ConsumerKey =
                                ConfigurationManager.AppSettings["TWITTER_CONSUMER_KEY"],
                            ConsumerSecret =
                                ConfigurationManager.AppSettings["TWITTER_CONSUMER_SECRET"],
                            TwitterAccessToken =
                                ConfigurationManager.AppSettings["TWITTER_ACCESS_TOKEN"],
                            TwitterAccessTokenSecret =
                                ConfigurationManager.AppSettings["TWITTER_ACCESS_TOKEN_SECRET"]
                        }
                };
        }


        private static string fileName = "entered.txt";
        private static void BootStrapTwitterHistoryFromFile(ITwitterHistoryRepository repo)
        {
            if (File.Exists(fileName))
            {
                var lines = File.ReadAllLines(fileName);

                foreach (var line in lines)
                {
                    ulong res = 0;
                    var parts = line.Split(new [] {'/'}, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Count() == 3)
                    {
                        //follow
                        repo.RecordFollow(parts[2].ToLower().Trim());
                    } else if (parts.Count() == 5 && line.ToLower().Contains("status") && ulong.TryParse(parts[4].Trim(), out res))
                    {
                        //make sure it is a stauts
                        repo.RecordReTweet(parts[4].Trim().ToLower());
                    }
                    else
                    {
                        Console.WriteLine("Line not recognised");
                        Console.WriteLine(line);
                    }
                }
            }
            else
            {
                Console.WriteLine("File Not Found");
            }
        }
    }
}

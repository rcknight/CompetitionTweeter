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
using CompetitionTweeter.DTO;
using CompetitionTweeter.Jobs;
using CompetitionTweeter.Jobs.Scraping;
using CompetitionTweeter.Jobs.Stats;
using CompetitionTweeter.Jobs.TwitterActions;
using CompetitionTweeter.Storage;
using CompetitionTweeter.Storage.Tasks;
using CompetitionTweeter.Storage.TwitterHistory;
using MongoDB.Driver;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Triggers;
using TinyIoC;
using TwitterToken;

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
                TriggerBuilder.Create().WithSimpleSchedule(x => x.WithIntervalInSeconds(120).RepeatForever()).Build();

            var twitterScraperJob = JobBuilder.Create<TwitterScraper>().Build();
            var twitterScraperTrigger =
                TriggerBuilder.Create().WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever()).Build();

            var rateLimitLoggerJob = JobBuilder.Create<TwitterRateLimitLogger>().Build();
            var twitterRateLimitTrigger =
                TriggerBuilder.Create().WithSimpleSchedule(x => x.WithIntervalInMinutes(1).RepeatForever()).Build();

            var twitterActionJob = JobBuilder.Create<TwitterActionHandler>().Build();
            var twitterActionTriggers = new Quartz.Collection.HashSet<ITrigger>()
                {
                    BuildSpecificTimeTrigger(08,30),
                    BuildSpecificTimeTrigger(11,00),
                    BuildSpecificTimeTrigger(14,00),
                    BuildSpecificTimeTrigger(16,30),
                    BuildSpecificTimeTrigger(20,00)
                };

            //scheduler.ScheduleJob(rssScraperJob, rssScraperTrigger);
            scheduler.ScheduleJob(twitterActionJob, twitterActionTriggers, true);
            //scheduler.ScheduleJob(twitterScraperJob, twitterScraperTrigger);
            //scheduler.ScheduleJob(rateLimitLoggerJob, twitterRateLimitTrigger);
            new ManualResetEvent(false).WaitOne();
        }

        private static ITrigger BuildSpecificTimeTrigger(int hours, int mins)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
            if (timeZone == null)
                timeZone = TimeZoneInfo.Utc;

            return TriggerBuilder.Create()
                              .WithDailyTimeIntervalSchedule(
                                  x =>
                                  x.StartingDailyAt(TimeOfDay.HourAndMinuteOfDay(hours, mins)).OnEveryDay()
                                   .InTimeZone(timeZone)
                                   .WithMisfireHandlingInstructionDoNothing()
                                   .WithRepeatCount(0)).Build();


        }

        private static void ConfigureIoc()
        {
            var container = TinyIoCContainer.Current;
            container.Register(ConfigureIronMq());
            container.Register(ConfigureMongo());
            container.Register(ConfigureTwitterAuth());
            container.Register<ITwitterHistoryRepository, MongoDbTwitterHistoryRepository>();
            container.Register<ITwitterActionQueue, IronMqTwitterActionQueue>();
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
            ConfigurationWrapper.QueueNameMappings = new Dictionary<Type, string>();
            ConfigurationWrapper.QueueNameMappings.Add(typeof(TwitterAction), "TwitterActions");
            return new Client(projId, token);
        }

        private static Token ConfigureTwitterAuth()
        {
            var token = new Token(ConfigurationManager.AppSettings["TWITTER_ACCESS_TOKEN"],
                                  ConfigurationManager.AppSettings["TWITTER_ACCESS_TOKEN_SECRET"],
                                  ConfigurationManager.AppSettings["TWITTER_CONSUMER_KEY"],
                                  ConfigurationManager.AppSettings["TWITTER_CONSUMER_SECRET"]);

            return token;
        }


        private static string fileName = "entered.txt";
        private static void BootStrapTwitterHistoryFromFile(ITwitterHistoryRepository repo)
        {
            if (File.Exists(fileName))
            {
                var lines = File.ReadAllLines(fileName);

                foreach (var line in lines)
                {
                    if (line.ToLower().Contains("search"))
                        continue;
                    ulong res = 0;
                    var parts = line.Split(new [] {'/'}, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Count() == 3)
                    {
                        //follow
                        repo.RecordFollow(parts[2].ToLower().Trim());
                    } else if (parts.Count() == 5 && line.ToLower().Contains("status") && ulong.TryParse(parts[4].Trim(), out res))
                    {
                        //make sure it is a stauts
                        repo.RecordFollow(parts[2].ToLower().Trim());
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

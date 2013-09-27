using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToTwitter;

namespace StatsProducer
{
    class Program
    {
        private static SingleUserAuthorizer auth;

        static void Main(string[] args)
        {
            auth = new SingleUserAuthorizer()
            {
                Credentials = new SingleUserInMemoryCredentials()
                {
                    ConsumerKey =
                        ConfigurationManager.AppSettings["token_ConsumerKey"],
                    ConsumerSecret =
                        ConfigurationManager.AppSettings["token_ConsumerSecret"],
                    TwitterAccessToken =
                        ConfigurationManager.AppSettings["token_AccessToken"],
                    TwitterAccessTokenSecret =
                        ConfigurationManager.AppSettings["token_AccessTokenSecret"]
                }
            };

            var tweets = GetAllTweets().Where(t => t.RetweetedStatus != null && t.RetweetedStatus.StatusID != null).ToList();
            var todayCount = tweets.Where(t => t.CreatedAt.Date.Equals(DateTime.Today)).Count();
            tweets = tweets.Where(t => t.CreatedAt < DateTime.Today).ToList();

            Console.WriteLine("{0} Entered today (ignoring)", todayCount);
            

            decimal totalWins = 0;

            foreach (var tweet in tweets)
            {
                totalWins += (decimal)1/(decimal)tweet.RetweetCount;
            }


            decimal totalProbOfLosing = 0;

            foreach (var status in tweets)
            {
                decimal losingProbability = (((decimal)status.RetweetCount - 1) / (decimal)status.RetweetCount);
                if (totalProbOfLosing != 0)
                {
                    totalProbOfLosing *= losingProbability;
                }
                else
                {
                    totalProbOfLosing = losingProbability;
                }
            }

            var totalProbOfWinning = 1 - totalProbOfLosing;
            
            Console.WriteLine("Actual expected wins so far: {0:0.###}", totalWins);
            Console.WriteLine("Total probability of having won something so far: {0:0.##}",totalProbOfWinning);
            
            var earliestTweet = tweets.Min(t => t.CreatedAt);
            var latestTweet = tweets.Max(t => t.CreatedAt);
            var tweetsperiod = (latestTweet - earliestTweet).TotalDays;
            var tweetsPerDay = tweets.Count/tweetsperiod;
            var tweetsPerWeek = (decimal)tweetsPerDay*7;
            var tweetsPerMonth = (decimal)tweetsPerDay*30;

            Console.WriteLine("Total other entrants: {0}", tweets.Sum(t => t.RetweetCount));

            

            Console.WriteLine("Based on {0} retweets over the last {1:0.###} days with {2:0.###} average entries.", tweets.Count, tweetsperiod, (decimal)tweets.Sum(t => t.RetweetCount) / (decimal)tweets.Count);
            Console.WriteLine("Average probability of winning something so far: {0:0.###}", tweets.Count / ((decimal)tweets.Sum(t => t.RetweetCount) / tweets.Count));
            Console.WriteLine("Average expected wins per week: {0:0.###}", tweetsPerWeek / ((decimal)tweets.Sum(t => t.RetweetCount) / tweets.Count));
            Console.WriteLine("Average expected wins per month: {0:0.###}", tweetsPerMonth / ((decimal)tweets.Sum(t => t.RetweetCount) / tweets.Count));

            Console.ReadLine();

        }

        private static List<Status> GetAllTweets()
        {
            var allTweets = new List<Status>();

            using (var twitter = new TwitterContext(auth))
            {
                int lastCount = 199;
                var oldestId = ulong.MaxValue;
                while (lastCount == 199)
                {
                    IQueryable<Status> statusTweets =
                        twitter.Status.Where(tweet => tweet.Type == StatusType.User
                                                      && tweet.IncludeRetweets == true
                                                      && tweet.Count == 199);

                    if (oldestId != ulong.MaxValue)
                        statusTweets = statusTweets.Where(t => t.MaxID == oldestId);

                    var returned = statusTweets.ToList();
                    lastCount = returned.Count();
                    oldestId = returned.Min(t => ulong.Parse(t.StatusID));
                    allTweets.AddRange(statusTweets);
                }
            }

            return allTweets;
        }
    }
}

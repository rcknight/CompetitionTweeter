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

            RemoveBadFollowers();

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

        private static void RemoveBadFollowers()
        {
            var followers = GetAllFollowers();

            var eggUsers = followers.Where(f => f.ProfileImageUrl.Contains("default"));
            var unfollowCandidates =
                followers.Where(
                    f => (!string.IsNullOrEmpty(f.Description) && f.Description != Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(f.Description))) ||
                    f.ProfileImageUrl.Contains("default") || f.StatusesCount == 0 || f.FollowersCount == 0 ||
                    f.Name != Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(f.Name)));

            Console.WriteLine("Followers: {0}", followers.Count);
            Console.WriteLine("Eggs: {0}", eggUsers.Count());
            Console.WriteLine("Actual Pictures: {0}", followers.Count - eggUsers.Count());
            Console.WriteLine("With tweets: {0}", followers.Where(f => f.StatusesCount > 0).Count());
            Console.WriteLine("With followers: {0}", followers.Where(f => f.FollowersCount > 0).Count());
            Console.WriteLine("Non-ascii chars: {0}", followers.Select(f => f.Name).Where(s => s != Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(s))).Count());
            Console.WriteLine("Non-ascii description: {0}", followers.Where(f => !string.IsNullOrEmpty(f.Description) && f.Description != Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(f.Description))).Count());
            Console.WriteLine("Non-egg ascii only users: {0}", followers.Where(f => !f.ProfileImageUrl.Contains("default") && f.Name == Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(f.Name))).Count());
            Console.WriteLine("Non-egg ascii only users with tweets and followers > 5: {0}", followers.Where(f => f.FollowersCount > 0 && f.StatusesCount > 0 && !f.ProfileImageUrl.Contains("default") && f.Name == Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(f.Name)) && (string.IsNullOrEmpty(f.Description) || f.Description == Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(f.Description)))).Count());
            Console.WriteLine("Candidates for unfollowing: {0}", unfollowCandidates.Count());

            Console.WriteLine("Press Enter to unfollow");
            Console.ReadLine();
            using (var twitter = new TwitterContext(auth))
            {
                foreach (var unfollow in unfollowCandidates)
                {
                    Console.WriteLine("Blocking {2}, ({1}) ({0})", unfollow.Identifier.ID, unfollow.Identifier.ScreenName, unfollow.Name);
                    twitter.CreateBlock(ulong.Parse(unfollow.Identifier.ID), null, false, false);
                }
            }
        }

        private static List<User> GetAllFollowers()
        {
            var allUsers = new List<User>();

            using (var twitter = new TwitterContext(auth))
            {
                var userIds = (from friend in twitter.SocialGraph
                               where friend.Type == SocialGraphType.Followers &&
                                     friend.ScreenName == "RichK1986"
                               select friend).SingleOrDefault().IDs;

                string cursor = "-1";

                var usersSoFar = 0;
                var userSliceSize = 100;

                while (usersSoFar < userIds.Count)
                {
                    string queryUsers = "";
                    for (int i = 0; i < userSliceSize; i++)
                    {
                        if (usersSoFar + i < userIds.Count)
                        {
                            queryUsers += userIds[usersSoFar + i] + ",";
                        }
                    }
                    usersSoFar += 100;

                    var users =
                        (from user in twitter.User
                         where user.Type == UserType.Lookup &&
                               user.UserID == queryUsers
                         select user)
                         .ToList();

                    allUsers.AddRange(users);
                    //cursor = userIds.CursorMovement.Next
                }

                return allUsers;
            }
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
                    allTweets.AddRange(returned);
                }
            }

            return allTweets;
        }
    }
}

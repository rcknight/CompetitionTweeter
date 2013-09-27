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
using LinqToTwitter;
using Timer = System.Timers.Timer;

namespace CompetitionTweeter
{
    class Program
    {
        private static SingleUserAuthorizer auth;

        private static List<String> enteredBefore = new List<string>();

        private static ConcurrentQueue<string> _linksToProcess = new ConcurrentQueue<string>(); 

        static void Main(string[] args)
        {
            if(File.Exists("entered.txt"))
                enteredBefore = File.ReadAllLines("entered.txt").ToList();

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

            var timer = new Timer(120000);
            timer.Elapsed += TimerOnElapsed;
            timer.Start();

            TimerOnElapsed(null, null);

            ProcessLinks();

            Console.ReadLine();
        }


        private static void ProcessLinks()
        {
            string dequeued;
            
            while (true)
            {
                if (_linksToProcess.TryDequeue(out dequeued))
                {
                    HandleLink(dequeued);
                }
                else
                {
                    Thread.Sleep(2000);
                }
            }
        }

        static Random random = new Random();

        private static void TimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            var sleepFor = random.Next(30000, 60000);
            Console.WriteLine("\n{1} Timer elapsed, sleeping for: {0}", sleepFor, "[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]");
            Thread.Sleep(sleepFor);

            Console.WriteLine("Checking RSS");
            var reader = XmlReader.Create("http://forums.moneysavingexpert.com/external.html?type=rss2&forumids=72" + "&_cacheBuster=" + sleepFor);
            var threads = SyndicationFeed.Load(reader);
            
            if (threads == null)
            {
                Console.WriteLine("Error parsing feed?");
                return;
            }

            var interestingThreads = threads.Items.Where(i => IsInterestingTitle(i.Title.Text));

            Console.WriteLine("{0} interesting threads found", interestingThreads.Count());

            foreach (var thread in interestingThreads)
            {
                //get html summary
                var extension = thread.ElementExtensions.FirstOrDefault(e => e.OuterName.Equals("encoded"));
                if (extension == null)
                {
                    Console.WriteLine("No encoded text found");
                    return;
                }

                var html = extension.GetObject<String>();

                var links = LinkFinder.Find(html);
                var twitterLinks = links.Where(l => l.Href.Contains("twitter.com/")).Select(l => l.Href).ToList();
                var newLinks = twitterLinks.Where(l => !enteredBefore.Contains(l)).ToList();

                if (newLinks.Any())
                {
                    Console.WriteLine("{0}", thread.Title.Text);
                    Console.WriteLine("{0} links, {1} new.", twitterLinks.Count(), newLinks.Count());
                }

                foreach (var link in newLinks)
                {
                    _linksToProcess.Enqueue(link);
                }
            }
            reader.Close();
        }

        private static bool IsInterestingTitle(string title)
        {
            title = title.ToLower();
            if (title.Contains("(fb)") || title.Contains("fb ") || title.Contains("fb)") || title.Contains("(fb") || title.Contains("facebook"))
                return false;
            if (title.Contains("twitter") || title.Contains("(tw)"))
                return true;

            return false;
        }

        private const int MAX_RETRIES = 5;

        private static void HandleLink(string url)
        {
            if (enteredBefore.Contains(url))
            {
                Console.WriteLine("Previously entered link passed to consumer, skipping");
                return;
            }

            File.AppendAllLines("entered.txt", new List<string>() {url});
            enteredBefore.Add(url);

            if (!url.Contains("twitter.com"))
            {
                Console.WriteLine("Ignoring non twitter url {0}", url);
                return;
            }

            if (url.Contains("twitter.com/search"))
            {
                Console.WriteLine("Ignoring twitter search url {0}", url);
                return;
            }

            Console.WriteLine("\nHandling: {0}", url);

            var sleepFor = random.Next(0, 10000);
            Console.WriteLine("Before handling link, sleeping for: {0}", sleepFor);
            Thread.Sleep(sleepFor);

            var split = url.Split(new[] { '/' } , StringSplitOptions.RemoveEmptyEntries);

            if (split.Count() != 5 && split.Count() != 3)
            {
                Console.WriteLine("Malformed Link: {0}", url);
                return;
            }

            bool retweeted = false;
            var retweetExceptions = new List<Exception>();
            if (split.Count() == 5)
            {
                for (int i = 0; i < MAX_RETRIES; i++)
                {
                    try
                    {
                        //probably a tweet url
                        Retweet(split[4]);
                        retweeted = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        retweetExceptions.Add(ex);
                        Console.WriteLine("Error retweeting {0}: {1}{2}", url, Environment.NewLine, ex.Message);
                    }
                    Console.WriteLine("Retrying in 5s");
                    Thread.Sleep(5000);
                }
            }
            else
            {
                //dont need to retweet, so dont count this as an error
                retweeted = true;
            }

            bool followed = false;
            List<Exception> followExceptions = new List<Exception>();
            for (int i = 0; i < MAX_RETRIES; i++)
            {
                try
                {
                    FollowUser(split[2]);
                    followed = true;
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error following {0}: {1}{2}", url, Environment.NewLine, ex.Message);
                    followExceptions.Add(ex);
                }
                Console.WriteLine("Retrying in 5s");
                Thread.Sleep(5000);
            }

            var errorsToAppend = new List<String>();
            if (followed == false)
            {
                errorsToAppend.Add(string.Format("Error following: {0}", url));
                followExceptions = followExceptions.Distinct().ToList();
                foreach (var followException in followExceptions)
                {
                    errorsToAppend.Add(String.Format("\t{0} - {1}",followException.GetType().ToString(), followException.Message));
                }
            }

            if (retweeted == false)
            {
                errorsToAppend.Add(string.Format("Error retweeting: {0}", url));
                retweetExceptions = retweetExceptions.Distinct().ToList();
                foreach (var retweetException in retweetExceptions)
                {
                    errorsToAppend.Add(String.Format("\t{0} - {1}", retweetException.GetType().ToString(), retweetException.Message));
                }
            }

            if (!followed || !retweeted)
            {
                File.AppendAllLines("errors.txt", errorsToAppend);    
            }
        }

        static void FollowUser(string userName)
        {
            using (var twitter = new TwitterContext(auth))
            {
                var myFriend = twitter.CreateFriendship(null, userName, true);
                Console.WriteLine("Followed user {0}", myFriend.Name);
            }
        }
        
        static void Retweet(string tweetId)
        {
            //parse the long just to make sure the url was valid
            using (var twitter = new TwitterContext(auth))
            {
                var result = from tweet in twitter.Status
                                where tweet.Type == StatusType.Show &&
                                    tweet.ID == tweetId
                                select tweet;

                var targetTweet = result.First();
                var myTweet = twitter.Retweet(targetTweet.ID);
                    
                Console.WriteLine("Posted retweet at https://twitter.com/RichK1985/status/{0}", myTweet.StatusID);
            }
        }
    }

    public struct LinkItem
    {
        public string Href;
        public string Text;

        public override string ToString()
        {
            return Href + "\n\t" + Text;
        }
    }

    static class LinkFinder
    {
        public static List<LinkItem> Find(string file)
        {
            List<LinkItem> list = new List<LinkItem>();

            // 1.
            // Find all matches in file.
            MatchCollection m1 = Regex.Matches(file, @"(<a.*?>.*?</a>)",
                RegexOptions.Singleline);

            // 2.
            // Loop over each match.
            foreach (Match m in m1)
            {
                string value = m.Groups[1].Value;
                LinkItem i = new LinkItem();

                // 3.
                // Get href attribute.
                Match m2 = Regex.Match(value, @"href=\""(.*?)\""",
                RegexOptions.Singleline);
                if (m2.Success)
                {
                    i.Href = m2.Groups[1].Value;
                }

                // 4.
                // Remove inner tags from text.
                string t = Regex.Replace(value, @"\s*<.*?>\s*", "",
                RegexOptions.Singleline);
                i.Text = t;

                list.Add(i);
            }
            return list;
        }
    }
}

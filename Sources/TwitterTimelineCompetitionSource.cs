using System;
using System.Collections.Generic;
using System.Linq;
using LinqToTwitter;

namespace Sources
{
    public class TwitterTimelineCompetitionSource : CompetitionSource
    {
        protected ulong _lastStatus = 1;
        private readonly string _screenName;
        private readonly InMemoryCredentialStore _credentialStore;

        public TwitterTimelineCompetitionSource(int checkInterval, string user, string consumerKey, string consumerSecret, string accessToken, string accessSecret) : base(checkInterval, "Twitter Source (" + user + ")")
        {
            _credentialStore = new InMemoryCredentialStore()
            {
                ConsumerKey = consumerKey,
                ConsumerSecret = consumerSecret,
                OAuthToken = accessToken,
                OAuthTokenSecret = accessSecret
            };

            _screenName = user;
        }

        protected override IEnumerable<Competition> GetCompetitions()
        {
            var auth = new SingleUserAuthorizer()
            {
                CredentialStore = _credentialStore
            };

            using (var ctx = new TwitterContext(auth))
            {
                var tweets = ctx.Status.Where(t => t.Type == StatusType.User && t.ScreenName == _screenName && t.Count == 200 && t.IncludeRetweets == true && t.ExcludeReplies == true);

                if (_lastStatus != 1)
                    tweets = tweets.Where(t => t.SinceID == _lastStatus);
                
                tweets = tweets.OrderByDescending(t => t.StatusID);

                var firstTweet = tweets.FirstOrDefault();
                if (_lastStatus == 1)
                {
                    _lastStatus = firstTweet != null ? firstTweet.StatusID : _lastStatus;
                    yield break;
                }
                _lastStatus = firstTweet != null ? firstTweet.StatusID : _lastStatus;

                foreach (var tweet in tweets)
                {
                    if (tweet.RetweetedStatus.User == null)
                        continue;
                    var screenName = tweet.RetweetedStatus.User.ScreenNameResponse;
                    if (!string.IsNullOrEmpty(screenName))
                    {
                        yield return
                            new Competition(tweet.RetweetedStatus.StatusID, screenName, String.Format("Twitter ({0})", screenName), tweet.Text);
                    }
                }
            }
        }
    }
}
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using LinqToTwitter;
using Sources;

namespace Sinks
{
    public class TwitterAccount : IObserver<Competition>
    {
        private readonly string _screenName;
        private readonly List<String> _following;
        private readonly List<ulong> _recentTweets; 
        private readonly InMemoryCredentialStore _credentialStore;

        protected readonly ILog _logger;

        public TwitterAccount(string screenName, string consumerKey, string consumerSecret, string accessToken, string accessSecret)
        {
            _logger = LogManager.GetLogger($"Twitter Sink ({screenName})");
            
            _screenName = screenName;
            _following = new List<string>();

            _credentialStore = new InMemoryCredentialStore()
            {
                ConsumerKey = consumerKey,
                ConsumerSecret = consumerSecret,
                OAuthToken = accessToken,
                OAuthTokenSecret = accessSecret
            };

            //on startup, need to bootstrap a list of followers, and recent tweets
            using (var ctx = new TwitterContext(new SingleUserAuthorizer() {CredentialStore = _credentialStore}))
            {
                var myTweets = ctx.Status.Where(t => t.Type == StatusType.User && t.ScreenName == _screenName && t.Count == 200 && t.IncludeRetweets == true && t.ExcludeReplies == true).ToList();

                _recentTweets = myTweets.Select(t => t.RetweetedStatus.StatusID).ToList();

                Friendship friendship = null;
                long cursor = -1;
                do
                {
                    try
                    {
                        friendship =
                            (from friend in ctx.Friendship
                                where friend.Type == FriendshipType.FriendsList &&
                                      friend.ScreenName == _screenName &&
                                      friend.Cursor == cursor &&
                                      friend.Count == 200
                                select friend)
                                .SingleOrDefault();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error loading friends list, probably rate limit, waiting 5 mins", ex);
                        Thread.Sleep(5*60*1000);
                    }

                    if (friendship?.Users == null || friendship.CursorMovement == null)
                        continue;

                    cursor = friendship.CursorMovement.Next;
                    friendship.Users.ForEach(friend => _following.Add(friend.ScreenNameResponse.ToLower()));
                } while (cursor != 0);
            }

            _logger.InfoFormat("Started up with {0} followers", _following.Count);
        }

        public void OnNext(Competition comp)
        {
            if (_recentTweets.Contains(comp.Retweet))
            {
                return;
            }

            if (!_following.Contains(comp.Follow.ToLower()))
            {
                Follow(comp.Follow.ToLower());
            }
            else
            {
                _following.Remove(comp.Follow.ToLower());
                _following.Insert(0, comp.Follow.ToLower());
            }

            Retweet(comp);
        }

        private void Follow(string screenName)
        {
            using (var ctx = new TwitterContext(new SingleUserAuthorizer() {CredentialStore = _credentialStore}))
            {
                try
                {
                    ctx.CreateFriendshipAsync(screenName, true).Wait();
                    _following.Insert(0, screenName.ToLower());
                }
                catch (Exception ex)
                {
                    _logger.Error("Error following user " + screenName, ex);
                }

                while (_following.Count > 1950)
                {
                    var toUnfollow = _following.Last();
                    _logger.InfoFormat("Follower count too high, unfollowing {0}", toUnfollow);
                    UnFollow(toUnfollow);
                }
            }
        }

        private void UnFollow(string screenName)
        {
            using (var ctx = new TwitterContext(new SingleUserAuthorizer() {CredentialStore = _credentialStore}))
            {
                try
                {
                    ctx.DestroyFriendshipAsync(screenName).Wait();
                    _following.Remove(screenName);
                }
                catch (Exception ex)
                {
                    _logger.Error("Error following user " + screenName, ex);
                }
            }
        }

        private int entered = 0;
        private void Retweet(Competition comp)
        {
            var statusId = comp.Retweet;
            using (var ctx = new TwitterContext(new SingleUserAuthorizer() { CredentialStore = _credentialStore }))
            {
                try
                {
                    ctx.RetweetAsync(statusId).Wait();
                    entered++;
                    _logger.InfoFormat("Entered Competition:\n {0}", comp);
                    if (entered%10 == 0)
                    {
                        _logger.InfoFormat("Entered {0} competitions since startup", entered);
                    }
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerExceptions.Count > 0 && ex.InnerExceptions[0].Message.Contains("already"))
                    {
                    }
                    else
                    {
                        _logger.Error("Error retweeting status " + statusId, ex);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Error retweeting status " + statusId, ex);
                }
            }
        }

        public void OnError(Exception error)
        {}

        public void OnCompleted()
        {}
    }
}

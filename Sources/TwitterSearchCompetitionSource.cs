using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using LinqToTwitter;

namespace Sources
{
    public class TwitterSearchCompetitionSource : CompetitionSource
    {
        private ulong _lastStatus = 1;
        private readonly InMemoryCredentialStore _credentialStore;
        private readonly string _query;
        private readonly bool _locationSearch;

        private readonly List<String> _blackListedUsers;
        private readonly List<String> _blackListedTerms;

        public TwitterSearchCompetitionSource(int checkInterval, string query, List<String> blacklistedUsers, List<String> blacklistedTerms, bool locationSearch, string consumerKey, string consumerSecret, string accessToken, string accessSecret) : base(checkInterval, "Twitter Search")
        {
            _query = query;
            _locationSearch = locationSearch;
            _blackListedUsers = blacklistedUsers;
            _blackListedTerms = blacklistedTerms;

            _credentialStore = new InMemoryCredentialStore()
            {
                ConsumerKey = consumerKey,
                ConsumerSecret = consumerSecret,
                OAuthToken = accessToken,
                OAuthTokenSecret = accessSecret
            };
        }

        protected override IEnumerable<Competition> GetCompetitions()
        {
            var auth = new SingleUserAuthorizer()
            {
                CredentialStore = _credentialStore
            };

            using (var ctx = new TwitterContext(auth))
            {
                var searchQuery = ctx.Search.Where(
                    s => s.Count == 200 
                    && s.Type == SearchType.Search 
                    && s.Query == _query
                    && s.IncludeEntities == true
                    && s.ResultType == ResultType.Recent
                );

                if (_locationSearch)
                    searchQuery = searchQuery.Where(s => s.GeoCode == "54.171278,-4.312134,700km");

                var searchResponse = searchQuery.SingleOrDefault();

                if (searchResponse == null || searchResponse.Statuses == null || searchResponse.Statuses.Count == 0)
                    yield break;

                foreach (var status in searchResponse.Statuses)
                {
                    var origStatus = status;
                    var isrt = false;
                    var blackListedUser = false;
                    //if this is a retweet, find the original tweet, check none in the chain are by a blacklisted user
                    while (origStatus.RetweetedStatus.User != null && !string.IsNullOrEmpty(origStatus.RetweetedStatus.User.ScreenNameResponse))
                    {
                        isrt = true;
                        origStatus = origStatus.RetweetedStatus;
                        if (_blackListedUsers.Contains(origStatus.User.ScreenNameResponse.ToLower()))
                            blackListedUser = true;
                    }

                    //check blacklist again in case we started off with an original rather than a retweet
                    if (blackListedUser || _blackListedUsers.Contains(origStatus.User.ScreenNameResponse.ToLower())) continue;

                    //check the original tweet text actually still contains our search query
                    //and that they are just not part of another word eg spoRT
                    var tweetTerms = Regex.Replace(origStatus.Text.ToLower(), @"[^A-Za-z]+", " ").Split(' ');
                    var queryTerms = _query.Split(' ').Where(t => t.ToLower() != "uk");

                    if(!queryTerms.All(t => tweetTerms.Contains(t))) continue;
                    if(_blackListedTerms.Any(badTerm => tweetTerms.Contains(badTerm.ToLower()))) continue;

                    //some more sources of false positives
                    if (origStatus.Text.StartsWith("RT @") || origStatus.Text.StartsWith("@") || origStatus.Text.StartsWith("RT:")) continue;
                    if (origStatus.Entities.UserMentionEntities.Any(u => u.ScreenName != origStatus.User.ScreenNameResponse)) continue;

                    var rtText = isrt ? "Retweet - " + status.StatusID : "Original";
                    yield return new Competition(origStatus.StatusID, origStatus.User.ScreenNameResponse, String.Format("Twitter Search ({0}) ({1})", _query, rtText), origStatus.Text);
                }
            }
        }
    }
}
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

        public TwitterSearchCompetitionSource(int checkInterval, string query, List<String> blacklistedUsers, List<String> blacklistedTerms, bool locationSearch, string consumerKey, string consumerSecret, string accessToken, string accessSecret) : base(checkInterval, "Twitter Search (" + query + ")")
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

                if (_lastStatus > 1)
                    searchQuery = searchQuery.Where(s => s.SinceID == _lastStatus);

                if (_locationSearch)
                    searchQuery = searchQuery.Where(s => s.GeoCode == "54.171278,-4.312134,700km");

                var searchResponse = searchQuery.SingleOrDefault();

                if (searchResponse == null || searchResponse.Statuses == null || searchResponse.Statuses.Count == 0)
                    yield break;

                _lastStatus = searchResponse.Statuses.Max(s => s.StatusID);

                _logger.InfoFormat("Found {0} new search results", searchResponse.Statuses.Count);

                var badUsers = 0;
                var badTerms = 0;
                var badStartsWith = 0;
                var missingTerms = 0;
                var insufficientRetweets = 0;
                var mentioningOthers = 0;
                var tooOld = 0;
                var success = 0;

                var termsMissed = new Dictionary<String, int>();

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

                    //check if the tweet was recent
                    if (origStatus.CreatedAt < DateTime.Now.AddHours(-24))
                    {
                        tooOld++;
                        continue;
                    }
                        

                    //check blacklist again in case we started off with an original rather than a retweet
                    if (blackListedUser || _blackListedUsers.Contains(origStatus.User.ScreenNameResponse.ToLower()))
                    {
                        badUsers++;
                        continue;
                    }

                    //check the original tweet text actually still contains our search query
                    //and that they are just not part of another word eg spoRT
                    var tweetTerms = Regex.Replace(origStatus.Text.ToLower(), @"[^a-z0-9]+", " ").Split(' ');
                    var queryTerms = _query.Split(' ').Where(t => t.ToLower() != "uk");

                    var hasMissingTerms = false;
                    foreach (var t in queryTerms.Where(t => !tweetTerms.Contains(t.ToLower())))
                    {
                        hasMissingTerms = true;
                        if(!termsMissed.ContainsKey(t))
                            termsMissed.Add(t,0);

                        termsMissed[t]++;
                    }

                    if(hasMissingTerms) { 
                        missingTerms++;
                        continue;
                    }

                    if (_blackListedTerms.Any(badTerm => tweetTerms.Contains(badTerm.ToLower())))
                    {
                        badTerms++;
                        continue;
                    }

                    //some more sources of false positives
                    if (origStatus.Text.StartsWith("RT @") || origStatus.Text.StartsWith("@") ||
                        origStatus.Text.StartsWith("RT:"))
                    {
                        badStartsWith++;
                        continue;
                    }

                    //retweet threshold - filters false positives, any popular comp will get entered later when we see more retweets
                    if (origStatus.RetweetCount < 5)
                    {
                        insufficientRetweets++;
                        continue;
                    }

                    success++;
                    var rtText = isrt ? "Retweet - " + status.StatusID : "Original";
                    yield return new Competition(origStatus.StatusID, origStatus.User.ScreenNameResponse, String.Format("Twitter Search ({0}) ({1})", _query, rtText), origStatus.Text, isrt);
                }

                _logger.InfoFormat(
                    "Accepted: {0} Too Old: {1} BadUser: {2}, BadTerms: {3}, BadStart: {4}, MissingTerms: {5}, RT<5: {6}, Mentions: {7}",
                    success, tooOld, badUsers, badTerms, badStartsWith, missingTerms, insufficientRetweets, mentioningOthers);
                #if DEBUG
                if (missingTerms > 0)
                {
                    _logger.Info("Missing Terms: ");
                    foreach (var pair in termsMissed.OrderBy(kvp => kvp.Value))
                    {
                        _logger.InfoFormat("{0} - {1}", pair.Key, pair.Value);
                    }
                }
                #endif
            }
        }
    }
}
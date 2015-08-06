using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using LinqToTwitter;

namespace Sources
{
    public class TwitterSearchCompetitionSource : CompetitionSource
    {
        private ulong _lastStatus = 1;
        private readonly InMemoryCredentialStore _credentialStore;
        private readonly string _query;
        private readonly bool _locationSearch;

        public TwitterSearchCompetitionSource(int checkInterval, string query, bool locationSearch, string consumerKey, string consumerSecret, string accessToken, string accessSecret) : base(checkInterval, "Twitter Search")
        {
            _query = query;
            _locationSearch = locationSearch;

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

                if (_lastStatus != 1)
                {
                    searchQuery = searchQuery.Where(t => t.SinceID == _lastStatus);
                }

                var searchResponse = searchQuery.SingleOrDefault();

                if (searchResponse == null || searchResponse.Statuses == null || searchResponse.Statuses.Count == 0) yield break;

                var highestStatus = searchResponse.Statuses.Max(s => s.StatusID);

                if (_lastStatus == 1)
                {
                    _lastStatus = highestStatus;
                    yield break;
                }
                _lastStatus = highestStatus;

                foreach (var status in searchResponse.Statuses)
                {
                    var origStatus = status;
                    var isrt = false;
                    //if this is a retweet, find the original tweet
                    while (origStatus.RetweetedStatus.User != null && !string.IsNullOrEmpty(origStatus.RetweetedStatus.User.ScreenNameResponse))
                    {
                        isrt = true;
                        origStatus = origStatus.RetweetedStatus;
                    }

                    //non retweets are actually normally false positives
                    if (!isrt)
                        continue;

                    //some more sources of false positives
                    if (origStatus.Text.StartsWith("RT @") || origStatus.Text.StartsWith("@") || origStatus.Text.StartsWith("RT:"))
                        continue;

                    if(origStatus.Entities.UserMentionEntities.Any(u => u.ScreenName != origStatus.User.ScreenNameResponse))
                        continue;

                    var rtText = isrt ? "Retweet - " + status.StatusID : "Original";
                    yield return new Competition(origStatus.StatusID, origStatus.User.ScreenNameResponse, String.Format("Twitter Search ({0}) ({1})",_query, rtText), origStatus.Text);
                }
            }
        }
    }
}
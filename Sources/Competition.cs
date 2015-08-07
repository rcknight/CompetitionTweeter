using System;

namespace Sources
{
    public class Competition
    {
        public Competition(ulong retweet, string follow, string source, string text, bool wasRetweet)
        {
            Retweet = retweet;
            Follow = follow.ToLower();
            Source = source;
            Text = text;
            WasRetweet = wasRetweet;
        }

        public ulong Retweet { get; private set; }
        public string Follow { get; private set; }
        public string Source { get; private set; }
        public string Text { get; private set; }
        public bool WasRetweet { get; private set; }

        public override string ToString()
        {
            return String.Format("{0}\n Follow: {1}\n Source: {2}\n", Retweet, Follow, Source);
        }
    }
}
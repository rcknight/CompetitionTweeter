namespace Sources
{
    public class Competition
    {
        public Competition(ulong retweet, string follow, string source, string text)
        {
            Retweet = retweet;
            Follow = follow.ToLower();
            Source = source;
            Text = text;
        }

        public ulong Retweet { get; private set; }
        public string Follow { get; private set; }
        public string Source { get; private set; }
        public string Text { get; private set; }

        public override string ToString()
        {
            return $"{Retweet}\n Follow: {Follow}\n Source: {Source}\n";
        }
    }
}
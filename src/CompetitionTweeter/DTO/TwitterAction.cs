namespace CompetitionTweeter.DTO
{
    public class TwitterAction
    {
        public string Id { get; set; }
        public TwitterActionType ActionType { get; set; }

        public TwitterAction(string id, TwitterActionType actionType)
        {
            Id = id;
            ActionType = actionType;
        }

        public TwitterAction()
        {
        }
    }
}

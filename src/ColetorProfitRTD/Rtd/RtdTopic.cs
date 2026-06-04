namespace ColetorProfitRTD.Rtd
{
    public sealed class RtdTopic
    {
        public int TopicId { get; set; }
        public string Asset { get; set; }
        public string Channel { get; set; }
        public string Topic { get; set; }
        public string Field { get; set; }
        public int? Index { get; set; }
        public string Extra { get; set; }
        public object[] Args { get; set; }
        public object LastValue { get; set; }

        public string Key
        {
            get
            {
                string index = Index.HasValue ? ":" + Index.Value : string.Empty;
                string extra = string.IsNullOrWhiteSpace(Extra) ? string.Empty : ":" + Extra;
                return Asset + ":" + Channel + ":" + Topic + ":" + Field + index + extra;
            }
        }
    }
}

using System.Web.Script.Serialization;

namespace ColetorProfitRTD.Web
{
    public static class JsonHelper
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = 32 * 1024 * 1024
        };

        public static string Serialize(object value)
        {
            lock (Serializer)
            {
                return Serializer.Serialize(value);
            }
        }

        public static T Deserialize<T>(string json)
        {
            lock (Serializer)
            {
                return Serializer.Deserialize<T>(json);
            }
        }
    }
}

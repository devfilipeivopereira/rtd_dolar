using System.Web.Script.Serialization;

namespace ColetorProfitRTD.Web
{
    public static class JsonHelper
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = 1024 * 1024
        };

        public static string Serialize(object value)
        {
            lock (Serializer)
            {
                return Serializer.Serialize(value);
            }
        }
    }
}

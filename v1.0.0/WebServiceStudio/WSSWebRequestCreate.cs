namespace WebServiceStudio
{
    using System;
    using System.Net;

    public class WSSWebRequestCreate : IWebRequestCreate
    {
        public virtual WebRequest Create(Uri uri)
        {
            var webRequest = WebRequest.CreateDefault(uri);
            if (WSSWebRequest.RequestTrace == null)
            {
                return webRequest;
            }
            return new WSSWebRequest(webRequest);
        }

        public static void RegisterPrefixes()
        {
            var creator = new WSSWebRequestCreate();
            WebRequest.RegisterPrefix("http://", creator);
            WebRequest.RegisterPrefix("https://", creator);
        }
    }
}


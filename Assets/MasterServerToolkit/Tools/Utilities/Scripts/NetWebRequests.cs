using MasterServerToolkit.Logging;
using System;
using System.IO;
using System.Net;

namespace MasterServerToolkit.Utils
{
    public class NetWebRequests
    {
        public static string Get(string url)
        {
            try
            {
                // Create a request for the URL. 		
                WebRequest request = WebRequest.Create(url);
                // Get the response.
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream dataStream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(dataStream);
                    string responseFromServer = reader.ReadToEnd();
                    reader.Close();
                    dataStream.Close();
                    response.Close();
                    return responseFromServer;
                }
                else
                {
                    Logs.Error($"The following error occurred : {response.StatusCode}, {response.StatusDescription}");
                    return string.Empty;
                }
            }
            catch (WebException e)
            {
                Logs.Error($"The following error occurred : {e.Status}");
                return string.Empty;
            }
            catch (Exception e)
            {
                Logs.Error($"The following Exception was raised : {e.Message}");
                return string.Empty;
            }
        }
    }
}
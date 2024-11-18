using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace MasterServerToolkit.Utils
{
    public class NetWebRequests
    {
        public static async Task<MstJson> GetAsync(string url, Dictionary<string, string> headers = null)
        {
            MstJson result = MstJson.EmptyObject;

            try
            {
                WebRequest request = WebRequest.Create(url);
                request.Method = "GET";
                request.ContentType = "application/json";

                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        request.Headers.Add(item.Key, item.Value);
                    }
                }

                using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (Stream dataStream = response.GetResponseStream())
                        using (StreamReader reader = new StreamReader(dataStream))
                        {
                            string responseFromServer = await reader.ReadToEndAsync();

                            if (MstJson.IsJson(responseFromServer))
                            {
                                result.SetField("data", new MstJson(responseFromServer));
                            }
                            else
                            {
                                result.SetField("data", MstJson.Create(responseFromServer));
                            }
                        }
                    }
                    else
                    {
                        result.SetField("error", $"Error: {response.StatusCode}, {response.StatusDescription}");
                    }
                }
            }
            catch (WebException e)
            {
                result.SetField("error", $"WebException: {e.Message}");
            }
            catch (Exception e)
            {
                result.SetField("error", $"Exception: {e.Message}");
            }

            return result;
        }

        public static async Task<MstJson> PostAsync(string url, Dictionary<string, string> headers = null, MstJson postData = null)
        {
            MstJson result = MstJson.EmptyObject;

            try
            {
                WebRequest request = WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";

                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        request.Headers.Add(item.Key, item.Value);
                    }
                }

                if (postData != null)
                {
                    using (var streamWriter = new StreamWriter(await request.GetRequestStreamAsync()))
                    {
                        string json = postData.ToString();
                        await streamWriter.WriteAsync(json);
                        await streamWriter.FlushAsync();
                    }
                }

                using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (Stream dataStream = response.GetResponseStream())
                        using (StreamReader reader = new StreamReader(dataStream))
                        {
                            string responseFromServer = await reader.ReadToEndAsync();

                            if (MstJson.IsJson(responseFromServer))
                            {
                                result.SetField("data", new MstJson(responseFromServer));
                            }
                            else
                            {
                                result.SetField("data", MstJson.Create(responseFromServer));
                            }
                        }
                    }
                    else
                    {
                        result.SetField("error", $"Error: {response.StatusCode}, {response.StatusDescription}");
                    }
                }
            }
            catch (WebException e)
            {
                result.SetField("error", $"WebException: {e.Message}");
            }
            catch (Exception e)
            {
                result.SetField("error", $"Exception: {e.Message}");
            }

            return result;
        }


        public static MstJson Get(string url, Dictionary<string, string> headers = null)
        {
            return Task.Run(() => GetAsync(url, headers)).GetAwaiter().GetResult();
        }
    }
}
using CommandTerminal;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace MasterServerToolkit.Client.Utilities
{
    public class ClientAuthTerminalCommands
    {
        [RegisterCommand(Name = "cl.auth.guest", Help = "Sign up as guest client. No credentials required", MinArgCount = 0, MaxArgCount = 0)]
        private static void ClientAuthSignInAsGuest(CommandArg[] args)
        {
            Mst.Client.Auth.SignInAsGuest((accountInfo, error) =>
            {
                if (string.IsNullOrEmpty(error))
                {
                    Logs.Info($"You have logged in as: {accountInfo.Username}. Here are full info: \n{accountInfo}");
                }
                else
                {
                    Logs.Error($"An error occurred while logging in: {error}");
                }
            });
        }

        [RegisterCommand(Name = "cl.auth.signin", Help = "Sign in as registered user. 1 Username, 2 Password", MinArgCount = 2, MaxArgCount = 2)]
        private static void ClientAuthSignIn(CommandArg[] args)
        {
            Mst.Client.Auth.SignInWithLoginAndPassword(args[0].String, args[1].String, (accountInfo, error) =>
            {
                if (string.IsNullOrEmpty(error))
                {
                    Logs.Info($"You have logged in as: {accountInfo.Username}");
                }
                else
                {
                    Logs.Error($"An error occurred while logging in: {error}");
                }
            });
        }

        [RegisterCommand(Name = "cl.auth.signup", Help = "Sign up as registered user. 1 Username, 2 E-Mail, 3 Password", MinArgCount = 3, MaxArgCount = 3)]
        private static void ClientAuthSignUp(CommandArg[] args)
        {
            var credentials = new MstProperties();
            credentials.Set("username", args[0].String);
            credentials.Set("email", args[1].String);
            credentials.Set("password", args[2].String);

            Mst.Client.Auth.SignUp(credentials, (isSuccessful, error) =>
            {
                if (isSuccessful)
                {
                    Logs.Info($"You have successfuly signed up. Now you may sign in");
                }
                else
                {
                    Logs.Error($"An error occurred while signing up: {error}");
                }
            });
        }

        [RegisterCommand(Name = "cl.auth.updateproperties", Help = "Update user account properties", MinArgCount = 0, MaxArgCount = 0)]
        private static void ClientAuthUpdateProperties(CommandArg[] args)
        {
            //MstTimer.Instance.StartCoroutine(GetUserInfoCoroutine((isSuccess, data) =>
            //{
            //    if (!isSuccess)
            //    {
            //        return;
            //    }

            //    //Debug.Log(data.SelectToken("results[0].gender").Value<string>());

            //    var properties = new MstProperties();
            //    properties.Set("gender", data.SelectToken("results[0].gender").Value<string>());
            //    properties.Set("name_title", data.SelectToken("results[0].name.title").Value<string>());
            //    properties.Set("name_first", data.SelectToken("results[0].name.first").Value<string>());
            //    properties.Set("name_last", data.SelectToken("results[0].name.last").Value<string>());
            //    properties.Set("location_street_number", data.SelectToken("results[0].location.street.number").Value<string>());
            //    properties.Set("location_street_name", data.SelectToken("results[0].location.street.name").Value<string>());
            //    properties.Set("location_city", data.SelectToken("results[0].location.city").Value<string>());
            //    properties.Set("location_state", data.SelectToken("results[0].location.state").Value<string>());
            //    properties.Set("location_country", data.SelectToken("results[0].location.country").Value<string>());
            //    properties.Set("location_postcode", data.SelectToken("results[0].location.postcode").Value<string>());
            //    properties.Set("location_coord_lat", data.SelectToken("results[0].location.coordinates.latitude").Value<string>());
            //    properties.Set("location_coord_long", data.SelectToken("results[0].location.coordinates.longitude").Value<string>());
            //    properties.Set("location_timezone_offset", data.SelectToken("results[0].location.timezone.offset").Value<string>());
            //    properties.Set("location_timezone_description", data.SelectToken("results[0].location.timezone.description").Value<string>());

            //    Mst.Client.Auth.AccountInfo.Properties.AddOrUpdate(properties);

            //    Mst.Client.Auth.UpdateAccountInfo((isSuccessful, error) =>
            //    {
            //        if (isSuccessful)
            //        {
            //            Logs.Info($"You have successfuly updated your properties. Here are new data: \n{Mst.Client.Auth.AccountInfo}");
            //        }
            //        else
            //        {
            //            Logs.Error($"An error occurred while updating your properties: {error}");
            //        }
            //    });
            //}));
        }

        private static IEnumerator GetUserInfoCoroutine(Action<bool, JObject> callback)
        {
            UnityWebRequest www = UnityWebRequest.Get("https://randomuser.me/api/");
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                callback?.Invoke(false, null);
                Debug.LogError(www.error);
            }
            else
            {
                var info = JsonConvert.DeserializeObject<JObject>(www.downloadHandler.text);
                callback?.Invoke(true, info);
            }
        }
    }
}

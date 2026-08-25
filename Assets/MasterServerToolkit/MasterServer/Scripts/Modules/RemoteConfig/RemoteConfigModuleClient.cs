using MasterServerToolkit.Networking;
using MasterServerToolkit.Logging;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class RemoteConfigModuleClient : MstBaseClient
    {
        public MstProperties Data { get; private set; } = new();
        public bool IsLoaded => Data.Count > 0;

        public event Action<MstProperties> OnLoaded;

        public RemoteConfigModuleClient(IClientSocket connection) : base(connection)
        {
            RegisterErrorParsers();
        }

        internal static void RegisterErrorParsers()
        {
            Mst.Errors.TryRegister(MstErrorCodes.REMOTE_CONFIG_RESPONSE_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.REMOTE_CONFIG_REQUEST_FAILED);
        }

        public void Load(SuccessCallback callback)
        {
            Load(callback, Connection);
        }

        public void Load(SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            try
            {
                connection.SendMessage(MstOpCodes.GetRemoteConfig, (status, response) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        callback?.Invoke(false, Mst.Errors.Parse(status, response));
                        return;
                    }

                    try
                    {
                        Data = MstProperties.FromBytes(response.AsBytes());
                    }
                    catch (Exception exception)
                    {
                        Logs.Error($"Remote config response is invalid: {exception}");
                        callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.Invalid,
                            CreateErrorProperties(MstErrorCodes.REMOTE_CONFIG_RESPONSE_INVALID)));
                        return;
                    }

                    OnLoaded?.Invoke(Data);
                    callback?.Invoke(true, string.Empty);
                });
            }
            catch (Exception exception)
            {
                Logs.Error($"Remote config request failed: {exception}");
                callback?.Invoke(false, Mst.Errors.Parse(
                    connection.IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected,
                    connection.IsConnected
                        ? CreateErrorProperties(MstErrorCodes.REMOTE_CONFIG_REQUEST_FAILED)
                        : null));
            }
        }

        private static MstProperties CreateErrorProperties(string code)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, code);
            return properties;
        }
    }
}

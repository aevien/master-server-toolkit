using MasterServerToolkit.Networking;
using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        private float analyticsSendInterval = 5f;

        [Header("Components"), SerializeField]
        private DashboardConnectionHelper connectionHelper;

        #endregion

        private SystemInfoPacket systemInfoPacket = new SystemInfoPacket();
        private string dashboardInfoId = string.Empty;

        protected override void Awake()
        {
            base.Awake();

            dashboardInfoId = Mst.Args.AsString(Mst.Args.Names.DashboardInfoId, $"Server_{Mst.Helper.CreateGuidStringN()}");
        }

        private void OnDestroy()
        {
            if (connectionHelper)
                connectionHelper.Connection.RemoveConnectionOpenListener(ConnectionOpenHandler);

            CancelInvoke();
        }

        public override void Initialize(IServer server)
        {
            if (!connectionHelper)
                connectionHelper = FindObjectOfType<DashboardConnectionHelper>();

            if (connectionHelper)
                connectionHelper.Connection.AddConnectionOpenListener(ConnectionOpenHandler);

            try
            {
                // Gather system info analytics. Do it only in main thread
                systemInfoPacket = new SystemInfoPacket
                {
                    Id = dashboardInfoId,
                    DeviceId = SystemInfo.deviceUniqueIdentifier,
                    DeviceModel = SystemInfo.deviceModel,
                    DeviceName = SystemInfo.deviceName,
                    DeviceType = SystemInfo.deviceType.ToString(),
                    GraphicsDeviceId = SystemInfo.graphicsDeviceID.ToString(),
                    GraphicsDeviceName = SystemInfo.graphicsDeviceName,
                    GraphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
                    GraphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                    GraphicsDeviceVendorId = SystemInfo.graphicsDeviceVendorID.ToString(),
                    GraphicsDeviceVendor = SystemInfo.graphicsDeviceVendor,
                    GraphicsDeviceMemory = SystemInfo.graphicsMemorySize,
                    Os = SystemInfo.operatingSystem,
                    OsFamily = SystemInfo.operatingSystemFamily.ToString(),
                    CpuType = SystemInfo.processorType,
                    CpuFrequency = SystemInfo.processorFrequency,
                    CpuCount = SystemInfo.processorCount,
                    Ram = SystemInfo.systemMemorySize
                };
            }
            catch (Exception e)
            {
                systemInfoPacket = new SystemInfoPacket()
                {
                    Error = e.Message
                };
            }
        }

        private void ConnectionOpenHandler(IClientSocket client)
        {
            analyticsSendInterval = Mathf.Clamp(Mst.Args.AsFloat(Mst.Args.Names.AnalyticsSendInterval, analyticsSendInterval), 2f, 120f);

            MstTimer.WaitForRealtimeSeconds(1f, () =>
            {
                logger.Info($"Registering in dashboard as {dashboardInfoId}");

                // Trying to join dashboard
                connectionHelper.Connection.SendMessage(MstOpCodes.JoinDashboard, dashboardInfoId, (status, respond) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        logger.Error(respond.AsString());
                        connectionHelper.Connection.Close();
                        return;
                    }

                    logger.Info($"Successfully registered in dashboard as {dashboardInfoId}");

                    CancelInvoke();
                    InvokeRepeating(nameof(UpdateAnalytics), analyticsSendInterval, analyticsSendInterval);
                });
            });
        }

        private void UpdateAnalytics()
        {
            SendSystemInfo();
        }

        private void SendSystemInfo()
        {
            if (connectionHelper.IsConnected)
            {
                connectionHelper.Connection.SendMessage(MstOpCodes.SystemInfo, systemInfoPacket, (status, respond) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        logger.Error(respond.AsString());
                        connectionHelper.Connection.Close();
                        return;
                    }

                    MstTimer.WaitForRealtimeSeconds(0.5f, () =>
                    {
                        SendServerInfo();
                    });
                });
            }
        }

        private void SendServerInfo()
        {
            if (connectionHelper.IsConnected)
            {
                // Gather server info analytics
                var serverInfoPacket = ServerInfoPacket.FromJobject(Server.JsonInfo());
                serverInfoPacket.Id = dashboardInfoId;

                connectionHelper.Connection.SendMessage(MstOpCodes.ServerInfo, serverInfoPacket, (status, respond) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        logger.Error(respond.AsString());
                        connectionHelper.Connection.Close();
                        return;
                    }

                    MstTimer.WaitForRealtimeSeconds(0.5f, () =>
                    {
                        SendModulesInfo();
                    });
                });
            }
        }

        private void SendModulesInfo()
        {
            if (connectionHelper.IsConnected)
            {
                // Gather modules info analytics
                foreach (var module in Server.GetInitializedModules())
                {
                    var moduleInfo = module.JsonInfo();

                    var moduleInfoPacket = new ModuleInfoPacket
                    {
                        Id = dashboardInfoId,
                        Module = moduleInfo.GetField("name").StringValue,
                        Data = moduleInfo
                    };

                    connectionHelper.Connection.SendMessage((ushort)MstOpCodes.ModulesInfo, moduleInfoPacket, (status, respond) =>
                    {
                        if (status != ResponseStatus.Success)
                        {
                            logger.Error(respond.AsString());
                            connectionHelper.Connection.Close();
                        }
                    });
                }
            }
        }
    }
}
using MasterServerToolkit.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class DashboardModule : BaseServerModule
    {
        /// <summary>
        /// 
        /// </summary>
        private readonly HashSet<string> sourcesToRemove = new HashSet<string>();
        /// <summary>
        /// 
        /// </summary>
        private readonly Dictionary<string, DashboardPeerExtension> registeredSources = new Dictionary<string, DashboardPeerExtension>();
        /// <summary>
        /// 
        /// </summary>
        public ConcurrentDictionary<string, SystemInfoPacket> SystemInfo { get; private set; } = new ConcurrentDictionary<string, SystemInfoPacket>();
        /// <summary>
        /// 
        /// </summary>
        public ConcurrentDictionary<string, ServerInfoPacket> ServerInfo { get; private set; } = new ConcurrentDictionary<string, ServerInfoPacket>();
        /// <summary>
        /// 
        /// </summary>
        public ConcurrentDictionary<string, List<ModuleInfoPacket>> ModulesInfo { get; private set; } = new ConcurrentDictionary<string, List<ModuleInfoPacket>>();

        public override void Initialize(IServer server)
        {
            server.RegisterMessageHandler(MstOpCodes.JoinDashboard, JoinDashboardMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.SystemInfo, SystemInfoMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.ServerInfo, ServerInfoMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.ModulesInfo, ModulesInfoMessageHandler);
        }

        #region MESSAGE HANDLERS

        private void JoinDashboardMessageHandler(IIncomingMessage message)
        {
            try
            {
                var dashdoardPeerExtension = message.Peer.GetExtension<DashboardPeerExtension>();

                if (dashdoardPeerExtension != null)
                {
                    message.Respond(ResponseStatus.Success);
                    return;
                }

                string sourceId = message.AsString();

                if (string.IsNullOrEmpty(sourceId))
                    throw new Exception($"{nameof(sourceId)} cannot be empty");

                sourcesToRemove.Remove(sourceId);

                dashdoardPeerExtension = new DashboardPeerExtension(sourceId, message.Peer);
                message.Peer.AddExtension(dashdoardPeerExtension);

                registeredSources[sourceId] = dashdoardPeerExtension;

                if (!SystemInfo.ContainsKey(sourceId))
                    SystemInfo[sourceId] = new SystemInfoPacket();

                if (!ServerInfo.ContainsKey(sourceId))
                    ServerInfo[sourceId] = new ServerInfoPacket();

                if (!ModulesInfo.ContainsKey(sourceId))
                    ModulesInfo[sourceId] = new List<ModuleInfoPacket>();

                message.Peer.OnConnectionCloseEvent += Peer_OnConnectionCloseEvent;

                logger.Info($"Source {sourceId} successfully registered");

                message.Respond(ResponseStatus.Success);
            }
            catch (Exception e)
            {
                logger.Error(e);
                message.Respond(e.ToString(), ResponseStatus.Error);
            }
        }

        private void Peer_OnConnectionCloseEvent(IPeer peer)
        {
            peer.OnConnectionCloseEvent -= Peer_OnConnectionCloseEvent;

            var dashdoardPeerExtension = peer.GetExtension<DashboardPeerExtension>();

            if (dashdoardPeerExtension != null)
            {
                if (!sourcesToRemove.Contains(dashdoardPeerExtension.SourceId))
                {
                    sourcesToRemove.Add(dashdoardPeerExtension.SourceId);

                    Mst.Concurrency.RunInMainThread(() =>
                    {
                        MstTimer.WaitForRealtimeSeconds(10f, () =>
                        {
                            if (sourcesToRemove.Contains(dashdoardPeerExtension.SourceId))
                            {
                                sourcesToRemove.Remove(dashdoardPeerExtension.SourceId);
                                registeredSources.Remove(dashdoardPeerExtension.SourceId);
                                SystemInfo.TryRemove(dashdoardPeerExtension.SourceId, out var _);
                                ServerInfo.TryRemove(dashdoardPeerExtension.SourceId, out var _);
                                ModulesInfo.TryRemove(dashdoardPeerExtension.SourceId, out var _);
                            }
                        });
                    });
                }
            }
        }

        private void SystemInfoMessageHandler(IIncomingMessage message)
        {
            try
            {
                SystemInfoPacket info = message.AsPacket(new SystemInfoPacket());

                if (string.IsNullOrEmpty(info.Id))
                    throw new Exception("Source Id cannot be empty");

                if (!SystemInfo.ContainsKey(info.Id))
                    throw new Exception($"Source with id {info.Id} is not registered");

                SystemInfo[info.Id] = info;

                message.Respond(ResponseStatus.Success);
            }
            catch (Exception e)
            {
                logger.Error(e);
                message.Respond(e.ToString(), ResponseStatus.Error);
            }
        }

        private void ServerInfoMessageHandler(IIncomingMessage message)
        {
            try
            {
                ServerInfoPacket info = message.AsPacket(new ServerInfoPacket());

                if (string.IsNullOrEmpty(info.Id))
                    throw new Exception("Source Id cannot be empty");

                if (!ServerInfo.ContainsKey(info.Id))
                    throw new Exception($"Source with id {info.Id} is not registered");

                ServerInfo[info.Id] = info;

                message.Respond(ResponseStatus.Success);
            }
            catch (Exception e)
            {
                logger.Error(e);
                message.Respond(e.ToString(), ResponseStatus.Error);
            }
        }

        private void ModulesInfoMessageHandler(IIncomingMessage message)
        {
            try
            {
                ModuleInfoPacket info = message.AsPacket(new ModuleInfoPacket());

                if (string.IsNullOrEmpty(info.Id))
                    throw new Exception("Source Id cannot be empty");

                if (!ModulesInfo.ContainsKey(info.Id))
                    throw new Exception($"Source with id {info.Id} is not registered");

                int indexOfModule = ModulesInfo[info.Id].FindIndex(x => x.Module == info.Module);

                if (indexOfModule < 0)
                {
                    ModulesInfo[info.Id].Add(info);
                }
                else
                {
                    ModulesInfo[info.Id][indexOfModule] = info;
                }

                message.Respond(ResponseStatus.Success);
            }
            catch (Exception e)
            {
                logger.Error(e);
                message.Respond(e.ToString(), ResponseStatus.Error);
            }
        }

        #endregion
    }
}
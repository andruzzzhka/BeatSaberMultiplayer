using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.ChannelSelectionScreen;
using BS_Utils.Gameplay;
using CustomUI.BeatSaber;
using CustomUI.Utilities;
using Lidgren.Network;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using VRUI;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class ChannelSelectionFlowCoordinator : FlowCoordinator
    {
        MultiplayerNavigationController channelSelectionNavController;
        ChannelSelectionViewController channelSelectionViewController;

        List<ChannelInfo> _channelInfos = new List<ChannelInfo>();
        int currentChannel = 0;

        List<RadioClient> _channelClients = new List<RadioClient>();

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                channelSelectionNavController = BeatSaberUI.CreateViewController<MultiplayerNavigationController>();
                channelSelectionNavController.didFinishEvent += () => { PluginUI.instance.modeSelectionFlowCoordinator.InvokeMethod("DismissFlowCoordinator", this, null, false); };

                channelSelectionViewController = BeatSaberUI.CreateViewController<ChannelSelectionViewController>();
                channelSelectionViewController.nextPressedEvent += () =>
                {
                    currentChannel++;
                    if (currentChannel >= _channelInfos.Count)
                        currentChannel = 0;
                    channelSelectionViewController.SetContent(_channelInfos[currentChannel]);
                };
                channelSelectionViewController.prevPressedEvent += () =>
                {
                    currentChannel--;
                    if (currentChannel < 0)
                        currentChannel = _channelInfos.Count - 1;
                    channelSelectionViewController.SetContent(_channelInfos[currentChannel]);
                };
                channelSelectionViewController.joinPressedEvent += (channel) => 
                {
                    if (!mainScreenViewControllers.Any(x => x.GetPrivateField<bool>("_isInTransition")))
                    {
                        PresentFlowCoordinator(PluginUI.instance.radioFlowCoordinator, null, false, false);
                        PluginUI.instance.radioFlowCoordinator.JoinChannel(channel.ip, channel.port, channel.channelId);
                        PluginUI.instance.radioFlowCoordinator.didFinishEvent -= DismissRadio;
                        PluginUI.instance.radioFlowCoordinator.didFinishEvent += DismissRadio;
                    }
                };

            }


            SetViewControllerToNavigationConctroller(channelSelectionNavController, channelSelectionViewController);
            ProvideInitialViewControllers(channelSelectionNavController, null, null);
            
            StartCoroutine(GetChannelsList());
        }

        private void DismissRadio()
        {
            DismissFlowCoordinator(PluginUI.instance.radioFlowCoordinator, null, false);
        }

        IEnumerator GetChannelsList()
        {
            yield return null;
#if LOCALRADIO
            UnityWebRequest www = UnityWebRequest.Get("http://127.0.0.1:222/Channels");
#else
            UnityWebRequest www = UnityWebRequest.Get("https://radio.assistant.moe/Channels/");
#endif

            Plugin.log.Info("Requesting channels list...");
            channelSelectionViewController.SetLoadingState(true);

            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Plugin.log.Error(www.error);
            }
            else
            {
                Plugin.log.Debug("Received response!");

                JSONNode node = JSON.Parse(www.downloadHandler.text);

                _channelInfos.Clear();
                List<ChannelInfo> channelInfos = new List<ChannelInfo>();

                if (node["channels"].Count == 0)
                {
                    Plugin.log.Error($"No channels available");
                    yield break;
                }

                for (int i = 0; i < node["channels"].Count; i++)
                {
                    channelInfos.Add(new ChannelInfo(node["channels"][i]));
                }

                currentChannel = 0;

                Plugin.log.Info("Requesting channel infos...");

                _channelClients.ForEach(x =>
                {
                    if (x != null)
                    {
                        x.Abort();
                        x.ReceivedResponse -= ReceivedResponse;
                        x.ChannelException -= ChannelException;
                    }
                });
                _channelClients.Clear();

                var groupedChannels = channelInfos.GroupBy(x => new { x.ip, x.port }).ToList();

                for (int i = 0; i < groupedChannels.Count; i++)
                {
                    RadioClient client = new GameObject("RadioClient").AddComponent<RadioClient>();

                    client.channelInfos = groupedChannels[i].ToList();
                    client.ReceivedResponse += ReceivedResponse;
                    client.ChannelException += ChannelException;
                    _channelClients.Add(client);
                }
                
                _channelClients.ForEach(x => x.GetRooms());
                Plugin.log.Debug("Requested channel infos!");
            }

        }

        private void ChannelException(RadioClient sender, Exception ex)
        {
            Plugin.log.Warn("Channel exception: "+ex);
        }

        private void ReceivedResponse(RadioClient sender, ChannelInfo info)
        {
            channelSelectionViewController.SetLoadingState(false);
            _channelInfos.Add(info);
            if(_channelInfos.Count == 1)
            {
                channelSelectionViewController.SetContent(_channelInfos.First());
            }
        }
    }

    class RadioClient : MonoBehaviour
    {
        private NetClient NetworkClient;

        public List<ChannelInfo> channelInfos;

        private int currentChannel = 0;

        public event Action<RadioClient, ChannelInfo> ReceivedResponse;
        public event Action<RadioClient, Exception> ChannelException;

        public void Awake()
        {
            NetPeerConfiguration Config = new NetPeerConfiguration("BeatSaberMultiplayer") { ConnectionTimeout = 5, MaximumHandshakeAttempts = 2 };
            NetworkClient = new NetClient(Config);
        }

        public void GetRooms()
        {
            try
            {
                NetworkClient.Start();

                Plugin.log.Debug($"Creating message...");
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();

                Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                byte[] version = new byte[4] { (byte)assemblyVersion.Major, (byte)assemblyVersion.Minor, (byte)assemblyVersion.Build, (byte)assemblyVersion.Revision };

                outMsg.Write(version);
                new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID()).AddToMessage(outMsg);

                Plugin.log.Debug($"Connecting to {channelInfos[0].ip}:{channelInfos[0].port}...");

                NetworkClient.Connect(channelInfos[0].ip, channelInfos[0].port, outMsg);
            }
            catch (Exception e)
            {
                ChannelException?.Invoke(this, e);
                Abort();
            }
        }

        public void Update()
        {
            if (NetworkClient != null && NetworkClient.Status == NetPeerStatus.Running)
            {
                NetIncomingMessage msg;
                while ((msg = NetworkClient.ReadMessage()) != null)
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.StatusChanged:
                            {
                                NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();

                                if (status == NetConnectionStatus.Connected)
                                {
                                    NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                                    outMsg.Write((byte)CommandType.GetChannelInfo);
                                    outMsg.Write(channelInfos[currentChannel].channelId);

                                    NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                                }
                                else if (status == NetConnectionStatus.Disconnected)
                                {
                                    ChannelException?.Invoke(this, new Exception("Channel refused connection!"));
                                    Abort();
                                }

                            };
                            break;
                        case NetIncomingMessageType.Data:
                            {
                                if ((CommandType)msg.ReadByte() == CommandType.GetChannelInfo)
                                {
                                    ChannelInfo received = new ChannelInfo(msg);
                                    if(received.channelId == -1)
                                    {
                                        ChannelException?.Invoke(this, new Exception($"Channel with ID {channelInfos[currentChannel].channelId} not found!"));
                                        Abort();
                                        return;
                                    }
                                    received.ip = channelInfos[currentChannel].ip;
                                    received.port = channelInfos[currentChannel].port;
                                    ReceivedResponse?.Invoke(this, received);
                                    if(channelInfos.Count - 1 > currentChannel)
                                    {
                                        currentChannel++;
                                        
                                        NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                                        outMsg.Write((byte)CommandType.GetChannelInfo);
                                        outMsg.Write(channelInfos[currentChannel].channelId);

                                        NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                                    }
                                    else
                                    {
                                        Abort();
                                    }
                                }
                            };
                            break;
                        case NetIncomingMessageType.WarningMessage:
                            Plugin.log.Warn(msg.ReadString());
                            break;
                        case NetIncomingMessageType.ErrorMessage:
                            Plugin.log.Error(msg.ReadString());
                            break;
                        case NetIncomingMessageType.VerboseDebugMessage:
                        case NetIncomingMessageType.DebugMessage:
                            Plugin.log.Debug(msg.ReadString());
                            break;
                        default:
                            Plugin.log.Debug("Unhandled type: " + msg.MessageType);
                            break;
                    }

                }
            }
        }

        public void Abort()
        {
            NetworkClient.Shutdown("");
            Destroy(gameObject);
        }
    }
}

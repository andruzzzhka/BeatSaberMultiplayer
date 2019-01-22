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

        List<ChannelClient> _channelClients = new List<ChannelClient>();

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
                    if (currentChannel <= 0)
                        currentChannel = _channelInfos.Count -1;
                    channelSelectionViewController.SetContent(_channelInfos[currentChannel]);
                };
                channelSelectionViewController.joinPressedEvent += (channel) => 
                {
                    PresentFlowCoordinator(PluginUI.instance.radioFlowCoordinator, null, false, false);
                    PluginUI.instance.radioFlowCoordinator.JoinChannel(channel.ip, channel.port);
                    PluginUI.instance.radioFlowCoordinator.didFinishEvent -= () => { DismissFlowCoordinator(PluginUI.instance.radioFlowCoordinator, null, false); };
                    PluginUI.instance.radioFlowCoordinator.didFinishEvent += () => { DismissFlowCoordinator(PluginUI.instance.radioFlowCoordinator, null, false); };
                };

            }


            SetViewControllerToNavigationConctroller(channelSelectionNavController, channelSelectionViewController);
            ProvideInitialViewControllers(channelSelectionNavController, null, null);
            
            StartCoroutine(GetChannelsList());
        }

        IEnumerator GetChannelsList()
        {
            yield return null;

            UnityWebRequest www = UnityWebRequest.Get("http://192.168.0.157:222/radioChannels");

            Misc.Logger.Info("Requesting channels list...");
            channelSelectionViewController.SetLoadingState(true);

            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Misc.Logger.Error(www.error);
            }
            else
            {
#if DEBUG
                Misc.Logger.Info("Received response!");
#endif
                JSONNode node = JSON.Parse(www.downloadHandler.text);

                _channelInfos.Clear();
                List<ChannelInfo> channelInfos = new List<ChannelInfo>();

                if (node["channels"].Count == 0)
                {
                    Misc.Logger.Error($"No channels available");
                    yield break;
                }

                for (int i = 0; i < node["channels"].Count; i++)
                {
                    channelInfos.Add(new ChannelInfo(node["channels"][i]));
                }

                currentChannel = 0;
                
                Misc.Logger.Info("Requesting channel infos...");

                _channelClients.ForEach(x =>
                {
                    x.Abort();
                    x.ReceivedResponse -= ReceivedResponse;
                    x.ChannelException -= ChannelException;
                });
                _channelClients.Clear();

                for (int i = 0; i < channelInfos.Count; i++)
                {
                    ChannelClient client = new GameObject("ChannelClient").AddComponent<ChannelClient>();
                    client.channelInfo = channelInfos[i];
                    client.ReceivedResponse += ReceivedResponse;
                    client.ChannelException += ChannelException;
                    _channelClients.Add(client);
                }
                
                _channelClients.ForEach(x => x.GetRooms());
                Misc.Logger.Info("Requested channel infos!");
            }

        }

        private void ChannelException(ChannelClient sender, Exception ex)
        {
            Misc.Logger.Warning("Channel exception: "+ex);
        }

        private void ReceivedResponse(ChannelClient sender, ChannelInfo info)
        {
            channelSelectionViewController.SetLoadingState(false);
            _channelInfos.Add(info);
            if(_channelInfos.Count == 1)
            {
                channelSelectionViewController.SetContent(_channelInfos.First());
            }
        }
    }

    class ChannelClient : MonoBehaviour
    {
        private NetClient NetworkClient;

        public ChannelInfo channelInfo;

        public bool channelAvailable;

        public event Action<ChannelClient, ChannelInfo> ReceivedResponse;
        public event Action<ChannelClient, Exception> ChannelException;

        public void Awake()
        {
            NetPeerConfiguration Config = new NetPeerConfiguration("BeatSaberMultiplayer") { ConnectionTimeout = 5 };
            NetworkClient = new NetClient(Config);
        }

        public void GetRooms()
        {
            try
            {
                NetworkClient.Start();

                Misc.Logger.Info($"Creating message...");
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write(Plugin.pluginVersion);
                new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID()).AddToMessage(outMsg);

                Misc.Logger.Info($"Connecting to {channelInfo.ip}:{channelInfo.port}...");

                NetworkClient.Connect(channelInfo.ip, channelInfo.port, outMsg);
            }
            catch (Exception e)
            {
                ChannelException?.Invoke(this, e);
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

                                    NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                                }
                                else if (status == NetConnectionStatus.Disconnected)
                                {
                                    Abort();
                                    ChannelException?.Invoke(this, new Exception("Channel refused connection!"));
                                }

                            };
                            break;
                        case NetIncomingMessageType.Data:
                            {
                                if ((CommandType)msg.ReadByte() == CommandType.GetChannelInfo)
                                {
                                    channelAvailable = true;
                                    NetworkClient.Shutdown("");
                                    ChannelInfo received = new ChannelInfo(msg);
                                    received.ip = channelInfo.ip;
                                    received.port = channelInfo.port;
                                    ReceivedResponse?.Invoke(this, received);
                                }
                            };
                            break;
                        case NetIncomingMessageType.WarningMessage:
                            Misc.Logger.Warning(msg.ReadString());
                            break;
                        case NetIncomingMessageType.ErrorMessage:
                            Misc.Logger.Error(msg.ReadString());
                            break;
                        case NetIncomingMessageType.VerboseDebugMessage:
                        case NetIncomingMessageType.DebugMessage:
                            Misc.Logger.Info(msg.ReadString());
                            break;
                        default:
                            Console.WriteLine("Unhandled type: " + msg.MessageType);
                            break;
                    }

                }
            }
        }

        public void Abort()
        {
            NetworkClient.Shutdown("");
        }
    }
}

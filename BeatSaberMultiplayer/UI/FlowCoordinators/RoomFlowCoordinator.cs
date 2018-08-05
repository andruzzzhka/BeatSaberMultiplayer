using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using ServerHub.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class RoomFlowCoordinator : FlowCoordinator
    {
        ServerHubNavigationController _serverHubNavigationController;

        CustomKeyboardViewController _passwordKeyboard;
        RoomNavigationController _roomNavigationController;

        string ip;
        int port;
        uint roomId;
        bool usePassword;
        string password;

        bool joined = false;

        public void JoinRoom(ServerHubNavigationController serverHubNavCon, string ip, int port, uint roomId, bool usePassword, string pass = "")
        {
            Log.Info("1Joining room "+roomId);
            _serverHubNavigationController = serverHubNavCon;
            password = pass;
            this.ip = ip;
            this.port = port;
            this.roomId = roomId;
            this.usePassword = usePassword;

            if (usePassword && string.IsNullOrEmpty(password))
            {
                if (_passwordKeyboard == null)
                {
                    _passwordKeyboard = BeatSaberUI.CreateViewController<CustomKeyboardViewController>();
                    _passwordKeyboard.enterButtonPressed += PasswordEntered;
                }

                _passwordKeyboard._inputString = "";
                _serverHubNavigationController.PresentModalViewController(_passwordKeyboard, null);
            }
            else
            {
                if(_roomNavigationController == null)
                {
                    _roomNavigationController = BeatSaberUI.CreateViewController<RoomNavigationController>();
                }

                _serverHubNavigationController.PresentModalViewController(_roomNavigationController, null);

                if (Client.instance == null)
                {
                    Client.CreateClient();
                }
                if (!Client.instance.Connected || (Client.instance.Connected && (Client.instance.ip != ip || Client.instance.port != port)))
                {
                    Client.instance.Disconnect(true);
                    Client.instance.Connect(ip, port);
                    Client.instance.ConnectedToServerHub += ConnectedToServerHub;
                }
                else
                {
                    ConnectedToServerHub();
                }

            }
        }

        public void LeaveRoom()
        {
            if(joined)
                Client.instance.LeaveRoom();
            Client.instance.PacketReceived -= PacketReceived;
        }

        private void PasswordEntered(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                JoinRoom(_serverHubNavigationController, ip, port, roomId, usePassword, input);
            }
        }

        private void ConnectedToServerHub()
        {
            Client.instance.ConnectedToServerHub -= ConnectedToServerHub;
            Client.instance.PacketReceived += PacketReceived;
            if (usePassword)
            {
                Client.instance.JoinRoom(roomId, password);
            }
            else
            {
                Client.instance.JoinRoom(roomId);
            }
        }
        
        private void PacketReceived(BasePacket packet)
        {
            Log.Info("ReceivedPacket "+packet.commandType+" "+packet.additionalData[0]);
            if (!joined)
            {
                if(packet.commandType == CommandType.JoinRoom)
                {
                    switch (packet.additionalData[0])
                    {
                        case 0:
                            {
                                joined = true;
                            }
                            break;
                        case 1:
                            {
                                _roomNavigationController.DisplayError("Room not found");
                            }
                            break;
                        case 2:
                            {
                                _roomNavigationController.DisplayError("Incorrect password");
                            }
                            break;
                        case 3:
                            {
                                _roomNavigationController.DisplayError("Too much players");
                            }
                            break;
                        default:
                            {
                                _roomNavigationController.DisplayError("Unknown error");
                            }
                            break;

                    }
                }
            }
        }
    }
}

using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen;
using SongLoaderPlugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRUI;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class RoomCreationFlowCoordinator : FlowCoordinator
    {
        RoomCreationServerHubsListViewController _serverHubsViewController;
        MainRoomCreationViewController _mainRoomCreationViewController;
        LeftRoomCreationViewController _leftRoomCreationViewController;

        ServerHubClient _selectedServerHub;
        RoomSettings _roomSettings;

        uint _createdRoomId;

        public void MainCreateRoomButtonPressed(VRUIViewController parentViewController, List<ServerHubClient> serverHubs)
        {
            if(_serverHubsViewController == null)
            {
                _serverHubsViewController = BeatSaberUI.CreateViewController<RoomCreationServerHubsListViewController>();
                _serverHubsViewController.selectedServerHub += ServerHubSelected;
            }

            parentViewController.PresentModalViewController(_serverHubsViewController, null);
            _serverHubsViewController.SetServerHubs(serverHubs);

        }

        public void ServerHubSelected(ServerHubClient serverHubClient)
        {
            _selectedServerHub = serverHubClient;

            if(_mainRoomCreationViewController == null)
            {
                _mainRoomCreationViewController = BeatSaberUI.CreateViewController<MainRoomCreationViewController>();
                _mainRoomCreationViewController.CreatedRoom += CreateRoomPressed;
            }
            if (_leftRoomCreationViewController == null)
            {
                _leftRoomCreationViewController = BeatSaberUI.CreateViewController<LeftRoomCreationViewController>();
                _leftRoomCreationViewController.SetSongs(SongLoader.CustomLevels);
            }

            _serverHubsViewController.PresentModalViewController(_mainRoomCreationViewController, null);
        }

        public bool CheckRequirements()
        {
            return _leftRoomCreationViewController.CheckRequirements() && _mainRoomCreationViewController.CheckRequirements();
        }

        public void LeftAndRightScreenViewControllers(out VRUIViewController leftScreenViewController, out VRUIViewController rightScreenViewController)
        {
            leftScreenViewController = _leftRoomCreationViewController;
            rightScreenViewController = null;
        }

        public void CreateRoomPressed(RoomSettings settings)
        {
            if (!PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements())
                return;

            _roomSettings = settings;
            _roomSettings.AvailableSongs = _leftRoomCreationViewController.selectedSongs.Select(x => new SongInfo() { songName = x.songName + " " + x.songSubName, levelId = x.levelID.Substring(0, 32), songDuration = x.audioClip.length }).ToList();

            if (Client.instance == null)
            {
                Client.CreateClient();
            }
            if(!Client.instance.Connected || (Client.instance.Connected && (Client.instance.ip != _selectedServerHub.ip || Client.instance.port != _selectedServerHub.port)))
            {
                Client.instance.Disconnect(true);
                Client.instance.Connect(_selectedServerHub.ip, _selectedServerHub.port);
                Client.instance.ConnectedToServerHub += ConnectedToServerHub;
            }
            else
            {
                ConnectedToServerHub();
            }
            
        }

        public void ConnectedToServerHub()
        {
            Client.instance.ConnectedToServerHub -= ConnectedToServerHub;
            Client.instance.CreateRoom(_roomSettings);
            Client.instance.PacketReceived += PacketReceived;
        }

        public void PacketReceived(BasePacket packet)
        {
            if(packet.commandType == CommandType.CreateRoom)
            {

                Client.instance.PacketReceived -= PacketReceived;
                _createdRoomId = BitConverter.ToUInt32(packet.additionalData, 0);
                _mainRoomCreationViewController.DismissModalViewController(null, true);
                _serverHubsViewController.DismissModalViewController(null, true);

                PluginUI.instance.serverHubFlowCoordinator.JoinRoom(_selectedServerHub.ip, _selectedServerHub.port, _createdRoomId, _roomSettings.UsePassword, _roomSettings.Password);
            }
        }
    }
}

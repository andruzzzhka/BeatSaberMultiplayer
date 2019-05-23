using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen;
using CustomUI.BeatSaber;
using Lidgren.Network;
using SongLoaderPlugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VRUI;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class RoomCreationFlowCoordinator : FlowCoordinator
    {
        public event Action<bool> didFinishEvent;

        RoomCreationServerHubsListViewController _serverHubsViewController;
        MainRoomCreationViewController _mainRoomCreationViewController;
        PresetsListViewController _presetsListViewController;
        
        BeatmapCharacteristicSO[] _beatmapCharacteristics;

        ServerHubClient _selectedServerHub;
        RoomSettings _roomSettings;

        uint _createdRoomId;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            _beatmapCharacteristics = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>();

            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _serverHubsViewController = BeatSaberUI.CreateViewController<RoomCreationServerHubsListViewController>();
                _serverHubsViewController.selectedServerHub += ServerHubSelected;
                _serverHubsViewController.didFinishEvent += () => { didFinishEvent?.Invoke(false); };

                _mainRoomCreationViewController = BeatSaberUI.CreateViewController<MainRoomCreationViewController>();
                _mainRoomCreationViewController.CreatedRoom += CreateRoomPressed;
                _mainRoomCreationViewController.SavePresetPressed += SavePreset;
                _mainRoomCreationViewController.LoadPresetPressed += LoadPresetPressed;
                _mainRoomCreationViewController.keyboardDidFinishEvent += _mainRoomCreationViewController_keyboardDidFinishEvent;
                _mainRoomCreationViewController.presentKeyboardEvent += PresentKeyboard;
                _mainRoomCreationViewController.didFinishEvent += () => { DismissViewController(_mainRoomCreationViewController); SetLeftScreenViewController(null); };
                
                _presetsListViewController = BeatSaberUI.CreateViewController<PresetsListViewController>();
                _presetsListViewController.didFinishEvent += _presetsListViewController_didFinishEvent;

            }

            ProvideInitialViewControllers(_serverHubsViewController, null, null);
        }

        public void PresentKeyboard(CustomKeyboardViewController keyboardViewController)
        {
            PresentViewController(keyboardViewController, null, false);
        }
        
        private void _mainRoomCreationViewController_keyboardDidFinishEvent(CustomKeyboardViewController keyboardViewController)
        {
            DismissViewController(keyboardViewController, null, false);
        }

        public void SetServerHubsList(List<ServerHubClient> serverHubs)
        {
            _serverHubsViewController.SetServerHubs(serverHubs);
        }

        public void ServerHubSelected(ServerHubClient serverHubClient)
        {
            _selectedServerHub = serverHubClient;
            
            PresentViewController(_mainRoomCreationViewController);
        }

        public bool CheckRequirements()
        {
            return _mainRoomCreationViewController.CheckRequirements();
        }
        
        private void SavePreset(RoomSettings settings, string name)
        {
            RoomPreset preset = new RoomPreset(settings);
            preset.SavePreset("UserData/RoomPresets/"+name+".json");
            PresetsCollection.ReloadPresets();
        }

        private void LoadPresetPressed()
        {
            _presetsListViewController.SetPresets(PresetsCollection.loadedPresets);
            PresentViewController(_presetsListViewController);
            SetLeftScreenViewController(null);
        }


        private void _presetsListViewController_didFinishEvent(RoomPreset selectedPreset)
        {
            DismissViewController(_presetsListViewController);

            if (selectedPreset != null)
            {
                RoomSettings settings = selectedPreset.GetRoomSettings();

                _mainRoomCreationViewController.ApplyRoomSettings(settings);
            }
        }

        public void CreateRoomPressed(RoomSettings settings)
        {
            if (!PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements())
                return;

            _roomSettings = settings;

            _mainRoomCreationViewController.CreateButtonInteractable(false);
            
            if(!Client.Instance.connected || (Client.Instance.ip != _selectedServerHub.ip || Client.Instance.port != _selectedServerHub.port))
            {
                Client.Instance.Disconnect();
                Client.Instance.Connect(_selectedServerHub.ip, _selectedServerHub.port);
                Client.Instance.ConnectedToServerHub -= ConnectedToServerHub;
                Client.Instance.ConnectedToServerHub += ConnectedToServerHub;
            }
            else
            {
                ConnectedToServerHub();
            }
            
        }

        public void ConnectedToServerHub()
        {
            Client.Instance.ConnectedToServerHub -= ConnectedToServerHub;
            Client.Instance.CreateRoom(_roomSettings);
            Client.Instance.MessageReceived -= PacketReceived;
            Client.Instance.MessageReceived += PacketReceived;
        }

        public void PacketReceived(NetIncomingMessage msg)
        {
            if ((CommandType)msg.ReadByte() == CommandType.CreateRoom)
            {
                _mainRoomCreationViewController.CreateButtonInteractable(true);

                Client.Instance.MessageReceived -= PacketReceived;
                _createdRoomId = msg.ReadUInt32();
                
                DismissViewController(_mainRoomCreationViewController, null, true);
                SetLeftScreenViewController(null);
                didFinishEvent?.Invoke(true);

                PluginUI.instance.serverHubFlowCoordinator.doNotUpdate = true;
                PluginUI.instance.serverHubFlowCoordinator.JoinRoom(_selectedServerHub.ip, _selectedServerHub.port, _createdRoomId, _roomSettings.UsePassword, _roomSettings.Password);
            }
        }
    }
}

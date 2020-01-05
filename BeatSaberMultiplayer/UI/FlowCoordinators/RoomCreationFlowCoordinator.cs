using BeatSaberMarkupLanguage;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen;
using HMUI;
using Lidgren.Network;
using System;
using System.Collections.Generic;
using UnityEngine;

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

                _mainRoomCreationViewController = BeatSaberUI.CreateViewController<MainRoomCreationViewController>();
                _mainRoomCreationViewController.CreatedRoom += CreateRoomPressed;
                _mainRoomCreationViewController.SavePresetPressed += SavePreset;
                _mainRoomCreationViewController.LoadPresetPressed += LoadPresetPressed;
                
                _presetsListViewController = BeatSaberUI.CreateViewController<PresetsListViewController>();
                _presetsListViewController.didFinishEvent += _presetsListViewController_didFinishEvent;

            }

            showBackButton = true;

            ProvideInitialViewControllers(_serverHubsViewController, null, null);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController == _serverHubsViewController)
                didFinishEvent?.Invoke(false);
            else if (topViewController == _mainRoomCreationViewController)
            {
                DismissViewController(_mainRoomCreationViewController);
                SetLeftScreenViewController(null);
            }
            else if (topViewController == _presetsListViewController)
                _presetsListViewController_didFinishEvent(null);
        }

        /*
        public void PresentKeyboard(KeyboardViewController keyboardViewController)
        {
            PresentViewController(keyboardViewController, null, false);
        }
        
        private void _mainRoomCreationViewController_keyboardDidFinishEvent(KeyboardViewController keyboardViewController)
        {
            DismissViewController(keyboardViewController, null, false);
        }
        */

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

            _mainRoomCreationViewController.SetCreateButtonInteractable(false);
            
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
            msg.Position = 0;
            if ((CommandType)msg.ReadByte() == CommandType.CreateRoom)
            {
                _mainRoomCreationViewController.SetCreateButtonInteractable(true);

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

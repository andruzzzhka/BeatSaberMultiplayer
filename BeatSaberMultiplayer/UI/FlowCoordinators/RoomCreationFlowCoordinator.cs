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
        LeftRoomCreationViewController _leftRoomCreationViewController;
        PresetsListViewController _presetsListViewController;

        LevelCollectionSO _levelCollection;
        BeatmapCharacteristicSO[] _beatmapCharacteristics;

        ServerHubClient _selectedServerHub;
        RoomSettings _roomSettings;

        uint _createdRoomId;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            _beatmapCharacteristics = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>();
            _levelCollection = SongLoader.CustomLevelCollectionSO;

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
                _mainRoomCreationViewController.didFinishEvent += () => { DismissViewController(_mainRoomCreationViewController); SetLeftScreenViewController(null); };

                _leftRoomCreationViewController = BeatSaberUI.CreateViewController<LeftRoomCreationViewController>();
                _leftRoomCreationViewController.SetSongs(_levelCollection.GetLevelsWithBeatmapCharacteristic(_beatmapCharacteristics.First(x => x.characteristicName == "Standard")).ToList());

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
            SetLeftScreenViewController(_leftRoomCreationViewController);
        }

        public bool CheckRequirements()
        {
            return _leftRoomCreationViewController.CheckRequirements() && _mainRoomCreationViewController.CheckRequirements();
        }
        
        private void SavePreset(RoomSettings settings, string name)
        {
            RoomSettings presetSettings = new RoomSettings();
            presetSettings = settings;
            presetSettings.AvailableSongs = _leftRoomCreationViewController.selectedSongs.Select(x => new SongInfo() { songName = x.songName + " " + x.songSubName, levelId = x.levelID.Substring(0, Math.Min(32, x.levelID.Length)), songDuration = x.audioClip.length }).ToList();
            RoomPreset preset = new RoomPreset(presetSettings);
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
            SetLeftScreenViewController(_leftRoomCreationViewController);

            if (selectedPreset != null)
            {
                RoomSettings settings = selectedPreset.GetRoomSettings();

                _mainRoomCreationViewController.ApplyRoomSettings(settings);
                _leftRoomCreationViewController.SelectSongs(settings.AvailableSongs.Select(x => x.levelId).ToList());
            }
        }

        public void CreateRoomPressed(RoomSettings settings)
        {
            if (!PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements())
                return;

            _roomSettings = settings;
            _roomSettings.AvailableSongs = _leftRoomCreationViewController.selectedSongs.Select(x => new SongInfo() { songName = x.songName + " " + x.songSubName, levelId = x.levelID.Substring(0, Math.Min(32, x.levelID.Length)), songDuration = x.audioClip.length }).ToList();

            _mainRoomCreationViewController.CreateButtonInteractable(false);
            
            if(!Client.Instance.Connected || (Client.Instance.Connected && (Client.Instance.ip != _selectedServerHub.ip || Client.Instance.port != _selectedServerHub.port)))
            {
                Client.Instance.Disconnect(true);
                Client.Instance.Connect(_selectedServerHub.ip, _selectedServerHub.port);
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

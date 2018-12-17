using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen;
using CustomUI.BeatSaber;
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
            preset.SavePreset("UserData\\RoomPresets\\"+name+".json");
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

            RoomSettings settings = selectedPreset.GetRoomSettings();
            
            _mainRoomCreationViewController.ApplyRoomSettings(settings);
            _leftRoomCreationViewController.SelectSongs(settings.AvailableSongs.Select(x=>x.levelId).ToList());
        }

        public void CreateRoomPressed(RoomSettings settings)
        {
            if (!PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements())
                return;

            _roomSettings = settings;
            _roomSettings.AvailableSongs = _leftRoomCreationViewController.selectedSongs.Select(x => new SongInfo() { songName = x.songName + " " + x.songSubName, levelId = x.levelID.Substring(0, Math.Min(32, x.levelID.Length)), songDuration = x.audioClip.length }).ToList();

            _mainRoomCreationViewController.CreateButtonInteractable(false);

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
            Client.instance.PacketReceived -= PacketReceived;
            Client.instance.PacketReceived += PacketReceived;
        }

        public void PacketReceived(BasePacket packet)
        {
            if(packet.commandType == CommandType.CreateRoom)
            {
                _mainRoomCreationViewController.CreateButtonInteractable(true);

                Client.instance.PacketReceived -= PacketReceived;
                _createdRoomId = BitConverter.ToUInt32(packet.additionalData, 0);
                
                DismissViewController(_mainRoomCreationViewController, null, true);
                SetLeftScreenViewController(null);
                didFinishEvent?.Invoke(true);
                
                PluginUI.instance.serverHubFlowCoordinator.JoinRoom(_selectedServerHub.ip, _selectedServerHub.port, _createdRoomId, _roomSettings.UsePassword, _roomSettings.Password);
            }
        }
    }
}

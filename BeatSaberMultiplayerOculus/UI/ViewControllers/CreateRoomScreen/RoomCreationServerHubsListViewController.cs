using BeatSaberMultiplayer.UI.FlowCoordinators;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen
{
    class RoomCreationServerHubsListViewController : VRUIViewController, TableView.IDataSource
    {
        public event Action<ServerHubClient> selectedServerHub;

        private Button _backButton;
        private Button _pageUpButton;
        private Button _pageDownButton;

        private TextMeshProUGUI _selectText;

        TableView _serverHubsTableView;
        StandardLevelListTableCell _serverTableCellInstance;

        List<ServerHubClient> _serverHubClients = new List<ServerHubClient>();

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                if(activationType == ActivationType.AddedToHierarchy)
                {
                    _serverTableCellInstance = Resources.FindObjectsOfTypeAll<StandardLevelListTableCell>().First(x => (x.name == "StandardLevelListTableCell"));

                    _selectText = BeatSaberUI.CreateText(rectTransform, "Select ServerHub", new Vector2(0f, -5f));
                    _selectText.alignment = TextAlignmentOptions.Center;
                    _selectText.fontSize = 7f;

                    _backButton = BeatSaberUI.CreateBackButton(rectTransform);
                    _backButton.onClick.AddListener(delegate () { DismissModalViewController(null, false); });

                    _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                    (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                    (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                    (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -14f);
                    _pageUpButton.interactable = true;
                    _pageUpButton.onClick.AddListener(delegate ()
                    {
                        _serverHubsTableView.PageScrollUp();

                    });
                    _pageUpButton.interactable = false;

                    _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                    (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                    (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                    (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 8f);
                    _pageDownButton.interactable = true;
                    _pageDownButton.onClick.AddListener(delegate ()
                    {
                        _serverHubsTableView.PageScrollDown();

                    });
                    _pageDownButton.interactable = false;

                    _serverHubsTableView = new GameObject().AddComponent<TableView>();
                    _serverHubsTableView.transform.SetParent(rectTransform, false);

                    Mask viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<Mask>().First(), _serverHubsTableView.transform, false);
                    viewportMask.transform.DetachChildren();
                    _serverHubsTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                    (_serverHubsTableView.transform as RectTransform).anchorMin = new Vector2(0.3f, 0.5f);
                    (_serverHubsTableView.transform as RectTransform).anchorMax = new Vector2(0.7f, 0.5f);
                    (_serverHubsTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                    (_serverHubsTableView.transform as RectTransform).position = new Vector3(0f, 0f, 2.4f);
                    (_serverHubsTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, -3f);

                    ReflectionUtil.SetPrivateField(_serverHubsTableView, "_pageUpButton", _pageUpButton);
                    ReflectionUtil.SetPrivateField(_serverHubsTableView, "_pageDownButton", _pageDownButton);

                    _serverHubsTableView.didSelectRowEvent += ServerHubs_didSelectRowEvent;
                    _serverHubsTableView.dataSource = this;
                }
            }
            else
            {
                _serverHubsTableView.ReloadData();
            }
        }

        public void SetServerHubs(List<ServerHubClient> serverHubClients)
        {
            _serverHubClients = serverHubClients.OrderBy(x => x.availableRooms.Count).ToList();

            if (_serverHubsTableView.dataSource != this)
            {
                _serverHubsTableView.dataSource = this;
            }
            else
            {
                _serverHubsTableView.ReloadData();
            }

            _serverHubsTableView.ScrollToRow(0, false);
        }

        private void ServerHubs_didSelectRowEvent(TableView sender, int row)
        {
            selectedServerHub?.Invoke(_serverHubClients[row]);
        }

        public TableCell CellForRow(int row)
        {
            StandardLevelListTableCell cell = Instantiate(_serverTableCellInstance);

            ServerHubClient client = _serverHubClients[row];

            cell.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(x => x.name == "CoverImage").enabled = false;
            cell.songName = $"{client.ip}:{client.port}";
            cell.author = $"{client.availableRooms.Count} rooms";

            return cell;
        }

        public int NumberOfRows()
        {
            return _serverHubClients.Count; 
        }

        public float RowHeight()
        {
            return 10f;
        }
    }
}

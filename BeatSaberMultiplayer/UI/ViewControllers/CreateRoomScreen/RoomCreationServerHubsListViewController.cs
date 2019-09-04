using BeatSaberMultiplayer.UI.FlowCoordinators;
using CustomUI.BeatSaber;
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
        public event Action didFinishEvent;
        public event Action<ServerHubClient> selectedServerHub;

        private Button _backButton;
        private Button _pageUpButton;
        private Button _pageDownButton;

        private TextMeshProUGUI _selectText;

        TableView _serverHubsTableView;
        LevelListTableCell _serverTableCellInstance;

        List<ServerHubClient> _serverHubClients = new List<ServerHubClient>();

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                if(activationType == ActivationType.AddedToHierarchy)
                {
                    _serverTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell"));

                    _selectText = BeatSaberUI.CreateText(rectTransform, "Select ServerHub", new Vector2(0f, 35.5f));
                    _selectText.alignment = TextAlignmentOptions.Center;
                    _selectText.fontSize = 7f;

                    _backButton = BeatSaberUI.CreateBackButton(rectTransform);
                    _backButton.onClick.AddListener(delegate () { didFinishEvent?.Invoke(); });

                    _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == "PageUpButton")), rectTransform, false);
                    (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                    (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                    (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -4.5f);
                    (_pageUpButton.transform as RectTransform).sizeDelta = new Vector2(40f, 10f);
                    _pageUpButton.interactable = true;
                    _pageUpButton.onClick.AddListener(delegate ()
                    {
                        _serverHubsTableView.GetPrivateField<TableViewScroller>("_scroller").PageScrollUp();

                    });
                    _pageUpButton.interactable = true;

                    _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                    (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                    (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                    (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -1f);
                    (_pageDownButton.transform as RectTransform).sizeDelta = new Vector2(40f, 10f);
                    _pageDownButton.interactable = true;
                    _pageDownButton.onClick.AddListener(delegate ()
                    {
                        _serverHubsTableView.GetPrivateField<TableViewScroller>("_scroller").PageScrollDown();

                    });
                    _pageDownButton.interactable = true;

                    RectTransform container = new GameObject("Container", typeof(RectTransform)).transform as RectTransform;
                    container.SetParent(rectTransform, false);
                    container.anchorMin = new Vector2(0.3f, 0.5f);
                    container.anchorMax = new Vector2(0.7f, 0.5f);
                    container.sizeDelta = new Vector2(0f, 60f);
                    container.anchoredPosition = new Vector2(0f, -3f);

                    var tableGameObject = new GameObject("CustomTableView");
                    tableGameObject.SetActive(false);
                    _serverHubsTableView = tableGameObject.AddComponent<TableView>();
                    _serverHubsTableView.gameObject.AddComponent<RectMask2D>();
                    _serverHubsTableView.transform.SetParent(container, false);

                    _serverHubsTableView.SetPrivateField("_isInitialized", false);
                    _serverHubsTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);

                    RectTransform viewport = new GameObject("Viewport").AddComponent<RectTransform>();
                    viewport.SetParent(_serverHubsTableView.transform, false);
                    (viewport.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                    (viewport.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                    (viewport.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                    _serverHubsTableView.GetComponent<ScrollRect>().viewport = viewport;
                    _serverHubsTableView.Init();

                    (_serverHubsTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                    (_serverHubsTableView.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                    (_serverHubsTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                    (_serverHubsTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, 0f);

                    tableGameObject.SetActive(true);

                    _serverHubsTableView.didSelectCellWithIdxEvent += ServerHubs_didSelectRowEvent;
                    _serverHubsTableView.dataSource = this;
                    _serverHubsTableView.ScrollToCellWithIdx(0, TableViewScroller.ScrollPositionType.Beginning, false);
                }
            }
            else
            {
                _serverHubsTableView.dataSource = this;
            }
        }

        public void SetServerHubs(List<ServerHubClient> serverHubClients)
        {
            _serverHubClients = serverHubClients.OrderByDescending(x => x.serverHubCompatible ? 2 : (x.serverHubAvailable ? 1 : 0)).ThenBy(x => x.ping).ThenBy(x => x.availableRoomsCount).ToList();

            if (_serverHubsTableView != null)
            {
                if (_serverHubsTableView.dataSource != this)
                {
                    _serverHubsTableView.dataSource = this;
                }
                else
                {
                    _serverHubsTableView.ReloadData();
                }

                _serverHubsTableView.ScrollToCellWithIdx(0, TableViewScroller.ScrollPositionType.Beginning, false);
            }
        }

        private void ServerHubs_didSelectRowEvent(TableView sender, int row)
        {
            if(_serverHubClients[row].serverHubCompatible)
                selectedServerHub?.Invoke(_serverHubClients[row]);
        }

        public TableCell CellForIdx(TableView sender, int row)
        {
            LevelListTableCell cell = Instantiate(_serverTableCellInstance);
            cell.reuseIdentifier = "ServerHubCell";

            ServerHubClient client = _serverHubClients[row];

            cell.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).First(x => x.name == "CoverImage").enabled = false;
            cell.SetText($"{(!client.serverHubCompatible ? (client.serverHubAvailable ? "<color=yellow>" : "<color=red>") : "")}{client.ip}:{client.port}");

            if (client.serverHubCompatible)
            {
                cell.SetSubText($"{client.playersCount} players, {client.availableRoomsCount} rooms,  ping: {Mathf.RoundToInt(client.ping * 1000)}");
            }
            else
            {
                cell.SetSubText($"{(client.serverHubAvailable ? "VERSION MISMATCH" : "SERVER DOWN")}");
            }

            cell.SetPrivateField("_beatmapCharacteristicAlphas", new float[0]);
            cell.SetPrivateField("_beatmapCharacteristicImages", new UnityEngine.UI.Image[0]);
            cell.SetPrivateField("_bought", true);
            foreach (var icon in cell.GetComponentsInChildren<UnityEngine.UI.Image>().Where(x => x.name.StartsWith("LevelTypeIcon")))
            {
                Destroy(icon.gameObject);
            }

            return cell;
        }

        public int NumberOfCells()
        {
            return _serverHubClients.Count; 
        }

        public float CellSize()
        {
            return 10f;
        }
    }
}

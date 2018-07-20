using BeatSaberMultiplayer.Data;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer
{
    class MultiplayerServerListViewController : VRUIViewController, TableView.IDataSource
    {
        BSMultiplayerUI ui;
        MultiplayerServerHubViewController _parentMasterViewController;

        Button _pageUpButton;
        Button _pageDownButton;

        TableView _serverTableView;
        StandardLevelListTableCell _serverTableCellInstance;

        List<Data.Data> availableServers = new List<Data.Data>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            ui = BSMultiplayerUI._instance;

            _parentMasterViewController = transform.parent.GetComponent<MultiplayerServerHubViewController>();

            _serverTableCellInstance = Resources.FindObjectsOfTypeAll<StandardLevelListTableCell>().First(x => (x.name == "StandardLevelListTableCell"));

            if (_pageUpButton == null)
            {
                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -14f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _serverTableView.PageScrollUp();

                });
                _pageUpButton.interactable = false;
            }

            if (_pageDownButton == null)
            {
                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 8f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _serverTableView.PageScrollDown();

                });
                _pageDownButton.interactable = false;
            }

            if (_serverTableView == null)
            {
                _serverTableView = new GameObject().AddComponent<TableView>();

                _serverTableView.transform.SetParent(rectTransform, false);

                Mask viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<Mask>().First(), _serverTableView.transform, false);
                viewportMask.transform.DetachChildren();

                _serverTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                (_serverTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0.5f);
                (_serverTableView.transform as RectTransform).anchorMax = new Vector2(1f, 0.5f);
                (_serverTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                (_serverTableView.transform as RectTransform).position = new Vector3(0f, 0f, 2.4f);
                (_serverTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, -3f);
                
                ReflectionUtil.SetPrivateField(_serverTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_serverTableView, "_pageDownButton", _pageDownButton);

                _serverTableView.didSelectRowEvent += ServerTableView_DidSelectRow;

                _serverTableView.dataSource = this;

            }
            else
            {
                _serverTableView.ReloadData();
            }



        }

        public void SetServers(List<Data.Data> servers)
        {
            availableServers = new List<Data.Data>(servers).Distinct(new ServerEqualityComparer()).ToList();

            if (_serverTableView.dataSource != this)
            {
                _serverTableView.dataSource = this;
            }
            else
            {
                _serverTableView.ReloadData();
            }

            _serverTableView.ScrollToRow(0, false);
        }

        public void AddServers(List<Data.Data> servers)
        {
            availableServers.AddRange(servers);
            availableServers = availableServers.Distinct(new ServerEqualityComparer()).ToList();

            if (_serverTableView.dataSource != this)
            {
                _serverTableView.dataSource = this;
            }
            else
            {
                _serverTableView.ReloadData();
            }

            _serverTableView.ScrollToRow(0, false);
        }

        private void ServerTableView_DidSelectRow(TableView sender, int row)
        {
            _parentMasterViewController.ConnectToServer(availableServers[row].IPv4, availableServers[row].Port);

        }

        public TableCell CellForRow(int row)
        {
            StandardLevelListTableCell cell = Instantiate(_serverTableCellInstance);

            Destroy(cell.GetComponentsInChildren<UnityEngine.UI.Image>().First(x => x.name.ToLower().Contains("cover")));
            cell.songName = availableServers[row].Name;
            cell.author = $"{availableServers[row].IPv4}:{availableServers[row].Port}";

            return cell;
        }

        public int NumberOfRows()
        {
            return availableServers.Count;
        }

        public float RowHeight()
        {
            return 10f;
        }


    }

    class ServerEqualityComparer : IEqualityComparer<Data.Data>
    {
        public bool Equals(Data.Data x, Data.Data y)
        {
            return (x.IPv4 == y.IPv4) && (x.Name == y.Name) && (x.Port == y.Port);
        }

        public int GetHashCode(Data.Data obj)
        {
            return obj.IPv4.GetHashCode() + obj.Port.GetHashCode() + obj.Name.GetHashCode();
        }
    }
}

using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public TableView _serverTableView;
        SongListTableCell _serverTableCellInstance;

        List<Data.Data> availableServers = new List<Data.Data>();

        protected override void DidActivate()
        {
            ui = BSMultiplayerUI._instance;

            _parentMasterViewController = transform.parent.GetComponent<MultiplayerServerHubViewController>();

            _serverTableCellInstance = Resources.FindObjectsOfTypeAll<SongListTableCell>().First(x => (x.name == "SongListTableCell"));

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

                _serverTableView.DidSelectRowEvent += ServerTableView_DidSelectRow;

            }

        }

        public void SetServers(List<Data.Data> servers)
        {
            availableServers = servers;
            if((object)_serverTableView.dataSource != this)
            {
                _serverTableView.dataSource = this;
            }
            else
            {
                _serverTableView.ReloadData();
            }
        }

        private void ServerTableView_DidSelectRow(TableView sender, int row)
        {
            _parentMasterViewController.ConnectToServer(availableServers[row].IPv4, availableServers[row].Port);
        }

        public TableCell CellForRow(int row)
        {
            SongListTableCell cell = Instantiate(_serverTableCellInstance);

            Destroy(cell.GetComponentsInChildren<UnityEngine.UI.Image>().First(x => x.name.ToLower().Contains("cover")));
            cell.songName = availableServers[row].Name;
            cell.author = $"{availableServers[row].IPv4}:{availableServers[row].Port}";

            return cell;
        }

        public int NumberOfRows()
        {
            return Math.Min(availableServers.Count, 6);
        }

        public float RowHeight()
        {
            return 10f;
        }


    }
}

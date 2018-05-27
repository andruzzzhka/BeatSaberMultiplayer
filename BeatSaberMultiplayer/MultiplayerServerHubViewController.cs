using BeatSaberMultiplayer.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer
{
    class MultiplayerServerHubViewController : VRUINavigationController
    {
        BSMultiplayerUI ui;

        Button _backButton;
        Button _enterIPButton;

        MultiplayerServerListViewController _multiplayerServerList;

        GameObject _loadingIndicator;
        private bool isLoading = false;
        public bool _loading { get { return isLoading; } set { isLoading = value; SetLoadingIndicator(isLoading); } }
        
        TcpClient _serverHubConnection;
        

        protected override void DidActivate()
        {
            ui = BSMultiplayerUI._instance;

            if (_backButton == null)
            {
                _backButton = ui.CreateBackButton(rectTransform);

                _backButton.onClick.AddListener(delegate ()
                {
                    if(_serverHubConnection != null && _serverHubConnection.Connected)
                    {
                        _serverHubConnection.Close();
                        _loading = false;
                    }
                    DismissModalViewController(null, false);

                });
            }

            if (_enterIPButton == null)
            {
                _enterIPButton = ui.CreateUIButton(rectTransform, "ApplyButton");
                ui.SetButtonText(ref _enterIPButton, "Connect to 127.0.0.1:3701");
                ui.SetButtonTextSize(ref _enterIPButton, 3f);
                (_enterIPButton.transform as RectTransform).sizeDelta = new Vector2(38f, 6f);
                (_enterIPButton.transform as RectTransform).anchoredPosition = new Vector2(-19f, 73f);
                _enterIPButton.onClick.RemoveAllListeners();
                _enterIPButton.onClick.AddListener(delegate ()
                {
                    ConnectToServer("127.0.0.1",3701);
                });
            }

            if (_loadingIndicator == null)
            {
                try
                {
                    _loadingIndicator = ui.CreateLoadingIndicator(rectTransform);
                    (_loadingIndicator.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
                    (_loadingIndicator.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
                    (_loadingIndicator.transform as RectTransform).anchoredPosition = new Vector2(0f, 0f);
                    _loadingIndicator.SetActive(true);

                }
                catch (Exception e)
                {
                    Console.WriteLine("EXCEPTION: " + e);
                }
            }

            if (_multiplayerServerList == null)
            {
                _multiplayerServerList = ui.CreateViewController<MultiplayerServerListViewController>();
                _multiplayerServerList.rectTransform.anchorMin = new Vector2(0.3f, 0f);
                _multiplayerServerList.rectTransform.anchorMax = new Vector2(0.7f, 1f);

                PushViewController(_multiplayerServerList, true);

            }
            else
            {
                if (_viewControllers.IndexOf(_multiplayerServerList) < 0)
                {
                    PushViewController(_multiplayerServerList, true);
                }

            }

            _multiplayerServerList._currentPage = 0;
            GetPage(_multiplayerServerList._currentPage);


        }

        internal void GetPage(int currentPage)
        {
            if (!_loading)
            {
                Console.WriteLine($"Getting page {currentPage}");
                try
                {
                    _loading = true;
                    _multiplayerServerList._pageUpButton.interactable = false;
                    _multiplayerServerList._pageDownButton.interactable = false;
                    _serverHubConnection = new TcpClient(Config.Instance.ServerHubIP, Config.Instance.ServerHubPort);
                    ClientDataPacket packet = new ClientDataPacket() { ConnectionType = ConnectionType.Client, Offset = _multiplayerServerList._currentPage };

                    _serverHubConnection.GetStream().Write(packet.ToBytes(), 0, packet.ToBytes().Length);
                    StartCoroutine(WaitForResponse());
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception: {e}");
                    _loading = false;
                    TextMeshProUGUI _errorText = ui.CreateText(rectTransform, "Can't connect to ServerHub!", new Vector2(0f, -48f));
                    _errorText.alignment = TextAlignmentOptions.Center;
                    Destroy(_errorText.gameObject, 4f);
                }
            }
        }


        IEnumerator WaitForDoneProcess(float timeout)
        {
            while (_serverHubConnection.Available <= 0)
            {
                yield return null;
                timeout -= Time.deltaTime;
                if (timeout <= 0f) break;
            }
        }

        IEnumerator WaitForResponse()
        {
            yield return WaitForDoneProcess(5f);

            if(_serverHubConnection.Available <= 0)
            {
                try
                {
                    TextMeshProUGUI _errorText = ui.CreateText(rectTransform, "Can't connect to ServerHub!", new Vector2(0f, -48f));
                    _errorText.alignment = TextAlignmentOptions.Center;
                    Destroy(_errorText.gameObject, 4f);
                    _serverHubConnection.Close();
                    _loading = false;
                }catch(Exception e)
                {
                    Console.WriteLine($"ServerHub Exception: {e}");
                }
                yield break;
            }

            byte[] bytes = new byte[Packet.MAX_BYTE_LENGTH];
            ClientDataPacket packet = null;
            if (_serverHubConnection.GetStream().Read(bytes, 0, bytes.Length) != 0)
            {
                packet = Packet.ToClientDataPacket(bytes);
            }

            _serverHubConnection.Close();
            _loading = false;

            _multiplayerServerList.SetServers((packet as ClientDataPacket).Servers);

        }

        public void ConnectToServer(string serverIP, int serverPort)
        {
            MultiplayerLobbyViewController lobby = ui.CreateViewController<MultiplayerLobbyViewController>();

            lobby.selectedServerIP = serverIP;
            lobby.selectedServerPort = serverPort;

            PresentModalViewController(lobby, null);
        }


        void SetLoadingIndicator(bool loading)
        {
            if (_loadingIndicator)
            {
                _loadingIndicator.SetActive(loading);
            }
        }

    }
}

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
        public bool doNotUpdate;

        BSMultiplayerUI ui;

        Button _backButton;
        Button _enterIPButton;

        MultiplayerServerListViewController _multiplayerServerList;

        GameObject _loadingIndicator;
        private bool isLoading = false;
        public bool _loading { get { return isLoading; } set { isLoading = value; SetLoadingIndicator(isLoading); } }

        List<ServerHubClient> _serverHubs = new List<ServerHubClient>();
        

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            ui = BSMultiplayerUI._instance;

            if (_backButton == null)
            {
                _backButton = ui.CreateBackButton(rectTransform);

                _backButton.onClick.AddListener(delegate ()
                {
                    try
                    {
                        if (_serverHubs != null && _serverHubs.Count > 0)
                        {
                            _serverHubs.ForEach(x =>
                            {
                                x.Disconnect();

                                x.ReceivedServerList -= MultiplayerServerHubViewController_receivedServerList;
                                x.ServerHubException -= MultiplayerServerHubViewController_serverHubException;
                            });
                            _loading = false;
                        }
                    }catch(Exception e)
                    {
                        Console.WriteLine($"Can't disconnect from ServerHubs. Exception: {e}");
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

            _serverHubs.Clear();
            for (int i = 0; i < Config.Instance.ServerHubIPs.Length; i++)
            {
                ServerHubClient client = new GameObject($"ServerHubClient-{i}").AddComponent<ServerHubClient>();
                client.ip = Config.Instance.ServerHubIPs[i];
                if (Config.Instance.ServerHubPorts.Length <= i)
                {
                    client.port = 3700;
                }
                else
                {
                    client.port = Config.Instance.ServerHubPorts[i];
                }
                _serverHubs.Add(client);
            }
            
            if (!doNotUpdate)
            {
                _serverHubs.ForEach(x =>
                {                    
                    x.ReceivedServerList += MultiplayerServerHubViewController_receivedServerList;
                    x.ServerHubException += MultiplayerServerHubViewController_serverHubException;
                });
                GetServers();
            }
            else
            {
                doNotUpdate = false;
            }


        }

        public void UpdatePage()
        {
            _loading = false;
            GetServers();
        }

        internal void GetServers()
        {
            if (!_loading)
            {
                Console.WriteLine($"Get servers from ServerHubs");
                try
                {
                    _loading = true;

                    _multiplayerServerList.SetServers(new List<Data.Data>());
                    _serverHubs.ForEach(x => x.RequestServers());
                    
                    Console.WriteLine("Waiting for response...");
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

        private void MultiplayerServerHubViewController_serverHubException(ServerHubClient sender, Exception e)
        {
            _loading = false;
            TextMeshProUGUI _errorText = ui.CreateText(rectTransform, $"Can't connect to ServerHub @ {sender.ip}:{sender.port}", new Vector2(0f, -48f));
            _errorText.alignment = TextAlignmentOptions.Center;
            Destroy(_errorText.gameObject, 4f);
            Console.WriteLine($"Can't connect to ServerHub @ {sender.ip}:{sender.port}. Exception: {e}");
            _serverHubs.Remove(sender);
        }

        private void MultiplayerServerHubViewController_receivedServerList(ServerHubClient sender, List<Data.Data> servers)
        {
            _loading = false;
            _multiplayerServerList.AddServers(servers);
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

    class ServerHubClient : MonoBehaviour
    {
        private TcpClient serverHubConnection;

        public string ip;
        public int port;

        public event Action<ServerHubClient, List<Data.Data>> ReceivedServerList;
        public event Action<ServerHubClient, Exception> ServerHubException;

        private event Action<List<Data.Data>, int> ReceivedOnePage;

        private List<Data.Data> availableServers = new List<Data.Data>();

        public bool Connect()
        {
            try
            {
                serverHubConnection = new TcpClient(ip, port);

                Console.WriteLine($"Connected to ServerHub @ {ip}:{port}");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Can't connect to ServerHub @ {ip}:{port}. Exception: {e}");
                return false;
            }

        }

        public void Disconnect()
        {
            if (serverHubConnection.Connected)
            {
                Console.WriteLine($"Diconnecting from ServerHub @ {ip}:{port}");
                serverHubConnection.Close();
            }
        }

        public void RequestServers()
        {
            ReceivedOnePage += ServerHubClient_receivedOnePage;
            availableServers.Clear();
            StartCoroutine(RequestPage(0));
        }

        private IEnumerator RequestPage(int page)
        {
            Connect();
            yield return new WaitForSecondsRealtime(0.05f);

            ClientDataPacket packet = new ClientDataPacket() { ConnectionType = ConnectionType.Client, Offset = page };

            serverHubConnection.GetStream().Write(packet.ToBytes(), 0, packet.ToBytes().Length);
            
            yield return WaitForDoneProcess(15f);

            if (serverHubConnection.Available <= 0)
            {
                try
                {
                    serverHubConnection.Close();
                    ServerHubException.Invoke(this, new Exception("ServerHub is offline"));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ServerHub Exception: {e}");
                    ServerHubException.Invoke(this, e);
                }
                yield break;
            }

            byte[] bytes = new byte[Packet.MAX_BYTE_LENGTH];

            packet = null;
            if (serverHubConnection.GetStream().Read(bytes, 0, bytes.Length) != 0)
            {
                packet = Packet.ToClientDataPacket(bytes);
            }
            Disconnect();
            ReceivedOnePage.Invoke(packet.Servers, page);
        }

        private void ServerHubClient_receivedOnePage(List<Data.Data> servers, int page)
        {
            Console.WriteLine($"Received page {page}");
            
            availableServers.AddRange(servers);

            if (servers.Count < 6)
            {
                ReceivedOnePage -= ServerHubClient_receivedOnePage;
                ReceivedServerList.Invoke(this, availableServers);
            }
            else
            {
                StartCoroutine(RequestPage(page+1));
            }
        }
        
        private IEnumerator WaitForDoneProcess(float timeout)
        {
            while (serverHubConnection.Available <= 0)
            {
                yield return null;
                timeout -= Time.deltaTime;
                if (timeout <= 0f) break;
            }
        }
    }
}

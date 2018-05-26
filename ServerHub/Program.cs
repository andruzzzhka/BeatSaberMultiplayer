using System;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using ServerHub.Handlers;
using ServerHub.Misc;

namespace ServerHub {
    class Program {
        private static string IP { get; set; }

        static void Main(string[] args) => new Program().Start(args);

        private ServerListener Listener { get; set; } = new ServerListener();

        private Thread listenerThread { get; set; }

        private static EventHandler onClose { get; set; }

        Program() {
            onClose+=OnClose;
            IP = GetPublicIPv4();
        }
        
        void Start(string[] args) {
            Logger.Instance.Log($"Current IP: {IP}");

            listenerThread = new Thread(() => Listener.Start()) {
                IsBackground = true
            };
            listenerThread.Start();
            while (true) {
                Console.Read();
            }
        }

        string GetPublicIPv4() {
            using (var client = new WebClient()) {
                return client.DownloadString("https://api.ipify.org");
            }
        }
        
        private bool OnClose(CtrlType sig)
        {
            switch (sig)
            {
                case CtrlType.CTRL_C_EVENT:
                    break;
                case CtrlType.CTRL_LOGOFF_EVENT:
                    break;
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                    break;
                case CtrlType.CTRL_CLOSE_EVENT:
                    Listener.Stop();
                    listenerThread.Join();
                    return true;
            }

            return false;
        }
        
        #region Console Close Event
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }
        #endregion
    }
}
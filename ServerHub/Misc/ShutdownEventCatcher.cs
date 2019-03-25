// Author: Benjamin Boyle 
// Email: bboyle1234@gmail.com

using System;
using System.Runtime.InteropServices;

namespace ServerHub.Misc
{
    ///<summary>
    /// Provides all c# console application shutdown scenarios in a single handler
    ///</summary>
    public static class ShutdownEventCatcher
    {

        public static event Action<ShutdownEventArgs> Shutdown;
        static void RaiseShutdownEvent(ShutdownEventArgs args)
        {
            foreach(var action in Shutdown.GetInvocationList())
            {
                try
                {
                    action.DynamicInvoke(args);
                }
                catch (Exception e)
                {
                    Logger.Instance.Warning($"Exception on Shutdown event in {action.Target.GetType().ToString()}.{action.Method.Name}: {e}");
                }
            }
        }

#if (!NETCOREAPP2_0)
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(Kernel32ShutdownHandler handler, bool add);

        private delegate bool Kernel32ShutdownHandler(ShutdownReason reason);
        
        private static Kernel32ShutdownHandler kernel32Handler;

#endif

        /// <summary>
        /// Constructor attaches the shutdown event handlers immediately
        /// </summary>
        static ShutdownEventCatcher()
        {

#if (!NETCOREAPP2_0)
            kernel32Handler = new Kernel32ShutdownHandler(Kernel32_ProcessShuttingDown);

            SetConsoleCtrlHandler(kernel32Handler, true);
#endif
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            var args = new ShutdownEventArgs(ShutdownReason.ReachEndOfMain);
            RaiseShutdownEvent(args);
        }
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var args = new ShutdownEventArgs(e.ExceptionObject as Exception);
            RaiseShutdownEvent(args);
        }
        static bool Kernel32_ProcessShuttingDown(ShutdownReason sig)
        {
            ShutdownEventArgs args = new ShutdownEventArgs(sig);
            RaiseShutdownEvent(args);
            return false;
        }
    }

    public enum ShutdownReason
    {
        /// <summary>
        /// Source is Kernel 32
        /// User has pressed ^C
        /// </summary>
        PressCtrlC = 0,

        /// <summary>
        /// Source is Kernel 32
        /// User has pressed ^Break
        /// </summary>
        PressCtrlBreak = 1,

        /// <summary>
        /// Source is Kernel 32
        /// User has clicked the big "X" to close the console window or a windows message has been sent to the console
        /// </summary>
        ConsoleClosing = 2,

        /// <summary>
        /// Source is Kernel 32
        /// Windows is logging off
        /// </summary>
        WindowsLogOff = 5,

        /// <summary>
        /// Source is Kernel 32
        /// Windows is shutting down
        /// </summary>
        WindowsShutdown = 6,

        /// <summary>
        /// Source is Kernel 32
        /// Program has finished executing
        /// </summary>
        ReachEndOfMain = 1000,

        /// <summary>
        /// Source is AppDomain
        /// Unhandled exception in the program
        /// </summary>
        Exception = 1001
    }

    public class ShutdownEventArgs
    {
        public readonly Exception Exception;
        public readonly ShutdownReason Reason;

        public ShutdownEventArgs(ShutdownReason reason)
        {
            Reason = reason;
            Exception = null;
        }

        public ShutdownEventArgs(Exception exception)
        {
            Reason = ShutdownReason.Exception;
            Exception = exception;
        }
    }
}

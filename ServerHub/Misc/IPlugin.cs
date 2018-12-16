using System;
using System.Collections.Generic;
using System.Text;

namespace ServerHub.Misc
{
    public interface IPlugin
    {
        string Name { get;}
        string Version { get;}
        List<Command> Commands { get;}

        bool Init();

        void ServerStart();
        void ServerShutdown();

        void Tick(object sender, HighResolutionTimerElapsedEventArgs e);
    }
}

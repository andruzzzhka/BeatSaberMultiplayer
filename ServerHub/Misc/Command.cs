using System;

namespace ServerHub.Misc
{
    public struct Command
    {
        public string name;
        public string help;
        public Func<string[], string> function;
    }
}

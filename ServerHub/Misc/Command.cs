using System;
using System.Collections.Generic;
using System.Text;

namespace ServerHub.Misc
{
    public struct Command
    {
        public string name;
        public string help;
        public Func<string[], string> function;
    }
}

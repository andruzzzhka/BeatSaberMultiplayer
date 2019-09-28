using ServerHub.Hub;
using ServerHub.Rooms;
using System;
using System.IO;
using System.Linq;

namespace ServerHub.Misc
{
    class AutoCompletionHandler : IAutoCompleteHandler
    {
        public char[] Separators { get; set; } = new char[] { ' ' };
        
        public string[] GetSuggestions(string text, int index)
        {
            try
            {
                var parsedArgs = Program.ParseLine(text);
                if (text.EndsWith(" "))
                    parsedArgs.Add("");

                if (parsedArgs.Count == 0)
                    return null;
                else if (parsedArgs.Count == 1)
                {
                    return Program.availableCommands.Where(x => x.name.StartsWith(text)).Select(y => y.name).ToArray();
                }
                else if (parsedArgs.Count == 2)
                {
                    switch (parsedArgs[0])
                    {
                        case "blacklist":
                            return new string[] { "add", "remove" }.Where(x => x.StartsWith(parsedArgs[1]) || string.IsNullOrEmpty(parsedArgs[1])).ToArray();
                        case "whitelist":
                            return new string[] { "enable", "disable", "add", "remove" }.Where(x => x.StartsWith(parsedArgs[1]) || string.IsNullOrEmpty(parsedArgs[1])).ToArray();
                        case "tickrate":
                            return new string[] { "30", "60", "90" }.Where(x => x.StartsWith(parsedArgs[1]) || string.IsNullOrEmpty(parsedArgs[1])).ToArray();
                        case "createroom":
                            return Directory.GetFiles("RoomPresets/", "*.json").Where(x => Path.GetFileName(x).StartsWith(parsedArgs[1]) || string.IsNullOrEmpty(parsedArgs[1])).Select(x => $"\"{Path.GetFileName(x)}\"").ToArray();
                        case "cloneroom":
                        case "saveroom":
                        case "destroyroom":
                        case "message":
                            return RoomsController.GetRoomsList().Select(x => x.roomId.ToString()).Where(x => (x.StartsWith(parsedArgs[1]) || string.IsNullOrEmpty(parsedArgs[1]))).ToArray();
                        case "radio":
                            return new string[] { "help", "enable", "disable", "list" }.Where(x => (x.StartsWith(parsedArgs[1]) || string.IsNullOrEmpty(parsedArgs[1])) && parsedArgs[1] != x).ToArray();
                        default:
                            return null;
                    }
                }
                else if (parsedArgs.Count == 3)
                {
                    if(parsedArgs[0] == "radio")
                    {
                        
                        return new string[] { "set", "queue" }.Where(x => (x.StartsWith(parsedArgs[2]) || string.IsNullOrEmpty(parsedArgs[2])) && parsedArgs[2] != x).ToArray();
                    }
                    else
                        return null;
                }
                else if (parsedArgs.Count == 4)
                {
                    if (parsedArgs[0] == "radio")
                    {
                        switch (parsedArgs[2])
                        {
                            case "set":
                                return new string[] { "name", "iconurl", "difficulty" }.Where(x => x.StartsWith(parsedArgs[3]) || string.IsNullOrEmpty(parsedArgs[3])).ToArray();
                            case "queue":
                                return new string[] { "list", "clear", "remove", "add" }.Where(x => x.StartsWith(parsedArgs[3]) || string.IsNullOrEmpty(parsedArgs[3])).ToArray();
                            default:
                                return null;
                        }
                    }
                    else
                        return null;
                }
                else
                    return null;
            }
            catch(Exception e)
            {
#if DEBUG
                Logger.Instance.Log("Exception: "+e);
#endif
                return null;
            }
        }
    }
}

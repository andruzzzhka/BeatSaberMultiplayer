using BeatSaberMultiplayer.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.Misc
{
    static class CustomExtensions
    {
        public static void SetButtonStrokeColor(this Button btn, Color color)
        {
            btn.GetComponentsInChildren<Image>().First(x => x.name == "Stroke").color = color;
        }
        
        public static int FindIndexInList(this List<PlayerInfo> list, PlayerInfo _player)
        {
            return list.FindIndex(x => (x.playerId == _player.playerId) && (x.playerName == _player.playerName));
        }
    }
}

using BeatSaberMultiplayer.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.Misc
{
    public static class CustomExtensions
    {
        public static void SetButtonStrokeColor(this Button btn, Color color)
        {
            btn.GetComponentsInChildren<Image>().First(x => x.name == "Stroke").color = color;
        }
        
        public static int FindIndexInList(this List<PlayerInfo> list, PlayerInfo _player)
        {
            return list.FindIndex(x => (x.playerId == _player.playerId) && (x.playerName == _player.playerName));
        }

        public static TextMeshPro CreateWorldText(Transform parent, string text="TEXT")
        {
            TextMeshPro textMesh = new GameObject("CustomUIText").AddComponent<TextMeshPro>();
            textMesh.transform.SetParent(parent, false);
            textMesh.text = text;
            textMesh.fontSize = 5;
            textMesh.color = Color.white;
            textMesh.font = Resources.Load<TMP_FontAsset>("Teko-Medium SDF No Glow");

            return textMesh;
        }
    }
}

using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.UI.UIElements;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace BeatSaberMultiplayer.Misc
{
    public static class CustomExtensions
    {
        public static void SetButtonStrokeColor(this Button btn, Color color)
        {
            btn.GetComponentsInChildren<UnityEngine.UI.Image>().First(x => x.name == "Stroke").color = color;
        }
        
        public static int FindIndexInList<T>(this List<T> list, T b)
        {
            return list.FindIndex(x => x.Equals(b));
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

        public static T CreateInstance<T>(params object[] args)
        {
            var type = typeof(T);
            var instance = type.Assembly.CreateInstance(
                type.FullName, false,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null, args, null, null);
            return (T)instance;
        }
    }
}

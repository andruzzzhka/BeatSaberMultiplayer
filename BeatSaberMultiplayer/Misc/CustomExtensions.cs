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
        private static Shader _customTextShader;
        public static Shader CustomTextShader
        {
            get
            {
                if(_customTextShader == null)
                {
                    Logger.Info("Loading text shader asset bundle...");
                    AssetBundle assetBundle = AssetBundle.LoadFromStream(Assembly.GetCallingAssembly().GetManifestResourceStream("BeatSaberMultiplayer.Assets.Shader.asset"));
                    _customTextShader = assetBundle.LoadAsset<Shader>("Assets/TextMesh Pro/Resources/Shaders/TMP_SDF_ZeroAlphaWrite_ZWrite.shader");
                }
                return _customTextShader;
            }
        }

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
            textMesh.fontSize = 5f;
            textMesh.color = Color.white;
            textMesh.font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(x => x.name.Contains("Teko-Medium SDF No Glow"));
            textMesh.renderer.material.shader = CustomTextShader;

            return textMesh;
        }

        public static bool IsRotNaN(this PlayerInfo _info)
        {
            return  float.IsNaN(_info.headRot.x)        || float.IsNaN(_info.headRot.y)         || float.IsNaN(_info.headRot.z)         || float.IsNaN(_info.headRot.w) ||
                    float.IsNaN(_info.leftHandRot.x)    || float.IsNaN(_info.leftHandRot.y)     || float.IsNaN(_info.leftHandRot.z)     || float.IsNaN(_info.leftHandRot.w) ||
                    float.IsNaN(_info.rightHandRot.x)   || float.IsNaN(_info.rightHandRot.y)    || float.IsNaN(_info.rightHandRot.z)    || float.IsNaN(_info.rightHandRot.w);
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

        public static T Random<T>(this List<T> list)
        {
            return list[(int)Mathf.Round(UnityEngine.Random.Range(0, list.Count))];
        }

        public static T Random<T>(this T[] list)
        {
            return list[(int)Mathf.Round(UnityEngine.Random.Range(0, list.Length))];
        }

        public static void ToShortArray(this float[] input, short[] output, int offset, int len)
        {
            for (int i = 0; i < len; ++i)
            {
                output[i] = (short)Mathf.Clamp((int)(input[i + offset] * 32767.0f), short.MinValue, short.MaxValue);
            }
        }

        public static void ToFloatArray(this short[] input, float[] output, int length)
        {
            for (int i = 0; i < length; ++i)
            {
                output[i] = input[i] / (float)short.MaxValue;
            }
        }
    }
}

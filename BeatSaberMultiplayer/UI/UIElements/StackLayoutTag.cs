using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Tags;
using HMUI;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.UIElements
{
    public class StackLayoutTag : BSMLTag
    {
        public override string[] Aliases => new[] { "stack" };

        public override GameObject CreateObject(Transform parent)
        {
            GameObject gameObject = new GameObject();
            gameObject.name = "BSMLStackLayoutGroup";
            gameObject.transform.SetParent(parent, false);
            gameObject.AddComponent<StackLayoutGroup>();

            ContentSizeFitter contentSizeFitter = gameObject.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            gameObject.AddComponent<Backgroundable>();

            RectTransform rectTransform = gameObject.transform as RectTransform;
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.sizeDelta = new Vector2(0, 0);

            gameObject.AddComponent<LayoutElement>();
            return gameObject;
        }
    }
}

using BeatSaberMarkupLanguage.Tags;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.UIElements
{
    public class BigButtonTag : BSMLTag
    {
        public override string[] Aliases => new[] { "big-button" };

        public override GameObject CreateObject(Transform parent)
        {
            Button button = MonoBehaviour.Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == "SoloFreePlayButton")), parent, false);
            button.name = "BSMLBigButton";
            button.interactable = true;

            Object.Destroy(button.GetComponent<HoverHint>());
            Object.Destroy(button.GetComponent<LocalizedHoverHint>());

            button.gameObject.AddComponent<BigButtonImages>();

            return button.gameObject;
        }
    }
}
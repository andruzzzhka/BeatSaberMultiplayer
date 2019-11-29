using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.UIElements
{
    class BigButtonImages : MonoBehaviour
    {
        public Image icon;
        public Image bgArtwork;

        public void ApplyIcon(string path)
        {
            if (icon == null)
                icon = GetComponentsInChildren<Image>().FirstOrDefault(x => x.name == "Icon");
            if (icon == null)
                throw new Exception("Unable to find icon image!");

            icon.sprite = Sprites.FindSpriteInAssembly(path);
        }

        public void ApplyArtwork(string path)
        {
            if (bgArtwork == null)
                bgArtwork = GetComponentsInChildren<Image>().FirstOrDefault(x => x.name == "BGArtwork");
            if (bgArtwork == null)
                throw new Exception("Unable to find BG artwork image!");

            bgArtwork.sprite = Sprites.FindSpriteInAssembly(path);
        }
    }
}

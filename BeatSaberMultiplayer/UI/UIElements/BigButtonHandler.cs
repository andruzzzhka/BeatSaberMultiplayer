using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.TypeHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.UIElements
{
    [ComponentHandler(typeof(BigButtonImages))]
    class BigButtonHandler : TypeHandler
    {
        public override Dictionary<string, string[]> Props => new Dictionary<string, string[]>()
        {
            { "icon", new[]{"icon"} },
            { "bgArtwork", new[]{"bg-artwork", "artwork"} }
        };

        public override void HandleType(Component obj, Dictionary<string, string> data, BSMLParserParams parserParams)
        {
            BigButtonImages images = obj as BigButtonImages;
            if (images != null)
            {
                if (data.TryGetValue("icon", out string iconPath))
                {
                    images.ApplyIcon(iconPath);
                }

                if (data.TryGetValue("bgArtwork", out string artworkPath))
                {
                    images.ApplyArtwork(artworkPath);
                }
            }
        }
    }
}

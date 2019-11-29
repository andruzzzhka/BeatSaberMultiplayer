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
    [ComponentHandler(typeof(Image))]
    class ImageHandler : TypeHandler
    {
        public override Dictionary<string, string[]> Props => new Dictionary<string, string[]>()
        {
            { "image", new[]{"image", "source", "src", "img"} }
        }; 
        
        public override void HandleType(Component obj, Dictionary<string, string> data, BSMLParserParams parserParams)
        {
            Image image = obj as Image;
            if (image != null)
            {
                if (data.TryGetValue("image", out string iconPath))
                {
                    image.sprite = Sprites.FindSpriteInAssembly(iconPath);
                }
            }
        }
    }
}

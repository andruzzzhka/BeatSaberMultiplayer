using BeatSaberMarkupLanguage;
using System.Linq;
using UnityEngine;

namespace BeatSaberMultiplayer
{
    public static class Sprites
    {
        public static Material NoGlowMat
        {
            get
            {
                if (noGlowMat == null)
                {
                    noGlowMat = new Material(Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name == "UINoGlow").First());
                    noGlowMat.name = "UINoGlowCustom";
                }
                return noGlowMat;
            }
        }
        private static Material noGlowMat;

        public static Material UIScreenMat
        {
            get
            {
                if (uiMat == null)
                {
                    uiMat = new Material(Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name == "UIBlurredScreenGrab").First());
                    uiMat.name = "UIBlurredScreenGrabCustom";
                }
                return uiMat;
            }

        }
        private static Material uiMat;

        //https://thenounproject.com/term/globe/248/
        //Globe by Edward Boatman from the Noun Project
        public static Sprite onlineIcon;

        public static Sprite lockedRoomIcon;
        public static Sprite addToFavorites;
        public static Sprite removeFromFavorites;
        public static Sprite whitePixel;
        public static Sprite doubleArrow;
        public static Sprite starIcon;

        //by elliotttate#9942
        public static Sprite roomsIcon;

        //by elliotttate#9942
        public static Sprite radioIcon;

        //https://www.flaticon.com/free-icon/thumbs-up_70420
        public static Sprite thumbUp;

        //https://www.flaticon.com/free-icon/dislike-thumb_70485
        public static Sprite thumbDown;

        public static Sprite ratingIcon;

        //https://www.flaticon.com/free-icon/speaker-filled-audio-tool_59284
        public static Sprite speakerIcon;

        //https://www.materialui.co/icon/loop
        public static Sprite refreshIcon;

        public static void ConvertSprites()
        {
            onlineIcon = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.OnlineIcon.png");
            onlineIcon.texture.wrapMode = TextureWrapMode.Clamp;

            lockedRoomIcon = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.LockedRoom.png");
            lockedRoomIcon.texture.wrapMode = TextureWrapMode.Clamp;

            roomsIcon = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.RoomsIcon.png");
            roomsIcon.texture.wrapMode = TextureWrapMode.Clamp;

            radioIcon = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.RadioIcon.png");
            radioIcon.texture.wrapMode = TextureWrapMode.Clamp;

            whitePixel = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.WhitePixel.png");

            doubleArrow = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.DoubleArrowIcon.png");
            doubleArrow.texture.wrapMode = TextureWrapMode.Clamp;

            addToFavorites = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.AddToFavorites.png");
            addToFavorites.texture.wrapMode = TextureWrapMode.Clamp;

            removeFromFavorites = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.RemoveFromFavorites.png");
            removeFromFavorites.texture.wrapMode = TextureWrapMode.Clamp;

            thumbUp = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.ThumbUp.png");
            thumbUp.texture.wrapMode = TextureWrapMode.Clamp;

            thumbDown = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.ThumbDown.png");
            thumbDown.texture.wrapMode = TextureWrapMode.Clamp;

            speakerIcon = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.SpeakerIcon.png");
            speakerIcon.texture.wrapMode = TextureWrapMode.Clamp;

            refreshIcon = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.RefreshIcon.png");
            refreshIcon.texture.wrapMode = TextureWrapMode.Clamp;

            starIcon = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.StarIcon.png");
            starIcon.texture.wrapMode = TextureWrapMode.Clamp;

            ratingIcon = Utilities.FindSpriteInAssembly("BeatSaberMultiplayer.Assets.RatingIcon.png");
            ratingIcon.texture.wrapMode = TextureWrapMode.Clamp;
        }
    }
}

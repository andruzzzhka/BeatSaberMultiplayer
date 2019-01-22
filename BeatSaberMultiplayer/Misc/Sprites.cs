using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                };
                return noGlowMat;
            }
        }
        private static Material noGlowMat;

        public static Sprite onlineIcon;
        public static Sprite lockedRoomIcon;
        public static Sprite addToFavorites;
        public static Sprite removeFromFavorites;
        public static Sprite whitePixel;

        public static Sprite roomsIcon;

        //https://www.flaticon.com/free-icon/radio_727249
        public static Sprite radioIcon;

        //https://www.flaticon.com/free-icon/thumbs-up_70420
        public static Sprite thumbUp;

        //https://www.flaticon.com/free-icon/dislike-thumb_70485
        public static Sprite thumbDown;

        public static void ConvertSprites()
        {
            onlineIcon =          CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.OnlineIcon.png");
            lockedRoomIcon =      CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.LockedRoom.png");
            roomsIcon =           CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.RoomsIcon.png");
            radioIcon =           CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.RadioIcon.png");
            whitePixel =          CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.WhitePixel.png");
            addToFavorites =      CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.AddToFavorites.png");
            removeFromFavorites = CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.RemoveFromFavorites.png");
            thumbUp =             CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.ThumbUp.png");
            thumbDown =           CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.ThumbDown.png");
        }
    }
}

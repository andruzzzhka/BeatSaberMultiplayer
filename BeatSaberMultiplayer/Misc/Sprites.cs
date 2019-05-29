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

        //by elliotttate#9942
        public static Sprite roomsIcon;

        //by elliotttate#9942
        public static Sprite radioIcon;

        //https://www.flaticon.com/free-icon/thumbs-up_70420
        public static Sprite thumbUp;

        //https://www.flaticon.com/free-icon/dislike-thumb_70485
        public static Sprite thumbDown;

        //https://www.flaticon.com/free-icon/speaker-filled-audio-tool_59284
        public static Sprite speakerIcon;

        //https://www.materialui.co/icon/loop
        public static Sprite refreshIcon;

        public static void ConvertSprites()
        {
            onlineIcon =            CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.OnlineIcon.png");
            lockedRoomIcon =        CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.LockedRoom.png");
            roomsIcon =             CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.RoomsIcon.png");
            radioIcon =             CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.RadioIcon.png");
            whitePixel =            CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.WhitePixel.png");
            doubleArrow =           CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.DoubleArrowIcon.png");
            addToFavorites =        CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.AddToFavorites.png");
            removeFromFavorites =   CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.RemoveFromFavorites.png");
            thumbUp =               CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.ThumbUp.png");
            thumbDown =             CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.ThumbDown.png");
            speakerIcon =           CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.SpeakerIcon.png");
            refreshIcon =           CustomUI.Utilities.UIUtilities.LoadSpriteFromResources("BeatSaberMultiplayer.Assets.RefreshIcon.png");
        }
    }
}

using BeatSaberMarkupLanguage;
using BeatSaberMultiplayer.IPAUtilities;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using BeatSaverVoting.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer.Interop
{
    internal static class BeatSaverVotingInterop
    {

        private static FieldAccessor<VotingUI, Transform>.Accessor UpButton;
        private static FieldAccessor<VotingUI, Transform>.Accessor DownButton;
        private static FieldAccessor<VotingUI, IBeatmapLevel>.Accessor LastSong;

        private static VotingUI instance;
        private static RectTransform votingUIHost;

        public static void Setup(MultiplayerResultsViewController resultsView, IBeatmapLevel level)
        {
            if (!resultsView) return;

            if (instance == null)
            {
                Plugin.log.Debug("Setting up BeatSaverVoting interop...");

                var modInfo = IPA.Loader.PluginManager.GetPluginFromId("BeatSaverVoting");

                Plugin.log.Debug("Found BeatSaverVoting plugin!");

                if (modInfo == null) return;

                UpButton = FieldAccessor<VotingUI, Transform>.GetAccessor("upButton");
                DownButton = FieldAccessor<VotingUI, Transform>.GetAccessor("downButton");
                LastSong = FieldAccessor<VotingUI, IBeatmapLevel>.GetAccessor("_lastSong");

                Plugin.log.Debug("Got accessors");

                Assembly votingAssembly = modInfo.Metadata.Assembly;

                instance = VotingUI.instance;

                votingUIHost = new GameObject("VotingUIHost").AddComponent<RectTransform>();
                votingUIHost.SetParent(resultsView.transform, false);
                votingUIHost.anchorMin = Vector2.zero;
                votingUIHost.anchorMax = Vector2.one;
                votingUIHost.sizeDelta = Vector2.zero;
                votingUIHost.anchoredPosition = new Vector2(2.25f, -6f);
                votingUIHost.SetParent(resultsView.resultsTab, true);

                BSMLParser.instance.Parse(Utilities.GetResourceContent(votingAssembly, "BeatSaverVoting.UI.votingUI.bsml"), votingUIHost.gameObject, instance);

                Plugin.log.Debug("Created UI");

                UnityEngine.UI.Image upArrow = UpButton(ref instance).transform.Find("Arrow")?.GetComponent<UnityEngine.UI.Image>();
                UnityEngine.UI.Image downArrow = DownButton(ref instance).transform.Find("Arrow")?.GetComponent<UnityEngine.UI.Image>();
                if (upArrow != null && downArrow != null)
                {
                    upArrow.color = new Color(0.341f, 0.839f, 0.341f);
                    downArrow.color = new Color(0.984f, 0.282f, 0.305f);
                }
            }
            else
            {
                votingUIHost.gameObject.SetActive(true);
            }

            LastSong(ref instance) = level;

            Plugin.log.Debug("Calling GetVotesForMap...");

            instance.InvokePrivateMethod("GetVotesForMap", new object[0]);

            Plugin.log.Debug("Called GetVotesForMap!");
        }

        public static void Hide()
        {
            if(votingUIHost != null)
                votingUIHost.gameObject.SetActive(false);
        }

    }
}

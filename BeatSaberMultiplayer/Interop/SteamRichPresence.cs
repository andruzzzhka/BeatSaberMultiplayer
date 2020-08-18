using Discord;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSaberMultiplayer.Interop
{
    internal static class SteamRichPresence
    {
        private static Callback<GameRichPresenceJoinRequested_t> steamRichPresenceJoinRequested;

        public static void Init()
        {
            steamRichPresenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnSteamGameJoinRequest);
        }

        private static void OnSteamGameJoinRequest(GameRichPresenceJoinRequested_t callback)
        {
            Plugin.instance.OnActivityJoin(callback.m_rgchConnect);
        }

        public static void ClearSteamRichPresence()
        {
            // We can't use ClearRichPresence here because Beat Saber
            // has rich presence data as well and will probably show
            // "Browsing Menus" or something.
            SteamFriends.SetRichPresence("steam_player_group", "");
            SteamFriends.SetRichPresence("steam_player_group_size", "");
            SteamFriends.SetRichPresence("connect", "");
        }

        public static void UpdateSteamRichPresence(Activity discordActivity)
        {
            SteamFriends.SetRichPresence("steam_player_group", discordActivity.Party.Id);
            SteamFriends.SetRichPresence("steam_player_group_size", discordActivity.Party.Size.CurrentSize.ToString());
            SteamFriends.SetRichPresence("connect", discordActivity.Secrets.Join);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRUI;

namespace BeatSaberMultiplayer
{
    class MultiplayerResultsLeaderboardHolder : VRUINavigationController
    {

        MultiplayerLeaderboardViewController _multiplayerLeaderboard;

        protected override void DidActivate()
        {
            if (_multiplayerLeaderboard == null)
            {
                _multiplayerLeaderboard = BSMultiplayerUI._instance.CreateViewController<MultiplayerLeaderboardViewController>();
                _multiplayerLeaderboard.rectTransform.anchorMin = new Vector2(0.1f, 0f);
                _multiplayerLeaderboard.rectTransform.anchorMax = new Vector2(0.9f, 1f);

                PushViewController(_multiplayerLeaderboard, true);
            }

        }

        public void SetLeaderboard(PlayerInfo[] _playerInfos)
        {
            _multiplayerLeaderboard.SetLeaderboard(_playerInfos);
        }

}
}

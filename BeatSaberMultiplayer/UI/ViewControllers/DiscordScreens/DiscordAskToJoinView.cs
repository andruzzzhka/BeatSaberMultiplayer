using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using Discord;
using DiscordCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.ViewControllers.DiscordScreens
{
    class DiscordAskToJoinView : BSMLResourceViewController
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public User user;

        [UIComponent("player-avatar")]
        public RawImage playerAvatar;
        [UIComponent("title-text")]
        public TextMeshProUGUI titleText;

        [UIAction("#post-parse")]
        public void SetupScreen()
        {
            titleText.text = $"<b>{user.Username}#{user.Discriminator}</b> wants to join your game!";

            var imageManager = DiscordClient.GetImageManager(); 

            var handle = new ImageHandle()
            {
                Id = user.Id,
                Size = 256
            };

            imageManager.Fetch(handle, false, (result, img) =>
            {
                if (result == Result.Ok)
                {
                    var texture = imageManager.GetTexture(img);
                    playerAvatar.rectTransform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                    playerAvatar.texture = texture;
                }
            });
        }

        [UIAction("accept-pressed")]
        public void AcceptPressed()
        {
            DiscordClient.GetActivityManager().SendRequestReply(user.Id, ActivityJoinRequestReply.Yes,
                (result) =>
                {
                    Plugin.log.Debug("Accept invite result: " + result);
                }
            );

            Destroy(screen.gameObject);
        }

        [UIAction("decline-pressed")]
        public void DeclinePressed()
        {
            DiscordClient.GetActivityManager().SendRequestReply(user.Id, ActivityJoinRequestReply.No,
                (result) =>
                {
                    Plugin.log.Debug("Decline invite result: " + result);
                }
            );

            Destroy(screen.gameObject);
        }

        [UIAction("ignore-pressed")]
        public void IgnorePressed()
        {
            DiscordClient.GetActivityManager().SendRequestReply(user.Id, ActivityJoinRequestReply.Ignore,
                (result) =>
                {
                    Plugin.log.Debug("Ignore invite result: " + result);
                }
            );

            Destroy(screen.gameObject);
        }


    }
}

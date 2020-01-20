using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using Discord;
using DiscordCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.ViewControllers.DiscordScreens
{
    class DiscordInviteResponseView : BSMLResourceViewController
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public User user;
        public Activity activity;

        [UIComponent("player-avatar")]
        public RawImage playerAvatar;
        [UIComponent("title-text")]
        public TextMeshProUGUI titleText;

        [UIAction("#post-parse")]
        public void SetupScreen()
        {
            titleText.text = $"<b>{user.Username}#{user.Discriminator}</b> invited you to play! ({activity.Party.Size.CurrentSize}/{activity.Party.Size.MaxSize} players)";

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
            Plugin.instance.OnActivityJoin(activity.Secrets.Join);

            Destroy(screen.gameObject);
        }

        [UIAction("decline-pressed")]
        public void DeclinePressed()
        {
            Destroy(screen.gameObject);
        }
    }
}

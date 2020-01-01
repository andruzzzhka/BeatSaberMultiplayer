using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using BS_Utils.Gameplay;
using BS_Utils.Utilities;
using Discord;
using Harmony;
using HMUI;
using IPA;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberMultiplayer
{
    public class Plugin : IBeatSaberPlugin
    {
        public static Plugin instance;
        public static IPA.Logging.Logger log;
        public static Discord.Discord discord;
        public static Discord.Activity discordActivity;
        public static bool overrideDiscordActivity;

        private static bool joinAfterRestart;
        private static string joinSecret;

        public void Init(object nullObject, IPA.Logging.Logger logger)
        {
            log = logger;
        }

        public void OnApplicationStart()
        {
            instance = this;

            BS_Utils.Utilities.BSEvents.OnLoad();
            BS_Utils.Utilities.BSEvents.menuSceneLoadedFresh += MenuSceneLoadedFresh;
            BS_Utils.Utilities.BSEvents.menuSceneLoaded += MenuSceneLoaded;
            BS_Utils.Utilities.BSEvents.gameSceneLoaded += GameSceneLoaded;

            if (Config.Load())
                log.Info("Loaded config!");
            else
                Config.Create();

            try
            {
                PresetsCollection.ReloadPresets();
            }
            catch (Exception e)
            {
                log.Warn("Unable to load presets! Exception: "+e);
            }

            Sprites.ConvertSprites();

            ScrappedData.Instance.DownloadScrappedData(null);

            try
            {
                var harmony = HarmonyInstance.Create("com.andruzzzhka.BeatSaberMultiplayer");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                Plugin.log.Error("Unable to patch assembly! Exception: " + e);
            }

            discord = new Discord.Discord(661577830919962645, (UInt64)Discord.CreateFlags.NoRequireDiscord);

            discord.SetLogHook(Discord.LogLevel.Debug, DiscordLogCallback);
            discord.GetActivityManager().RegisterSteam(620980);

            var activityManager = discord.GetActivityManager();

            activityManager.OnActivityJoin += OnActivityJoin;
            activityManager.OnActivityJoinRequest += ActivityManager_OnActivityJoinRequest;
            activityManager.OnActivityInvite += ActivityManager_OnActivityInvite;
        }

        private void ActivityManager_OnActivityInvite(ActivityActionType type, ref User user, ref Activity activity)
        {
            if (SceneManager.GetActiveScene().name.Contains("Menu") && type == ActivityActionType.Join && !Client.Instance.inRoom && !Client.Instance.inRadioMode)
            {
                PluginUI.instance.ShowInvite(user, activity);
            }
        }

        private void ActivityManager_OnActivityJoinRequest(ref User user)
        {
            if (SceneManager.GetActiveScene().name.Contains("Menu"))
            {
                PluginUI.instance.ShowJoinRequest(user);
            }
        }

        public void OnActivityJoin(string secret)
        {
            if (SceneManager.GetActiveScene().name.Contains("Menu") && !Client.Instance.inRoom && !Client.Instance.inRadioMode)
            {
                joinAfterRestart = true;
                joinSecret = secret;
                Resources.FindObjectsOfTypeAll<MenuTransitionsHelper>().First().RestartGame();
            }
        }

        private void MenuSceneLoadedFresh()
        {
            ModelSaberAPI.HashAllAvatars();
            PluginUI.OnLoad();
            InGameOnlineController.OnLoad();
            SpectatingController.OnLoad();
            GetUserInfo.UpdateUserInfo();

            if (joinAfterRestart)
            {
                joinAfterRestart = false;
                SharedCoroutineStarter.instance.StartCoroutine(PluginUI.instance.JoinGameWithSecret(joinSecret));
                joinSecret = string.Empty;
            }
        }

        private void MenuSceneLoaded()
        {
            InGameOnlineController.Instance?.MenuSceneLoaded();
            if (Config.Instance.SpectatorMode)
                SpectatingController.Instance?.MenuSceneLoaded();
        }

        private void GameSceneLoaded()
        {
            InGameOnlineController.Instance?.GameSceneLoaded();
            if (Config.Instance.SpectatorMode)
                SpectatingController.Instance?.GameSceneLoaded();
        }

        public void OnApplicationQuit()
        {
        }

        public void OnUpdate()
        {
            discord?.RunCallbacks();
            if (Input.GetKeyDown(KeyCode.U))
                PluginUI.instance.ShowInvite(new User() { Id= 123039658580901888, Username="andruzzzhka", Discriminator="6859" }, new Activity() { Secrets = { Join="127.0.0.1:3700?1#test" } });
        }

        public void OnFixedUpdate()
        {
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
        }

        public void OnSceneUnloaded(Scene scene)
        {
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {
        }

        private void DiscordLogCallback(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    {
                        log.Log(IPA.Logging.Logger.Level.Debug, $"[DISCORD] {message}");
                    }
                    break;
                case LogLevel.Info:
                    {
                        log.Log(IPA.Logging.Logger.Level.Info, $"[DISCORD] {message}");
                    }
                    break;
                case LogLevel.Warn:
                    {
                        log.Log(IPA.Logging.Logger.Level.Warning, $"[DISCORD] {message}");
                    }
                    break;
                case LogLevel.Error:
                    {
                        log.Log(IPA.Logging.Logger.Level.Error, $"[DISCORD] {message}");
                    }
                    break;
            }
        }
    }
}

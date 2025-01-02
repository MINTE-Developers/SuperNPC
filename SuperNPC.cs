using Newtonsoft.Json;
using System.Diagnostics.Metrics;

namespace Oxide.Plugins
{
    [Info("Super NPC", "Mint", "1.0.0")]
    [Description("Make your NPCs real players.")]
    public class SuperNPC : RustPlugin
    {
        #region Fields

        private static SuperNPC _instance;

        #endregion

        #region Initialisation / Uninitialisation

        private void OnServerInitialized()
        {
            cmd.AddChatCommand(config.generalSettings.mainCommand, this, nameof(MainCommand));

            _instance = this;
        }

        #endregion

        #region Helpers



        #endregion

        #region Commands

        private void MainCommand(BasePlayer player, string command, params string[ ] args)
        {

        }

        #endregion

        #region Hooks



        #endregion

        #region API



        #endregion

        #region Functions



        #endregion

        #region Agent



        #endregion

        #region Core NPC



        #endregion

        #region Movement



        #endregion

        #region NPC Sensors



        #endregion

        #region Localisation



        #endregion

        #region Configuration

        private ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Debug mode")]
            public bool debug { get; set; }

            [JsonProperty(PropertyName = "General Settings")]
            public GeneralSettings generalSettings { get; set; }

            [JsonProperty(PropertyName = "Chat Settings")]
            public ChatSettings chatSettings { get; set; }
        }

        private class GeneralSettings
        {
            [JsonProperty(PropertyName = "Main command")]
            public string mainCommand { get; set; }

            [JsonProperty(PropertyName = "API key")]
            public string APIKey { get; set; }
        }

        private class ChatSettings
        {
            [JsonProperty(PropertyName = "Chat icon (Steam ID)")]
            public ulong iconID { get; set; }

            [JsonProperty(PropertyName = "Chat prefix")]
            public string chatPrefix { get; set; }

            [JsonProperty(PropertyName = "Chat prefix colour")]
            public string chatPrefixColour { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError($"{Name}.json is corrupted! Recreating a new configuration");
                LoadDefaultConfig();
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData
            {
                debug = false,
                generalSettings = new GeneralSettings()
                {
                    mainCommand = "Super NPC",
                    APIKey = "API-key-here"
                },

                chatSettings = new ChatSettings()
                {
                    iconID = 0,
                    chatPrefix = "Super NPC: ",
                    chatPrefixColour = "#00ff6a"
                },
            };
        }


        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
    }
}

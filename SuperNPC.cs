using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using System;

namespace Oxide.Plugins
{
    [Info("Super NPC", "MINTE", "1.0.0")]
    [Description("Make your NPCs real players.")]
    public class SuperNPC : RustPlugin
    {
        #region Fields

        private static SuperNPC _instance;

        private readonly string _npcPrefab = "assets/rust.ai/agents/npcplayer/npcplayertest.prefab";

        #endregion

        #region Initialisation / Uninitialisation

        private void OnServerInitialized()
        {
            //cmd.AddChatCommand(config.generalSettings.mainCommand, this, nameof(MainCommand));

            _instance = this;
        }

        #endregion

        #region Helpers



        #endregion

        #region Commands

        [ChatCommand("snpc")]
        private void MainCommand(BasePlayer player, string command, params string[ ] args)
        {
            CreateNPC(player.transform.position);
        }

        #endregion

        #region Hooks



        #endregion

        #region API



        #endregion

        #region Functions

        private void CreateNPC(Vector3 spawnPosition)
        {
            var snpc = GameManager.server.CreateEntity(_npcPrefab, spawnPosition) as NPCPlayer;
            if (snpc == null)
                return;

            snpc.Spawn();
            snpc.gameObject.AddComponent<CoreNPC>().InitNPC(snpc);
        }

        #endregion

        #region Agent



        #endregion

        #region Core NPC

        private class CoreNPC : MonoBehaviour
        {
            private NPCPlayer _npc;
            CoreMovement _movement;

            public void InitNPC(NPCPlayer npc)
            {
                _npc = npc;
                _movement = gameObject.AddComponent<CoreMovement>();
                _movement.InitMovement(_npc);
            }
        }

        #endregion

        #region Movement

        private class CoreMovement : CoreNPC
        {
            private NPCPlayer _npc;

            LayerMask layers = ~(1 << LayerMask.NameToLayer("Player (Server)"));

            Coroutine _moveToPosition;
            // Coroutine _rotateToDirection;

            public enum SpeedType
            {
                Walk,
                Sprint,
                Crouch,
                Wounded,
                Swim
            }

            private readonly Dictionary<SpeedType, float> _movementSpeeds = new Dictionary<SpeedType, float>
            {
                { SpeedType.Walk, 2.8f },
                { SpeedType.Sprint, 5.5f },
                { SpeedType.Crouch, 1.7f },
                { SpeedType.Wounded, 0.72f },
                { SpeedType.Swim, 0.33f }
            };
            
            private float GetSpeed(SpeedType speedType) => _movementSpeeds.TryGetValue(speedType, out float speed) ? speed : throw new ArgumentException($"Invalid SpeedType: {speedType}");

            public void InitMovement(NPCPlayer npc)
            {
                _npc = npc;

                // Ensure the NPC starts on the ground
                if (Physics.Raycast(_npc.transform.position + Vector3.up, Vector3.down, out RaycastHit hit, float.MaxValue, layers))
                {
                    _npc.transform.position = hit.point;
                    _npc.ServerPosition = _npc.transform.position;
                    _npc.SendNetworkUpdateImmediate();
                }

                MoveAction(_npc.transform.position + new Vector3(10f, 0f, 10f), SpeedType.Walk);
            }

            /// These are the main functions the agent will call deep neural network (DNN) 
            #region Actions 

            public void MoveAction(Vector3 targetPosition, SpeedType speedType, bool stopMoving = false) 
            {
                if (_moveToPosition != null ) 
                {
                    StopCoroutine(_moveToPosition);
                    _moveToPosition = null;
                }
                
                if (stopMoving)
                    return;

                _moveToPosition = StartCoroutine(MoveToPosition(targetPosition, speedType));
            }

            // public void Rotate(Vector3 targetPosition, SpeedType speedType, bool stopRotate = false) 
            // {
            //     if (_moveToPosition != null ) 
            //     {
            //         StopCoroutine(_rotateToDirection);
            //         _rotateToDirection = null;
            //     }
                
            //     if (stopRotate)
            //         return;

            //     _rotateToDirection = StartCoroutine(MoveToPosition(targetPosition, speedType));
            // }

            #endregion


            private IEnumerator MoveToPosition(Vector3 targetPosition, SpeedType speedType)
            {
                float speed = GetSpeed(speedType);

                while (true)
                {
                    // Calculate the distance to the target position
                    float distance = Vector3.Distance(_npc.transform.position, targetPosition);

                    // If close enough, snap to the target position
                    if (distance <= 0.1f)
                    {
                        SnapToPosition(targetPosition);
                        yield break; // Exit the coroutine immediately
                    }

                    // Move towards the target position
                    Move(speed, targetPosition);

                    yield return null; // Wait for the next frame
                }
            }


            private void Move(float baseSpeed, Vector3 targetPosition)
            {
                Vector3 currentPosition = _npc.transform.position;
                Vector3 moveDirection = (targetPosition - currentPosition).normalized;

                Vector3 targetMove = moveDirection * baseSpeed * Time.deltaTime;
                Vector3 newPosition = currentPosition + targetMove;

                // Adjust for ground height
                if (Physics.Raycast(newPosition + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, layers)) // Increase raycast length
                {
                    newPosition.y = hit.point.y + 0.1f; // Adjust height to ground level
                }

                _npc.transform.position = newPosition;
                _npc.ServerPosition = newPosition;
                _npc.SendNetworkUpdateImmediate();
            }

            private void SnapToPosition(Vector3 position)
            {
                _npc.transform.position = position;
                _npc.ServerPosition = position;
                _npc.SendNetworkUpdateImmediate();
            }


            // ADD Slope calculation (Going uphill should be slower)
            // ADD Angle calculation (You can sprint while holding W + D, so make NPC walk)
         }


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

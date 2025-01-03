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

        private static Dictionary<BasePlayer, DebugManager> _activeDebugManagers = new Dictionary<BasePlayer, DebugManager>();
        private static List<NPCPlayer> _activeNPCs = new List<NPCPlayer>();

        #endregion

        #region Initialisation / Uninitialisation

        private void OnServerInitialized()
        {
            //cmd.AddChatCommand(config.generalSettings.mainCommand, this, nameof(MainCommand));

            _instance = this;
        }

        private void Unload() 
        {
            foreach (var npc in _activeNPCs) 
            {
                if (npc == null)
                    continue;

                npc.AdminKill();
            }

            foreach (var debugManager in _activeDebugManagers.Values) 
            {
                if (debugManager == null)
                    continue;

                UnityEngine.Object.DestroyImmediate(debugManager);
            }
        }

        #endregion

        #region Helpers



        #endregion

        #region Commands

        [ChatCommand("snpc")]
        private void MainCommand(BasePlayer player, string command, params string[ ] args)
        {
            CreateNPC(player.transform.position);
            CreateDebugManager(player);
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

        private DebugManager CreateDebugManager(BasePlayer player) 
        {
            var debugManager = player.gameObject.AddComponent<DebugManager>();
            debugManager.InitDebugManager(player);

            return debugManager;
        }

        private DebugManager? RetrieveDebugManager(BasePlayer player) 
        {
            var debugManager = player.gameObject.GetComponent<DebugManager>();
           
            if (debugManager == null)
            {
                PrintError("Debug Manager is null?");
                return null;
            }

            return debugManager;
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

                _activeNPCs.Add(_npc);
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

                MoveAction(_npc.transform.position + new Vector3(0f, 0f, 10f), SpeedType.Sprint);
                _instance.timer.Once(1.5f,() => {
                    MoveAction(_npc.transform.position + new Vector3(-5f, 0f, -7f), SpeedType.Walk);
                    
                });


                InvokeRepeating(nameof(MovementDebug), 0.3f, 0.1f);
            }

            private void MovementDebug() 
            {
                foreach(var debugManager in _activeDebugManagers.Values) 
                {
                    debugManager.DebugSphere(_npc.transform.position, 0.2f, "#00008B", 0.1f);
                    debugManager.DebugSphere(_npc.transform.position + new Vector3(1f, 0f, 1f), 0.2f, "#00008B", 0.1f);
                    debugManager.DebugSphere(_npc.transform.position + new Vector3(-1f, 0f, 1f), 0.2f, "#00008B", 0.1f);
                    debugManager.DebugArrow(_npc.transform.position, _npc.transform.position + new Vector3(0f, 0f, 1f), "#00008B", 0.1f, 0.1f);
                    debugManager.DebugText(_npc.transform.position + new Vector3(0f, 1f, 0f) + Vector3.up, "1.0", "#00ff6a", 0.1f);
                }

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
                var tempTargetPos = targetPosition;

                // Why is Y equal to 0 on both vectors? Well we only calculate both X and Z for both vectors because what if a y value (for either vector) is like 50 meters up in the sky  
                // the conditional is never satisfied. Additionally if we calculate for 3D distance when the NPC gets to its desired location (for the X and Z) it will
                // just slow down. 
                tempTargetPos.y = 0f;
                while (Vector3.Distance(new Vector3(_npc.transform.position.x, 0f, _npc.transform.position.z), tempTargetPos) > 0.2f)
                {
                    Move(speed, targetPosition);
                    yield return null;
                }
            }

            private void Move(float baseSpeed, Vector3 targetPosition)
            {
                Vector3 currentPosition = _npc.transform.position;
                Vector3 moveDirection = (targetPosition - currentPosition).normalized;

                Vector3 targetMove = moveDirection * baseSpeed * Time.deltaTime;
                Vector3 newPosition = currentPosition + targetMove;

                // Adjust for ground height
                if (Physics.Raycast(newPosition + Vector3.up, Vector3.down, out RaycastHit hit, 10f, layers)) 
                    newPosition.y = hit.point.y + 0.1f;

                _npc.transform.position = newPosition;
                _npc.ServerPosition = newPosition;
                _npc.SendNetworkUpdateImmediate();
            }


            // ADD Slope calculation (Going uphill should be slower)
            // ADD Angle calculation (You can sprint while holding W + D, so make NPC walk)
         }


        #endregion

        #region NPC Sensors



        #endregion

        #region DebugManager 

        private class DebugManager : MonoBehaviour 
        {
            BasePlayer _player;

            public void InitDebugManager(BasePlayer player)
            {
                _player = player;
                
                if(!_activeDebugManagers.ContainsKey(player))
                    _activeDebugManagers.Add(_player, this);
            }
            

            public void DebugLine(Vector3 startPosition, Vector3 endPosition, string colour, float aliveTime = 10f) => _player.SendConsoleCommand("ddraw.line", aliveTime, HexToColour(colour), startPosition, endPosition);
            public void DebugArrow(Vector3 startPosition, Vector3 endPosition, string colour, float aliveTime = 10f, float arrowHeadSize = 2f) => _player.SendConsoleCommand("ddraw.arrow", aliveTime, HexToColour(colour), startPosition, endPosition, arrowHeadSize);
            public void DebugSphere(Vector3 origin, float radius, string colour, float aliveTime = 10f) => _player.SendConsoleCommand("ddraw.sphere", aliveTime, HexToColour(colour), origin, radius);
            public void DebugText(Vector3 origin, string text, string colour, float aliveTime = 10f) => _player.SendConsoleCommand("ddraw.text", aliveTime, HexToColour(colour), origin, text);
            public void DebugBox(Vector3 origin, float size, string colour, float aliveTime = 10f) => _player.SendConsoleCommand("ddraw.box", aliveTime, HexToColour(colour), origin, size);


            private Color HexToColour(string colour) 
            {
                ColorUtility.TryParseHtmlString(colour, out Color parsedColour);
                return parsedColour;
            }
        }

        #endregion


        #region Localisation



        #endregion

        #region Configuration

        private ConfigData config;

        private class ConfigData
        {
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
                generalSettings = new GeneralSettings()
                {
                    mainCommand = "snpc",
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

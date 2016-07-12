using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Hammer Time", "Shady", "1.0.7", ResourceId = 1711)]
    [Description("Tweak settings for building blocks like demolish time, and rotate time.")]
    class HammerTime : RustPlugin
    {
        #region Config/Init
        float DemolishTime => GetConfig("DemolishTime", 600f);
        float RotateTime => GetConfig("RotateTime", 600f);
        float RepairCooldown => GetConfig("RepairDamageCooldown", 8f);

        bool DemolishAfterRestart => GetConfig("AllowDemolishAfterServerRestart", false);
        bool RotateAfterServerRestart => GetConfig("AllowRotateAfterServerRestart", false);
        bool MustOwnDemolish => GetConfig("MustOwnToDemolish", false);
        bool MustOwnRotate => GetConfig("MustOwnToRotate", false);
        bool AuthLevelOverride => GetConfig("AuthLevelOverrideDemolish", true);

        
        /*--------------------------------------------------------------//
		//			Load up the default config on first use				//
		//--------------------------------------------------------------*/
        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config["DemolishTime"] = DemolishTime;
            Config["RotateTime"] = RotateTime;
            Config["MustOwnToDemolish"] = MustOwnDemolish;
            Config["MustOwnToRotate"] = MustOwnRotate;
            Config["AllowDemolishAfterServerRestart"] = DemolishAfterRestart;
            Config["AllowRotateAfterServerRestart"] = RotateAfterServerRestart;
            Config["AuthLevelOverrideDemolish"] = AuthLevelOverride;
            Config["RepairDamageCooldown"] = RepairCooldown;
            SaveConfig();
        }

        private void Init() => LoadDefaultMessages();

        void OnServerInitialized()
        {
            if (!DemolishAfterRestart && !RotateAfterServerRestart) return;
            var doRotate = RotateAfterServerRestart;
            var blocks = GameObject.FindObjectsOfType<BuildingBlock>();
            for(int i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i];
                var name = block?.LookupShortPrefabName() ?? string.Empty;
                if (string.IsNullOrEmpty(name)) continue;
                var grade = block?.grade.ToString() ?? string.Empty;
                if (grade.ToLower().Contains("twig")) continue; //ignore twigs (performance)
                if (block.Health() <= block.MaxHealth() / 2.75f) continue; //ignore blocks that are weak (performance)

                if (name.Contains("foundation") || name.Contains("pillar") || name.Contains("roof") || name.Contains("floor")) doRotate = false;
                DoInvokes(block, DemolishAfterRestart, doRotate, false);
            }
        }

        /*--------------------------------------------------------------//
        //			Localization Stuff			                        //
        //--------------------------------------------------------------*/

        private void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                //DO NOT EDIT LANGUAGE FILES HERE! Navigate to oxide\lang\HammerTime.en.json
                {"doesNotOwnDemo", "You can only demolish objects you own!"},
                {"doesNotOwnRotate", "You can only rotate objects you own!" }
            };
            lang.RegisterMessages(messages, this);
        }
        #endregion;
        #region InvokeBlocks
        void DoInvokes(BuildingBlock block, bool demo, bool rotate, bool justCreated)
        {
            if (demo)
            {
                if (DemolishTime < 0)
                {
                    block.CancelInvoke("StopBeingDemolishable");
                    block.SetFlag(BaseEntity.Flags.Reserved2, true); //reserved2 is demolishable
                    block.SendNetworkUpdateImmediate(justCreated);
                }
                if (DemolishTime == 0) block.Invoke("StopBeingDemolishable", 0.01f);
                if (DemolishTime >= 1 && DemolishTime != 600) //if time is = to 600, then it's default, and there's no point in changing anything
                {
                    block.CancelInvoke("StopBeingDemolishable");
                    block.SetFlag(BaseEntity.Flags.Reserved2, true); //reserved2 is demolishable
                    block.Invoke("StopBeingDemolishable", DemolishTime);
                    block.SendNetworkUpdateImmediate(justCreated);
                }
            }
            if (rotate)
            {
                if (RotateTime < 0)
                {
                    block.CancelInvoke("StopBeingRotatable");
                    block.SetFlag(BaseEntity.Flags.Reserved1, true); //reserved1 is rotatable
                    block.SendNetworkUpdateImmediate(justCreated);
                }
                    if (RotateTime == 0) block.Invoke("StopBeingRotatable", 0.01f);
                if (RotateTime >= 1 && RotateTime != 600) //if time is = to 600, then it's default, and there's no point in changing anything
                {
                    block.CancelInvoke("StopBeingRotatable");
                    block.SetFlag(BaseEntity.Flags.Reserved1, true); //reserved1 is rotatable
                    block.Invoke("StopBeingRotatable", RotateTime);
                    block.SendNetworkUpdateImmediate(justCreated);
                }
            }
        }
        #endregion
        #region Hooks
      
        private void OnEntityBuilt(Planner plan, GameObject objectBlock)
        {
            var GetTypeString = objectBlock?.ToBaseEntity()?.GetType()?.ToString();
            var isBuildingBlock = GetTypeString == "BuildingBlock";
            if (!isBuildingBlock) return;
            var block = objectBlock?.ToBaseEntity()?.GetComponent<BuildingBlock>() ?? null;
            if (block == null) return;
            var name = block?.LookupShortPrefabName() ?? string.Empty;
            if (string.IsNullOrEmpty(name)) return;
            var doRotate = true;
            if (name.Contains("foundation") || name.Contains("pillar") || name.Contains("floor") || name.Contains("roof")) doRotate = false;
            DoInvokes(block, true, doRotate, true);
        }

        private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (block == null) return;
            var name = block?.LookupShortPrefabName() ?? string.Empty;
            if (string.IsNullOrEmpty(name)) return;
            var doRotate = true;
            if (name.Contains("foundation") || name.Contains("pillar") || name.Contains("floor") || name.Contains("roof")) doRotate = false;   
            DoInvokes(block, false, doRotate, false);
        }

       object OnStructureRepair(BaseCombatEntity block, BasePlayer player)
        {
            if (block == null || player == null) return null;
            var cooldown = RepairCooldown;
            if (cooldown < 1f) cooldown = 0f;
            if (cooldown == 8f) return null;
            if (block.TimeSinceAttacked() < cooldown) return false;
            return null;
        }

        object OnHammerHit(BasePlayer player, HitInfo hitInfo)
        {
            var entity = hitInfo?.HitEntity?.GetComponent<BaseCombatEntity>() ?? null;
            if (entity == null) return null;
            var cooldown = RepairCooldown;
            if (cooldown < 1f) cooldown = 0f;
            if (cooldown == 8f) return null;
            if (entity.TimeSinceAttacked() < cooldown) return false;
            return null;
        }

        object OnStructureDemolish(BuildingBlock block, BasePlayer player)
        {
            if (!(bool)Config["MustOwnToDemolish"]) return null;
            if ((bool)Config["AuthLevelOverrideDemolish"] && player.IsAdmin()) return null;
            if (permission.UserHasPermission(player.userID.ToString(), "hammertime.allowdemo")) return null;
            if (block.OwnerID == 0 || player.userID == 0) return null;
            if (block.OwnerID != player.userID)
            {
                SendReply(player, GetMessage("doesNotOwnDemo"));
                return true;
            }
            return null;
        }

        object OnStructureRotate(BuildingBlock block, BasePlayer player)
        {
            if (!(bool)Config["MustOwnToRotate"]) return null;
            if (block.OwnerID == 0 || player.userID == 0) return null;
            if (block.OwnerID != player.userID)
            {
                SendReply(player, GetMessage("doesNotOwnRotate"));
                return true;
            }
                
            return null;
        }
        #endregion
        #region Util
        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        private string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
        #endregion
    }
}
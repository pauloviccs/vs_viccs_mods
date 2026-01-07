using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace MasteryTitles
{
    public class PartyBuffSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            // Check every 5 seconds
            api.Event.RegisterGameTickListener(CheckPartyBuffs, 5000);
        }

        private void CheckPartyBuffs(float dt)
        {
            if (sapi.World.AllOnlinePlayers.Length < 2) return;

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                ApplySynergyIfApplicable(player as IServerPlayer);
            }
        }

        private void ApplySynergyIfApplicable(IServerPlayer player)
        {
            var masterySys = sapi.ModLoader.GetModSystem<MasterySystem>();
            if (masterySys == null || !masterySys.masteryCache.TryGetValue(player.PlayerUID, out var myData)) return;

            // Find nearby players
            foreach (var other in sapi.World.AllOnlinePlayers)
            {
                if (other.PlayerUID == player.PlayerUID) continue;
                if (player.Entity.Pos.DistanceTo(other.Entity.Pos) > 10) continue;

                if (masterySys.masteryCache.TryGetValue(other.PlayerUID, out var otherData))
                {
                    // Check if different main profession
                    var myMain = GetMainMastery(myData);
                    var otherMain = GetMainMastery(otherData);

                    if (myMain != otherMain)
                    {
                        // Apply Synergy Buff!
                        // Example: "Synergy" stat
                        player.Entity.Stats.Set("walkingSpeed", "party", 0.1f, true); // +10% speed
                        // Could add more complex logic based on pair (Miner+Warrior = Dano)
                        // But sticking to general "Party Synergy" is safer and effect is clear.
                    }
                }
            }
        }

        private MasteryType GetMainMastery(PlayerMasteryData data)
        {
            if (data.Experience.Count == 0) return MasteryType.Mining;
            return data.Experience.OrderByDescending(x => x.Value).First().Key;
        }
    }
}

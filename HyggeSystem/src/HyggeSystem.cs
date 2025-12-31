using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config; // <--- Adicionado para corrigir GlobalConstants

namespace HyggeMod
{
    public class HyggeSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        
        private Dictionary<string, int> cozyCounter = new Dictionary<string, int>();
        private SimpleParticleProperties heartParticles;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            
            // Configuração visual das partículas
            heartParticles = new SimpleParticleProperties(
                1.0f, 1.0f, 
                ColorUtil.ToRgba(255, 255, 105, 180), 
                new Vec3d(), new Vec3d(), 
                new Vec3f(-0.1f, 0.1f, -0.1f), new Vec3f(0.1f, 0.5f, 0.1f), 
                1.5f, 
                0f, 
                0.5f, 0.8f, 
                EnumParticleModel.Quad
            );
            // REMOVIDO: heartParticles.ParticleGeometry (Causava erro e não é necessário para Quad)

            api.Event.RegisterGameTickListener(OnHyggeTick, 1000);
            api.Event.PlayerDisconnect += (player) => cozyCounter.Remove(player.PlayerUID);
        }

        private void OnHyggeTick(float dt)
        {
            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                bool isCozy = ProcessPlayer(player);

                if (isCozy)
                {
                    if (!cozyCounter.ContainsKey(player.PlayerUID)) cozyCounter[player.PlayerUID] = 0;
                    cozyCounter[player.PlayerUID]++;

                    int seconds = cozyCounter[player.PlayerUID];

                    if (seconds >= 10)
                    {
                        ApplyHyggeEffects(player, seconds);
                    }
                }
                else
                {
                    if (cozyCounter.ContainsKey(player.PlayerUID) && cozyCounter[player.PlayerUID] > 0)
                    {
                        RemoveImmediateEffects(player);
                        cozyCounter[player.PlayerUID] = 0;
                    }
                }
            }
        }

        private bool ProcessPlayer(IServerPlayer player)
        {
            // Verifica se está sentado (Chão ou Mobília)
            bool isSittingOnFloor = player.Entity.Controls.FloorSitting;
            bool isSittingOnFurniture = player.Entity.MountedOn != null;

            if (!isSittingOnFloor && !isSittingOnFurniture) return false;

            BlockPos pPos = player.Entity.Pos.AsBlockPos;
            int radius = 3;

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -1; y <= 1; y++) 
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos checkPos = pPos.AddCopy(x, y, z);
                        
                        // --- MUDANÇA DE SEGURANÇA ---
                        // Em vez de tentar carregar a classe BlockEntityFirepit (que deu erro),
                        // pegamos o BLOCO em si e checamos o código dele.
                        // Fogueiras acesas no VS contém "lit" no nome. Ex: "firepit-construct-lit"
                        Block block = sapi.World.BlockAccessor.GetBlock(checkPos);
                        
                        if (block.Code.Path.Contains("firepit") && block.Code.Path.Contains("lit"))
                        {
                            return true; // É uma fogueira e está acesa
                        }
                    }
                }
            }

            return false;
        }

        private void ApplyHyggeEffects(IServerPlayer player, int secondsActive)
        {
            EntityPlayer entity = player.Entity;

            heartParticles.MinPos = entity.Pos.XYZ.Add(0, entity.LocalEyePos.Y + 0.5, 0);
            sapi.World.SpawnParticles(heartParticles, player);

            entity.Stats.Set("hungerrate", "hygge-rest", 0f, true);

            if (secondsActive % 5 == 0)
            {
                entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, 1f);
            }

            entity.Stats.Set("maxhealthExtraPoints", "hygge-buff", 5f, true);
            entity.Stats.Set("miningSpeedMultiplier", "hygge-buff", 0.1f, true);
        }

        private void RemoveImmediateEffects(IServerPlayer player)
        {
            player.Entity.Stats.Remove("hungerrate", "hygge-rest");
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Voce se sente revigorado pelo descanso!", EnumChatType.Notification);
        }
    }
}
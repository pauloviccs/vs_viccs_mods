using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;

namespace LazyMagnet
{
    public class MagnetSystem : ModSystem
    {
        private ICoreServerAPI? sapi;
        
        // Lista de jogadores ativos
        private HashSet<string> activePlayers = new HashSet<string>();

        // Configurações
        private const double MagnetRange = 5.5; 
        private const float PullSpeed = 0.2f;   

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            api.ChatCommands.Create("magnet")
                .WithDescription("Ativa/Desativa o imã de itens")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(OnMagnetToggle);

            api.Event.RegisterGameTickListener(OnMagnetTick, 250); // 4x por segundo
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
        }

        private TextCommandResult OnMagnetToggle(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            string uid = player.PlayerUID;

            if (activePlayers.Contains(uid))
            {
                activePlayers.Remove(uid);
                return TextCommandResult.Success("Imã DESLIGADO.");
            }
            else
            {
                activePlayers.Add(uid);
                return TextCommandResult.Success("Imã LIGADO! (Raio: 5.5m)");
            }
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            if (activePlayers.Contains(player.PlayerUID))
            {
                activePlayers.Remove(player.PlayerUID);
            }
        }

        private void OnMagnetTick(float dt)
        {
            if (sapi == null || activePlayers.Count == 0) return;

            foreach (var uid in activePlayers)
            {
                IServerPlayer? player = sapi.World.PlayerByUid(uid) as IServerPlayer;
                
                if (player == null || player.Entity == null || 
                    player.WorldData.CurrentGameMode == EnumGameMode.Spectator) continue;

                EntityPos playerPos = player.Entity.Pos;
                
                sapi.World.GetEntitiesAround(player.Entity.Pos.XYZ, (float)MagnetRange, (float)MagnetRange, (entity) =>
                {
                    // FILTRO CORRIGIDO
                    if (entity is EntityItem itemEntity)
                    {
                        // Regra de Ouro: Só puxa se estiver vivo E no chão.
                        // Isso impede puxar itens que estão voando (recém jogados).
                        if (entity.Alive && entity.OnGround)
                        {
                            PullItem(itemEntity, playerPos.XYZ);
                        }
                    }
                    return true;
                });
            }
        }

        private void PullItem(EntityItem item, Vec3d targetPos)
        {
            var itemPos = item.ServerPos.XYZ;
            
            // Vetor em direção ao jogador
            var vector = targetPos.SubCopy(itemPos).Normalize();
            
            // Aplica movimento
            item.ServerPos.Motion.X += vector.X * PullSpeed;
            item.ServerPos.Motion.Y += vector.Y * PullSpeed + 0.1; // Pulinho pra subir degrau
            item.ServerPos.Motion.Z += vector.Z * PullSpeed;
        }
    }
}
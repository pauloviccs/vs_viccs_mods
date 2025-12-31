// using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace SimpleWaypoints
{
    public class WaypointModSystem : ModSystem
    {
        private ICoreServerAPI? sapi;
        private const int MaxWaypoints = 10;
        
        // Custo de fome removido. Agora é Free-to-Play.

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            var parsers = api.ChatCommands.Parsers;
            
            // 1. SETPOINT
            api.ChatCommands.Create("setpoint")
                .WithDescription("Salva sua posição atual")
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(parsers.Word("nome"))
                .HandleWith(OnSetPoint);

            // 2. GOPOINT (Agora Grátis)
            api.ChatCommands.Create("gopoint")
                .WithDescription("Teleporta instantaneamente para um ponto salvo")
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(parsers.Word("nome"))
                .HandleWith(OnGoPoint);

            // 3. LISTPOINTS
            api.ChatCommands.Create("listpoints")
                .WithDescription("Lista seus pontos")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(OnListPoints);

            // 4. REMOVEPOINT
            api.ChatCommands.Create("removepoint")
                .WithDescription("Remove um ponto")
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(parsers.Word("nome"))
                .HandleWith(OnRemovePoint);
        }

        // --- Lógica dos Comandos ---

        private TextCommandResult OnSetPoint(TextCommandCallingArgs args)
        {
            if (sapi == null) return TextCommandResult.Error("API Server off.");
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Jogador inválido.");

            string wpName = ((string)args[0]).ToLower();
            var data = LoadWaypoints(player.PlayerUID);

            if (data.Waypoints.Count >= MaxWaypoints)
                return TextCommandResult.Error($"Limite de {MaxWaypoints} pontos atingido.");

            if (data.Waypoints.Any(w => w.Name == wpName))
                return TextCommandResult.Error($"O ponto '{wpName}' já existe.");

            // Salva
            data.Waypoints.Add(new WaypointEntry { Name = wpName, Position = player.Entity.Pos.XYZ });
            SaveWaypoints(player.PlayerUID, data);

            return TextCommandResult.Success($"Ponto '{wpName}' salvo!");
        }

        private TextCommandResult OnGoPoint(TextCommandCallingArgs args)
        {
            if (sapi == null) return TextCommandResult.Error("API Server off.");
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Jogador inválido.");

            string wpName = ((string)args[0]).ToLower();
            var data = LoadWaypoints(player.PlayerUID);
            var wp = data.Waypoints.FirstOrDefault(w => w.Name == wpName);

            if (wp == null) return TextCommandResult.Error($"Ponto '{wpName}' não existe.");

            // --- Lógica de Teleporte Simples (Sem Fome) ---
            player.Entity.TeleportTo(wp.Position);
            
            return TextCommandResult.Success($"Teletransportado para '{wpName}'.");
        }

        private TextCommandResult OnRemovePoint(TextCommandCallingArgs args)
        {
            if (sapi == null) return TextCommandResult.Error("API Server off.");
            var player = args.Caller.Player as IServerPlayer;
            string wpName = ((string)args[0]).ToLower();

            var data = LoadWaypoints(player.PlayerUID);
            var wp = data.Waypoints.FirstOrDefault(w => w.Name == wpName);

            if (wp == null) return TextCommandResult.Error($"Ponto '{wpName}' não existe.");

            data.Waypoints.Remove(wp);
            SaveWaypoints(player.PlayerUID, data);
            return TextCommandResult.Success($"Ponto '{wpName}' removido.");
        }

        private TextCommandResult OnListPoints(TextCommandCallingArgs args)
        {
            if (sapi == null) return TextCommandResult.Error("API Server off.");
            var player = args.Caller.Player as IServerPlayer;
            var data = LoadWaypoints(player.PlayerUID);

            if (data.Waypoints.Count == 0) return TextCommandResult.Success("Lista vazia.");
            
            string names = string.Join(", ", data.Waypoints.Select(w => w.Name));
            return TextCommandResult.Success($"Seus Pontos ({data.Waypoints.Count}/{MaxWaypoints}): {names}");
        }

        // --- Persistência ---
        private PlayerWaypointData LoadWaypoints(string playerUid)
        {
            if (sapi == null) return new PlayerWaypointData();
            byte[] data = sapi.WorldManager.SaveGame.GetData("simplewaypoints_" + playerUid);
            if (data == null) return new PlayerWaypointData();
            try { return SerializerUtil.Deserialize<PlayerWaypointData>(data); }
            catch { return new PlayerWaypointData(); }
        }

        private void SaveWaypoints(string playerUid, PlayerWaypointData data)
        {
            if (sapi == null) return;
            sapi.WorldManager.SaveGame.StoreData("simplewaypoints_" + playerUid, SerializerUtil.Serialize(data));
        }
    }
}
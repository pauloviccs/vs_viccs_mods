using System;
using System.Collections.Generic;
using System.Linq; // Necessário para usar FirstOrDefault
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace SimpleTPA
{
    public class TeleportRequest
    {
        public string RequesterUID;
        public long RequestTimeMs; 
    }

    public class TpaSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        private Dictionary<string, TeleportRequest> pendingRequests = new Dictionary<string, TeleportRequest>();
        private const long EXPIRE_TIME_MS = 10000; 

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            // CORREÇÃO: Usamos Parsers.Word para pegar o nome como String
            api.ChatCommands.Create("tpa")
                .WithDescription("Solicita teleporte até outro jogador")
                .WithArgs(api.ChatCommands.Parsers.Word("targetName")) 
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(OnTpaCommand);

            api.ChatCommands.Create("tpaccept")
                .WithDescription("Aceita uma solicitação de teleporte")
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("requesterName")) 
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(OnTpAcceptCommand);
        }

        private TextCommandResult OnTpaCommand(TextCommandCallingArgs args)
        {
            IServerPlayer fromPlayer = args.Caller.Player as IServerPlayer;
            string targetName = args[0] as string;

            if (string.IsNullOrEmpty(targetName))
            {
                return TextCommandResult.Error("Você deve especificar o nome do jogador.");
            }

            // CORREÇÃO: Buscamos o jogador manualmente na lista de online
            IServerPlayer targetPlayer = sapi.World.AllOnlinePlayers
                .FirstOrDefault(p => p.PlayerName.Equals(targetName, StringComparison.InvariantCultureIgnoreCase)) as IServerPlayer;

            // Validações
            if (targetPlayer == null) return TextCommandResult.Error($"Jogador '{targetName}' não encontrado ou offline.");
            if (fromPlayer.PlayerUID == targetPlayer.PlayerUID) return TextCommandResult.Error("Você não pode se teleportar para si mesmo.");

            // Cria o pedido
            TeleportRequest newRequest = new TeleportRequest()
            {
                RequesterUID = fromPlayer.PlayerUID,
                RequestTimeMs = sapi.World.ElapsedMilliseconds
            };

            if (pendingRequests.ContainsKey(targetPlayer.PlayerUID))
            {
                pendingRequests[targetPlayer.PlayerUID] = newRequest;
            }
            else
            {
                pendingRequests.Add(targetPlayer.PlayerUID, newRequest);
            }

            // Gera o botão
            string commandToRun = $"/tpaccept {fromPlayer.PlayerName}";
            
            string message = string.Format(
                "<strong>{0}</strong> deseja se teleportar até você (Expira em 10s).\n<a href=\"cmd:{1}\"><strong>[CLIQUE PARA ACEITAR]</strong></a>",
                fromPlayer.PlayerName,
                commandToRun
            );

            sapi.SendMessage(targetPlayer, GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);

            return TextCommandResult.Success($"Solicitação enviada para {targetPlayer.PlayerName}.");
        }

        private TextCommandResult OnTpAcceptCommand(TextCommandCallingArgs args)
        {
            IServerPlayer hostPlayer = args.Caller.Player as IServerPlayer;
            string argName = args[0] as string; 

            if (!pendingRequests.TryGetValue(hostPlayer.PlayerUID, out TeleportRequest requestData))
            {
                return TextCommandResult.Error("Nenhuma solicitação de teleporte encontrada.");
            }

            long currentTime = sapi.World.ElapsedMilliseconds;
            if ((currentTime - requestData.RequestTimeMs) > EXPIRE_TIME_MS)
            {
                pendingRequests.Remove(hostPlayer.PlayerUID);
                return TextCommandResult.Error("Esta solicitação de teleporte já expirou.");
            }

            IServerPlayer requesterPlayer = sapi.World.PlayerByUid(requestData.RequesterUID) as IServerPlayer;

            if (requesterPlayer == null || requesterPlayer.ConnectionState != EnumClientState.Playing)
            {
                pendingRequests.Remove(hostPlayer.PlayerUID);
                return TextCommandResult.Error("O jogador que pediu o teleporte saiu do servidor.");
            }

            if (!string.IsNullOrEmpty(argName) && !requesterPlayer.PlayerName.Equals(argName, StringComparison.InvariantCultureIgnoreCase))
            {
                return TextCommandResult.Error($"O pedido válido é de {requesterPlayer.PlayerName}, mas o botão clicado era de {argName}.");
            }

            // Executa teleporte
            requesterPlayer.Entity.TeleportTo(hostPlayer.Entity.Pos);
            
            pendingRequests.Remove(hostPlayer.PlayerUID);
            requesterPlayer.SendMessage(GlobalConstants.GeneralChatGroup, $"Teleporte aceito por {hostPlayer.PlayerName}!", EnumChatType.Notification);

            return TextCommandResult.Success($"Você aceitou o teleporte de {requesterPlayer.PlayerName}.");
        }
    }
}
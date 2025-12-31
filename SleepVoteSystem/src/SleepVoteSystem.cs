using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config; // <--- Faltava esta linha!

namespace SleepVote
{
    public class SleepVoteSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        
        // Armazena UIDs de quem votou "Sim"
        private HashSet<string> yesVotes = new HashSet<string>();
        
        // Configurações
        private double votePercentageRequired = 0.50; // 50%
        private float checkInterval = 2.0f; // Checar camas a cada 2 segundos
        private float timePassed = 0f;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            // Registrar Comandos
            api.ChatCommands.Create("voteday")
                .WithDescription("Vota para pular a noite")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(OnVoteDayCommand);

            // Listener para detectar jogadores em camas
            api.Event.RegisterGameTickListener(OnTick, 1000); 
        }

        private void OnTick(float dt)
        {
            if (IsDaytime())
            {
                if (yesVotes.Count > 0) yesVotes.Clear();
                return;
            }

            timePassed += dt;
            if (timePassed < checkInterval) return;
            timePassed = 0;

            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity.MountedOn != null && !yesVotes.Contains(player.PlayerUID))
                {
                    RegistrarVoto(player, autoVote: true);
                }
            }
        }

        private TextCommandResult OnVoteDayCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            
            if (IsDaytime())
            {
                return TextCommandResult.Error("Já é dia! Não é possível votar agora.");
            }

            if (yesVotes.Contains(player.PlayerUID))
            {
                return TextCommandResult.Error("Você já votou.");
            }

            RegistrarVoto(player, autoVote: false);
            return TextCommandResult.Success(""); 
        }

        private void RegistrarVoto(IServerPlayer player, bool autoVote)
        {
            yesVotes.Add(player.PlayerUID);

            int onlinePlayers = sapi.World.AllOnlinePlayers.Length;
            int votesNeeded = (int)Math.Ceiling(onlinePlayers * votePercentageRequired);
            int currentVotes = yesVotes.Count;

            string action = autoVote ? "foi dormir" : "votou para passar a noite";
            string msg = string.Format("<strong>{0}</strong> {1}. ({2}/{3} votos necessários)", 
                player.PlayerName, action, currentVotes, votesNeeded);

            sapi.SendMessageToGroup(GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);

            VerificarResultado(onlinePlayers, votesNeeded);
        }

        private void VerificarResultado(int totalPlayers, int votesNeeded)
        {
            if (yesVotes.Count >= votesNeeded)
            {
                PularNoite();
            }
        }

        private void PularNoite()
        {
            var calendar = sapi.World.Calendar;
            float currentHour = calendar.HourOfDay;
            float targetHour = 6.0f; 

            float hoursToAdvance;
            if (currentHour > targetHour)
            {
                hoursToAdvance = (24f - currentHour) + targetHour;
            }
            else
            {
                hoursToAdvance = targetHour - currentHour;
            }

            calendar.Add(hoursToAdvance);

            sapi.SendMessageToGroup(GlobalConstants.GeneralChatGroup, "A democracia venceu! O sol nasceu.", EnumChatType.Notification);
            
            yesVotes.Clear();
        }

        private bool IsDaytime()
        {
            float hour = sapi.World.Calendar.HourOfDay;
            return hour >= 6.0f && hour < 18.0f;
        }
    }
}
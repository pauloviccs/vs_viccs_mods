using System;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json; 
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace DiscordLink
{
    public class DiscordConfig
    {
        public string WebhookUrl = "COLE_SUA_URL_NOVA_AQUI"; 
        public string LastMessageId = ""; 
        public double UpdateIntervalSeconds = 10.0;
    }

    public class DiscordStatusSystem : ModSystem
    {
        private ICoreServerAPI sapi; 
        private DiscordConfig config;
        private HttpClient client;
        private long lastTick = 0;
        private bool isSending = false; 

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api; 
            this.client = new HttpClient();
            this.client.Timeout = TimeSpan.FromSeconds(10);

            sapi.Logger.Notification("[DiscordLink] Mod Inicializado.");

            try
            {
                config = sapi.LoadModConfig<DiscordConfig>("discordstatus.json");
                if (config == null)
                {
                    config = new DiscordConfig();
                    sapi.StoreModConfig(config, "discordstatus.json");
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[DiscordLink] Erro no Config: {ex.Message}");
                config = new DiscordConfig();
            }

            sapi.Event.RegisterGameTickListener(OnTick, 5000); 
        }

        private void OnTick(float dt)
        {
            if (config == null || config.WebhookUrl.Contains("COLE_SUA_URL")) return;
            if (isSending) return;

            long now = sapi.World.ElapsedMilliseconds;
            if (now - lastTick < (config.UpdateIntervalSeconds * 1000)) return;
            
            lastTick = now;
            isSending = true;

            // --- COLETA DE DADOS ---
            var calendar = sapi.World.Calendar;
            int currentMonthIndex = calendar.Month; // 0 a 11
            
            string season = GetSeasonFromMonth(currentMonthIndex);
            string seasonEmoji = GetSeasonEmoji(season);
            string seasonPt = TranslateSeason(season);

            // Data atual
            int dayOfMonth = (int)(calendar.DayOfYear % calendar.DaysPerMonth) + 1;

            // --- CORREÇÃO DO CÁLCULO DE DIAS RESTANTES ---
            // As estações mudam nos meses: 2 (Mar), 5 (Jun), 8 (Set), 11 (Dez).
            // A fórmula anterior assumia troca em 0, 3, 6...
            // Ajustamos adicionando +1 ao índice do mês para alinhar o ciclo.
            int monthsUntilNextSeason = 3 - ((currentMonthIndex + 1) % 3);
            
            float daysRemainingInCurrentMonth = calendar.DaysPerMonth - dayOfMonth;
            // O total é: Dias do mês atual + (Meses inteiros restantes * Dias por mês)
            float totalDaysLeft = daysRemainingInCurrentMonth + ((monthsUntilNextSeason - 1) * calendar.DaysPerMonth);
            
            string timeOfDay = $"{calendar.HourOfDay:00}:{((int)(calendar.HourOfDay * 60) % 60):00}";
            int onlinePlayers = sapi.World.AllOnlinePlayers.Length;

            // Ajuste visual: +1 no mês para ficar legível (1-12) em vez de (0-11)
            string content = 
                $"**STATUS DO SERVIDOR**\n" +
                $"----------------------------------\n" +
                $"{seasonEmoji} **Estação:** {seasonPt}\n" +
                $"📅 **Data:** Dia {dayOfMonth}, Mês {currentMonthIndex + 1}\n" +
                $"⏳ **Próxima Estação:** em {totalDaysLeft:0.0} dias\n" +
                $"🕒 **Horário no Jogo:** {timeOfDay}\n" +
                $"👥 **Jogadores Online:** {onlinePlayers}\n" +
                $"----------------------------------\n" +
                $"*Atualizado em: {DateTime.Now:HH:mm:ss}*";

            Task.Run(() => SendToDiscordAsync(content));
        }

        private async Task SendToDiscordAsync(string content)
        {
            try
            {
                var payload = new { content = content };
                var json = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                if (!string.IsNullOrEmpty(config.LastMessageId))
                {
                    string editUrl = $"{config.WebhookUrl}/messages/{config.LastMessageId}";
                    var response = await client.PatchAsync(editUrl, httpContent);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        sapi.Event.EnqueueMainThreadTask(() => {
                            config.LastMessageId = ""; 
                            sapi.StoreModConfig(config, "discordstatus.json");
                        }, "ResetID");
                    }
                }
                else
                {
                    string postUrl = config.WebhookUrl + "?wait=true"; 
                    var response = await client.PostAsync(postUrl, httpContent);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        using (JsonDocument doc = JsonDocument.Parse(responseBody))
                        {
                            if (doc.RootElement.TryGetProperty("id", out JsonElement idElement))
                            {
                                string newId = idElement.GetString();
                                sapi.Event.EnqueueMainThreadTask(() => {
                                    config.LastMessageId = newId;
                                    sapi.StoreModConfig(config, "discordstatus.json");
                                }, "SaveID");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sapi.Event.EnqueueMainThreadTask(() => {
                    sapi.Logger.Error($"[DiscordLink] Erro: {ex.Message}");
                }, "LogEx");
            }
            finally
            {
                isSending = false;
            }
        }

        // --- CORREÇÃO DO MAPEAMENTO DE ESTAÇÕES ---
        // Padrão Vintage Story (Hemisfério Norte):
        // Primavera: Março (2), Abril (3), Maio (4)
        // Verão: Junho (5), Julho (6), Agosto (7)
        // Outono: Setembro (8), Outubro (9), Novembro (10)
        // Inverno: Dezembro (11), Janeiro (0), Fevereiro (1)
        private string GetSeasonFromMonth(int month)
        {
            if (month >= 2 && month <= 4) return "spring";
            if (month >= 5 && month <= 7) return "summer";
            if (month >= 8 && month <= 10) return "autumn";
            return "winter"; // 11, 0, 1
        }

        private string GetSeasonEmoji(string season)
        {
            if (season == "spring") return "🌺";
            if (season == "summer") return "☀️";
            if (season == "autumn") return "🍂";
            if (season == "winter") return "❄️";
            return "🌍";
        }
        
        private string TranslateSeason(string season)
        {
            if (season == "spring") return "Primavera";
            if (season == "summer") return "Verão";
            if (season == "autumn") return "Outono";
            if (season == "winter") return "Inverno";
            return season;
        }
    }
}
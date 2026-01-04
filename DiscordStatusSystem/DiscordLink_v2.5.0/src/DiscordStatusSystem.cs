using System;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json; 
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent; // Necessário para SystemTemporalStability

namespace DiscordLink
{
    public class DiscordConfig
    {
        public string WebhookUrl = "COLE_SUA_URL_NOVA_AQUI"; 
        public string LastMessageId = ""; 
        public double UpdateIntervalSeconds = 10.0;
        public string Hemisphere = "North"; // "North" or "South"
    }

    public class TemporalStormInfo
    {
        private object riftWeatherSystem;
        private ICoreAPI api;

        public bool Initialize(ICoreAPI api)
        {
            this.api = api;
            try
            {
                riftWeatherSystem = api.ModLoader.GetModSystem("Vintagestory.GameContent.ModSystemRiftWeather");
                return riftWeatherSystem != null;
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[DiscordLink] Failed to init storm info: {ex}");
                return false;
            }
        }

        public bool IsStormActive()
        {
            if (riftWeatherSystem == null) return false;
            try
            {
                var type = riftWeatherSystem.GetType();
                var property = type.GetProperty("IsStormActive");
                return property != null && (bool)property.GetValue(riftWeatherSystem);
            }
            catch { return false; }
        }

        public double GetStormDaysLeft()
        {
            if (riftWeatherSystem == null) return 0.0;
            try
            {
                var type = riftWeatherSystem.GetType();
                var property = type.GetProperty("StormDaysLeft");
                return property != null ? (double)property.GetValue(riftWeatherSystem) : 0.0;
            }
            catch { return 0.0; }
        }
    }

    public class DiscordStatusSystem : ModSystem
    {
        private ICoreServerAPI sapi; 
        private DiscordConfig config;
        private HttpClient client;
        private long lastTick = 0;
        private bool isSending = false;
        private TemporalStormInfo stormInfo;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api; 
            this.client = new HttpClient();
            this.client.Timeout = TimeSpan.FromSeconds(10);
            
            // Initialize Storm Info
            this.stormInfo = new TemporalStormInfo();
            this.stormInfo.Initialize(api);

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

            // Registrar comando de debug
            sapi.ChatCommands.Create("discordstatus")
                .WithDescription("Discord Status Debug Tools")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("info")
                    .HandleWith(OnDebugInfo)
                .EndSubCommand();
        }

        private TextCommandResult OnDebugInfo(TextCommandCallingArgs args)
        {
            var calendar = sapi.World.Calendar;
            var spawnPos = sapi.World.DefaultSpawnPosition.AsBlockPos;
            var season = calendar.GetSeason(spawnPos);
            var climate = sapi.World.BlockAccessor.GetClimateAt(spawnPos, EnumGetClimateMode.NowValues);
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== Discord Status Debug ===");
            sb.AppendLine($"Time: {DateTime.Now}");
            sb.AppendLine($"Game Time: {calendar.HourOfDay:0.00}h");
            sb.AppendLine($"Season (API): {season}");
            sb.AppendLine($"Temp (Spawn): {climate.Temperature:F1}°C");
            sb.AppendLine($"Rainfall: {climate.Rainfall:F2}");
            sb.AppendLine($"Storm Active: {stormInfo?.IsStormActive()}");
            sb.AppendLine($"Online Players: {sapi.World.AllOnlinePlayers.Length}");
            
            return TextCommandResult.Success(sb.ToString());
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
            var spawnPos = sapi.World.DefaultSpawnPosition.AsBlockPos;
            
            // Estação e Clima via API
            var seasonEnum = calendar.GetSeason(spawnPos);
            string season = seasonEnum.ToString().ToLower();
            var climate = sapi.World.BlockAccessor.GetClimateAt(spawnPos, EnumGetClimateMode.NowValues);
            
            string seasonEmoji = GetSeasonEmoji(season);
            string seasonPt = TranslateSeason(season);
            string weatherEmoji = GetWeatherEmoji(climate.Rainfall, climate.Temperature);
            string weatherDesc = GetWeatherDescription(climate.Rainfall);

            int dayOfMonth = (int)(calendar.DayOfYear % calendar.DaysPerMonth) + 1;
            int currentMonthIndex = calendar.Month;
            
            // Calculo de tempo restante estimativo
            // Nota: Com o uso da API de estações, o conceito de "meses até próxima estação" é mais fluido,
            // mas mantemos o cálculo baseado em dias para dar uma estimativa visual.
            int monthsUntilNextSeason = 3 - ((currentMonthIndex + 1) % 3);
            float daysRemainingInCurrentMonth = calendar.DaysPerMonth - dayOfMonth;
            float totalDaysLeft = daysRemainingInCurrentMonth + ((monthsUntilNextSeason - 1) * calendar.DaysPerMonth);
            
            string timeOfDay = $"{calendar.HourOfDay:00}:{((int)(calendar.HourOfDay * 60) % 60):00}";
            int onlinePlayers = sapi.World.AllOnlinePlayers.Length;

            // --- TEMPESTADE TEMPORAL ---
            string stormInfo = "";
            bool isStorm = this.stormInfo?.IsStormActive() ?? false;
            
            if (isStorm)
            {
                double gameHoursLeft = this.stormInfo.GetStormDaysLeft() * calendar.HoursPerDay;
                if (gameHoursLeft > 0)
                {
                    stormInfo = $"\n⚡ **TEMPESTADE TEMPORAL ATIVA!**\n🌪️ Termina em: {gameHoursLeft:0.0} horas (jogo)\n";
                }
            }

            string content = 
                $"**STATUS DO SERVIDOR**\n" +
                $"----------------------------------\n" +
                $"{seasonEmoji} **Estação:** {seasonPt}\n" +
                $"{weatherEmoji} **Clima:** {climate.Temperature:F1}°C, {weatherDesc}\n" +
                $"📅 **Data:** Dia {dayOfMonth}, Mês {currentMonthIndex + 1}\n" +
                $"⏳ **Próxima Estação:** em {totalDaysLeft:0.0} dias\n" +
                $"🕒 **Horário no Jogo:** {timeOfDay}\n" +
                $"👥 **Jogadores Online:** {onlinePlayers}\n" +
                $"{stormInfo}" + 
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

        private string GetSeasonEmoji(string season)
        {
            if (season.Contains("spring")) return "🌺";
            if (season.Contains("summer")) return "☀️";
            if (season.Contains("autumn") || season.Contains("fall")) return "🍂";
            if (season.Contains("winter")) return "❄️";
            return "🌍";
        }
        
        private string TranslateSeason(string season)
        {
            if (season.Contains("spring")) return "Primavera";
            if (season.Contains("summer")) return "Verão";
            if (season.Contains("autumn") || season.Contains("fall")) return "Outono";
            if (season.Contains("winter")) return "Inverno";
            return season;
        }

        private string GetWeatherEmoji(float rainfall, float temp)
        {
            if (rainfall > 0.5f) return "🌧️";
            if (rainfall > 0.2f) return "☁️";
            if (temp > 30) return "🔥";
            if (temp < 0) return "🧊";
            return "🌤️";
        }

        private string GetWeatherDescription(float rainfall)
        {
            if (rainfall < 0.1f) return "Céu Limpo";
            if (rainfall < 0.3f) return "Parcialmente Nublado";
            if (rainfall < 0.6f) return "Nublado";
            if (rainfall < 0.8f) return "Chuvoso";
            return "Tempestade";
        }
    }
}
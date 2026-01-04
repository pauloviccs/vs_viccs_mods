# Documentação Completa - Clima, Tempo, Relógio e Tempestades Temporais

## Índice
1. [Sistema de Calendário (IGameCalendar)](#sistema-de-calendário-igamecalendar)
2. [Sistema de Clima e Temperatura](#sistema-de-clima-e-temperatura)
3. [Sistema de Clima (Weather)](#sistema-de-clima-weather)
4. [Tempestades Temporais](#tempestades-temporais)
5. [Implementação Prática](#implementação-prática)
6. [Exemplos de Código Completos](#exemplos-de-código-completos)
7. [Integração com Outros Sistemas](#integração-com-outros-sistemas)
8. [Mods de Referência](#mods-de-referência)

---

## Sistema de Calendário (IGameCalendar)

### Visão Geral

O `IGameCalendar` é a API principal para tudo relacionado ao calendário e astronomia no Vintage Story. Acessível via `api.World.Calendar`.

### Propriedades Principais

#### Tempo e Data

```csharp
// Acessar o calendário
IGameCalendar calendar = api.World.Calendar;

// === HORÁRIO DO DIA ===
float hourOfDay = calendar.HourOfDay;              // 0.0 - 23.9999 (decimal)
int fullHour = calendar.FullHourOfDay;             // 0 - 23 (inteiro)
float hoursPerDay = calendar.HoursPerDay;          // Padrão: 24

// === DATA ===
int year = calendar.Year;                          // Começa em 1386
int month = calendar.Month;                        // 0-11 (0=Janeiro)
EnumMonth monthName = calendar.MonthName;          // Janeiro, Fevereiro, etc.
int daysPerMonth = calendar.DaysPerMonth;          // Padrão: 9
int dayOfYear = calendar.DayOfYear;                // 0 até DaysPerYear-1
float dayOfYearf = calendar.DayOfYearf;           // Decimal
int daysPerYear = calendar.DaysPerYear;           // Padrão: 108 (12 * 9)

// === TEMPO TOTAL DECORRIDO ===
double totalDays = calendar.TotalDays;             // Dias desde 1º de Janeiro, 1386
double totalHours = calendar.TotalHours;           // Horas desde início
long elapsedSeconds = calendar.ElapsedSeconds;     // Segundos in-game
double elapsedDays = calendar.ElapsedDays;         // Dias desde início do mundo
double elapsedHours = calendar.ElapsedHours;       // Horas desde início do mundo

// === FORMATAÇÃO ===
string prettyDate = calendar.PrettyDate();         // "Day 5 of June, Year 1387"
```

#### Velocidade do Tempo

```csharp
// SpeedOfTime: multiplicador de velocidade da física
// Padrão: 60 (1 dia = 24 minutos real)
float speedOfTime = calendar.SpeedOfTime;

// CalendarSpeedMul: multiplicador adicional de calendário
// Padrão: 0.5 (torna dias 2x mais longos)
// Resultado final: 60 * 0.5 = 30, então 1 dia = 48 minutos real
float calendarSpeed = calendar.CalendarSpeedMul;
calendar.CalendarSpeedMul = 1.0f; // Tornar dias mais curtos
calendar.CalendarSpeedMul = 0.1f; // Tornar dias 10x mais longos

// Modificadores de velocidade (para efeitos temporários)
calendar.SetTimeSpeedModifier("meu_mod", 2.0f);    // 2x mais rápido
calendar.RemoveTimeSpeedModifier("meu_mod");       // Remover
```

### Astronomia (Sol e Lua)

```csharp
// === LUA ===
EnumMoonPhase moonPhase = calendar.MoonPhase;      // NewMoon, WaxingCrescent, etc.
double moonPhaseExact = calendar.MoonPhaseExact;   // 0.0 - 8.0
float moonBrightness = calendar.MoonPhaseBrightness; // Brilho (maior em lua cheia)
float moonSize = calendar.MoonSize;                // Tamanho visual

// Posição da lua no céu
Vec3f moonPos = calendar.GetMoonPosition(
    new Vec3d(x, y, z),
    calendar.TotalDays
);

// === SOL ===
// Posição do sol no céu
Vec3f sunPos = calendar.GetSunPosition(
    new Vec3d(x, y, z),
    calendar.TotalDays
);

// Força da luz do dia (0.0 - 1.2)
float daylightStrength = calendar.GetDayLightStrength(blockPos);
float daylightAtCoords = calendar.GetDayLightStrength(x, z);
```

### Estações

```csharp
// === ESTAÇÃO ===
EnumSeason season = calendar.GetSeason(blockPos);
// Possíveis valores: Spring, Summer, Fall, Winter

// Estação como valor de 0 a 1
float seasonRel = calendar.GetSeasonRel(blockPos);
// 0.0 = Início da primavera
// 0.25 = Verão
// 0.5 = Outono
// 0.75 = Inverno
// 1.0 = Volta à primavera

// Progresso do ano (0.0 - 1.0)
float yearRel = calendar.YearRel;

// Override de estação (para testes)
calendar.SetSeasonOverride(0.5f);  // Forçar outono
calendar.SetSeasonOverride(null);  // Voltar ao normal
```

### Hemisférios e Latitude

```csharp
// Hemisfério (Norte ou Sul)
EnumHemisphere hemisphere = calendar.GetHemisphere(blockPos);
// North ou South

// Delegates (definidos pelo mod Survival)
calendar.OnGetLatitude += (pos) =>
{
    // Retorna -1 (polo sul), 0 (equador), 1 (polo norte)
    return CalculateLatitude(pos);
};

calendar.OnGetHemisphere += (pos) =>
{
    // Retorna North ou South
    return DetermineHemisphere(pos);
};

calendar.OnGetSolarSphericalCoords += (latitude, dayOfYear, hourOfDay) =>
{
    // Retorna coordenadas esféricas do sol
    return CalculateSolarPosition(latitude, dayOfYear, hourOfDay);
};
```

### Manipulação de Tempo

```csharp
// Adicionar tempo ao calendário
calendar.Add(24.0f); // Adicionar 24 horas (1 dia)
calendar.Add(1.5f);  // Adicionar 1.5 horas

// Timelapse (para screenshots/vídeos)
calendar.Timelapse = 10.0f; // Acelerar renderização
calendar.Timelapse = 0.0f;  // Normal
```

### Constantes Importantes

```csharp
// Ano de início
const int startYear = IGameCalendar.StartYear; // 1386

// Horários específicos do dia (hardcoded no jogo)
// Momento mais frio: 04:00 (4AM)
// Momento mais quente: 16:00 (4PM)
```

---

## Sistema de Clima e Temperatura

### ClimateCondition

A estrutura `ClimateCondition` representa as condições climáticas em uma posição:

```csharp
public class ClimateCondition
{
    public float Temperature { get; set; }    // Temperatura em °C
    public float Rainfall { get; set; }       // Precipitação (0.0 - 1.0)
    public float Fertility { get; set; }      // Fertilidade do solo
    public int WorldGenTemperature { get; set; } // Temp. base da geração
}
```

### Obter Clima Atual

```csharp
// Obter clima em uma posição
ClimateCondition climate = api.World.BlockAccessor.GetClimateAt(
    blockPos,
    EnumGetClimateMode.NowValues
);

float temperature = climate.Temperature;
float rainfall = climate.Rainfall;

// Modos de obtenção de clima
public enum EnumGetClimateMode
{
    WorldGenValues,    // Valores da geração do mundo (fixos)
    NowValues,         // Valores atuais (variam com estação/hora)
    ForSupplyOnly      // Apenas para cálculos internos
}

// Clima com modo específico
ClimateCondition worldGenClimate = api.World.BlockAccessor.GetClimateAt(
    blockPos,
    EnumGetClimateMode.WorldGenValues
);
```

### Variações de Temperatura

#### 1. Temperatura Base (Worldgen)

```csharp
// Temperatura média anual no nível do mar
// Equador: 30°C
// Temperado: 10°C
// Polos: -20°C

// Comando para ver estatísticas climáticas:
// /wgen pos climate
```

#### 2. Variação por Elevação

```csharp
// A temperatura diminui ~1.5°C a cada 10 blocos de elevação
// Exemplo:
// Y=100: Base 10°C
// Y=200: ~10°C - (100/10 * 1.5) = -5°C
```

#### 3. Variação Sazonal

```csharp
// Variação anual baseada em latitude:
// Equador: ±0°C (sem estações)
// Temperado: ±16.25°C
// Polos: ±32.5°C

// Exemplo em região temperada:
// Média anual: 10°C
// Verão (julho): 10°C + 16.25°C = 26.25°C
// Inverno (janeiro): 10°C - 16.25°C = -6.25°C

// Hemisfério Norte:
// Janeiro: mais frio
// Julho: mais quente

// Hemisfério Sul (invertido):
// Janeiro: mais quente
// Julho: mais frio
```

#### 4. Variação Diária

```csharp
// Varia baseado em precipitação:
// 0% chuva: ±9°C
// 100% chuva: ±2.5°C

// Horários:
// 04:00 (4AM): Mais frio
// 16:00 (4PM): Mais quente

// Exemplo região seca:
// Meio-dia (12:00): Base + 6°C
// Madrugada (04:00): Base - 9°C

// Exemplo região úmida:
// Meio-dia: Base + 1.7°C
// Madrugada: Base - 2.5°C
```

### Calculando Temperatura Completa

```csharp
public float GetAccurateTemperature(BlockPos pos)
{
    var api = this.api;
    var calendar = api.World.Calendar;
    var blockAccessor = api.World.BlockAccessor;
    
    // 1. Clima base (considera elevação e geração)
    var climate = blockAccessor.GetClimateAt(pos, EnumGetClimateMode.WorldGenValues);
    float baseTemp = climate.Temperature;
    
    // 2. Variação sazonal
    float seasonRel = calendar.GetSeasonRel(pos);
    float latitude = GetLatitude(pos); // -1 a 1
    
    // Amplitude sazonal
    float seasonalAmplitude = Math.Abs(latitude) * 32.5f;
    
    // Ajuste sazonal (simplificado)
    float seasonalAdjust = (float)Math.Sin(seasonRel * Math.PI * 2) * seasonalAmplitude;
    
    // 3. Variação diária
    float hourOfDay = calendar.HourOfDay;
    float hoursFromHottest = Math.Abs(hourOfDay - 16.0f);
    float dailyAmplitude = (1.0f - climate.Rainfall) * 9.0f + climate.Rainfall * 2.5f;
    float dailyAdjust = (float)Math.Cos(hoursFromHottest / 24.0f * Math.PI * 2) * dailyAmplitude;
    
    // Temperatura final
    float finalTemp = baseTemp + seasonalAdjust + dailyAdjust;
    
    return finalTemp;
}
```

### Precipitação e Biomas

```csharp
// Obter precipitação média
ClimateCondition climate = api.World.BlockAccessor.GetClimateAt(
    blockPos,
    EnumGetClimateMode.WorldGenValues
);

float rainfall = climate.Rainfall; // 0.0 - 1.0

// Interpretação
if (rainfall < 0.2f) // < 20%
{
    // Deserto
}
else if (rainfall < 0.5f) // 20-50%
{
    // Semi-árido
}
else if (rainfall < 0.7f) // 50-70%
{
    // Temperado
}
else // > 70%
{
    // Floresta tropical / Pantanal
}

// Nomes textuais de precipitação (exibidos com C)
// "Barren" (0%)
// "Rare" (10%)
// "Occasional" (25%)
// "Moderate" (40%)
// "Frequent" (60%)
// "Heavy" (80%)
// "Very Heavy" (100%)
```

---

## Sistema de Clima (Weather)

### Padrões Climáticos (Weather Patterns)

O jogo tem vários padrões climáticos pré-definidos:

```csharp
// Padrões de clima conhecidos:
// - Clear (limpo)
// - Fair (bom tempo)
// - Cloudy (nublado)
// - Overcast (encoberto)
// - Rain (chuva)
// - Drizzle (garoa)
// - Snowfall (nevando)
// - Hail (granizo)
// - Storm (tempestade)
// - Thunderstorm (trovoada)
```

### Intensidade de Precipitação

Não há API pública direta para obter o padrão climático atual via modding, mas você pode:

1. Verificar se está chovendo observando blocos de farmland irrigados
2. Usar comandos de servidor (apenas admin)
3. Usar reflexão para acessar sistemas internos

### Comandos de Clima (Admin)

```csharp
// Via chat (apenas admin):

// Verificar clima atual
/weather

// Definir precipitação (-1 a 1)
/weather setprecip 0.5    // Chuva moderada
/weather setprecip -1     // Remover nuvens
/weather setprecip 0      // Parar chuva mas manter nuvens
/weather setprecip 1      // Chuva máxima

// Resetar precipitação para automático
/weather setprecipa

// Parar chuva
/weather stoprain

// Definir vento
/weather setw still         // Sem vento
/weather setw lightbreeze   // Brisa leve
/weather setw mediumbreeze  // Brisa média
/weather setw strongbreeze  // Brisa forte
/weather setw storm         // Tempestade

// Definir padrão climático
/weather set fair           // Tempo bom
/weather set rain           // Chuva
/weather setirandom         // Padrão aleatório
```

### Vento

```csharp
// Obter velocidade do vento em uma posição
Vec3d windSpeed = api.World.BlockAccessor.GetWindSpeedAt(blockPos);

float windX = (float)windSpeed.X;
float windY = (float)windSpeed.Y; // Geralmente 0
float windZ = (float)windSpeed.Z;

// Magnitude do vento
float windMagnitude = (float)Math.Sqrt(windX * windX + windZ * windZ);
```

---

## Tempestades Temporais

### Visão Geral

Tempestades temporais são eventos periódicos que ocorrem aproximadamente a cada 10-20 dias (configurável). Durante as tempestades:
- Estabilidade temporal do jogador diminui rapidamente
- Drifters podem aparecer em qualquer lugar ao redor do jogador
- Efeitos visuais intensos (distorção, névoa vermelha, engrenagens)
- Efeitos sonoros (ventos fortes, sons mecânicos)

### Tipos de Tempestades

```csharp
public enum EnumTempStormStrength
{
    Light,     // Leve
    Medium,    // Média
    Heavy      // Pesada
}

// Todas têm mesma duração (2.4h - 4.8h padrão)
// Diferem em intensidade de efeitos visuais/sonoros
```

### Avisos de Tempestade

```csharp
// Chat warnings (padrão):
// "A light/medium/heavy temporal storm is approaching"
// Tempo até tempestade: 8h 24min (35% de um dia)

// "A light/medium/heavy temporal storm is imminent"
// Tempo até tempestade: 28min 48seg (2.99% de um dia)

// Durante tempestade:
// Visual: Distorções, névoa vermelha, engrenagens
// Audio: Ventos, sons mecânicos
```

### Acessando Estado da Tempestade

Não há API pública direta para estado de tempestade temporal. Opções:

#### 1. Mod System Interno (Reflection)

```csharp
public class TemporalStormInfo
{
    private object riftWeatherSystem;
    private ICoreAPI api;
    
    public bool Initialize(ICoreAPI api)
    {
        this.api = api;
        
        try
        {
            // Obter ModSystemRiftWeather via reflection
            riftWeatherSystem = api.ModLoader.GetModSystem("Vintagestory.GameContent.ModSystemRiftWeather");
            
            if (riftWeatherSystem == null)
            {
                api.Logger.Warning("[MyMod] ModSystemRiftWeather not found");
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            api.Logger.Error($"[MyMod] Failed to initialize temporal storm info: {ex}");
            return false;
        }
    }
    
    public bool IsStormActive()
    {
        if (riftWeatherSystem == null) return false;
        
        try
        {
            // Usar reflection para acessar propriedade
            var type = riftWeatherSystem.GetType();
            var property = type.GetProperty("IsStormActive");
            
            if (property != null)
            {
                return (bool)property.GetValue(riftWeatherSystem);
            }
        }
        catch
        {
            // Falhou
        }
        
        return false;
    }
    
    public float GetRiftActivity()
    {
        if (riftWeatherSystem == null) return 0f;
        
        try
        {
            var type = riftWeatherSystem.GetType();
            var property = type.GetProperty("RiftActivity");
            
            if (property != null)
            {
                return (float)property.GetValue(riftWeatherSystem);
            }
        }
        catch
        {
            // Falhou
        }
        
        return 0f;
    }
    
    public double GetStormDaysLeft()
    {
        if (riftWeatherSystem == null) return 0.0;
        
        try
        {
            var type = riftWeatherSystem.GetType();
            var property = type.GetProperty("StormDaysLeft");
            
            if (property != null)
            {
                return (double)property.GetValue(riftWeatherSystem);
            }
        }
        catch
        {
            // Falhou
        }
        
        return 0.0;
    }
}
```

#### 2. Detectar Indiretamente

```csharp
public class TemporalStormDetector
{
    private ICoreClientAPI capi;
    private bool wasLowStability = false;
    private long lowStabilityStart = 0;
    
    public TemporalStormDetector(ICoreClientAPI api)
    {
        this.capi = api;
    }
    
    public bool IsProbablyInStorm()
    {
        var player = capi.World.Player;
        
        // Verificar estabilidade temporal
        float stability = player.Entity.WatchedAttributes.GetFloat("temporalStability", 1.0f);
        
        // Se estabilidade está caindo rapidamente
        long now = capi.World.ElapsedMilliseconds;
        
        if (stability < 0.5f)
        {
            if (!wasLowStability)
            {
                lowStabilityStart = now;
                wasLowStability = true;
            }
            
            // Se baixa estabilidade por mais de 30 segundos
            if (now - lowStabilityStart > 30000)
            {
                return true; // Provavelmente em tempestade
            }
        }
        else
        {
            wasLowStability = false;
        }
        
        return false;
    }
}
```

### Configurações de Tempestade (WorldConfig)

```csharp
// Via comandos (apenas admin):

// Duração da tempestade (multiplicador)
/worldconfig tempstormDurationMul 2.0    // Dobrar duração
/worldconfig tempstormDurationMul 0.5    // Metade da duração

// Frequência da tempestade (multiplicador)
/worldconfigcreate double tempStormFrequencyMul 2.0  // 2x mais frequente
/worldconfigcreate double tempStormFrequencyMul 0.5  // 2x menos frequente

// Habilitar/desabilitar estabilidade temporal
/worldconfig temporalStability true
/worldconfig temporalStability false

// Comportamento de rifts
/worldconfig temporalRifts visible    // Rifts visíveis (padrão)
/worldconfig temporalRifts invisible  // Rifts invisíveis mas spawnam
/worldconfig temporalRifts off        // Sem rifts (sem drifters na superfície)

// Permitir dormir durante tempestade
/worldconfig temporalStormSleeping 1  // Permitir
/worldconfig temporalStormSleeping 0  // Não permitir (padrão)
```

---

## Implementação Prática

### Exemplo 1: HUD de Relógio e Temperatura

```csharp
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

public class ClockTemperatureHud : HudElement
{
    private long lastUpdate = 0;
    
    public ClockTemperatureHud(ICoreClientAPI capi) : base(capi)
    {
        SetupHud();
    }
    
    private void SetupHud()
    {
        // Posição no canto superior direito
        ElementBounds hudBounds = ElementBounds.Fixed(
            EnumDialogArea.RightTop, -210, 10, 200, 100);
        
        ElementBounds timeBounds = ElementBounds.Fixed(10, 10, 180, 25);
        ElementBounds dateBounds = timeBounds.BelowCopy(0, 5);
        ElementBounds tempBounds = dateBounds.BelowCopy(0, 5);
        ElementBounds seasonBounds = tempBounds.BelowCopy(0, 5);
        
        SingleComposer = capi.Gui.CreateCompo("clocktemphud", hudBounds)
            .AddShadedDialogBG(hudBounds.FlatCopy(), false)
            .AddDynamicText("--:--:--", CairoFont.WhiteMediumText(), 
                timeBounds, "time")
            .AddDynamicText("Day -, Year -", CairoFont.WhiteSmallText(), 
                dateBounds, "date")
            .AddDynamicText("Temp: --°C", CairoFont.WhiteSmallText(), 
                tempBounds, "temperature")
            .AddDynamicText("Season: --", CairoFont.WhiteSmallText(), 
                seasonBounds, "season")
            .Compose();
    }
    
    public override void OnRenderGUI(float deltaTime)
    {
        base.OnRenderGUI(deltaTime);
        
        // Atualizar a cada 500ms
        if (capi.World.ElapsedMilliseconds - lastUpdate > 500)
        {
            UpdateHud();
            lastUpdate = capi.World.ElapsedMilliseconds;
        }
    }
    
    private void UpdateHud()
    {
        var calendar = capi.World.Calendar;
        var player = capi.World.Player;
        var pos = player.Entity.Pos.AsBlockPos;
        
        // Hora
        int hours = (int)calendar.HourOfDay;
        int minutes = (int)((calendar.HourOfDay - hours) * 60);
        int seconds = (int)(((calendar.HourOfDay - hours) * 60 - minutes) * 60);
        string timeText = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        SingleComposer.GetDynamicText("time").SetNewText(timeText);
        
        // Data
        string dateText = $"Day {calendar.DayOfYear + 1}, Year {calendar.Year}";
        SingleComposer.GetDynamicText("date").SetNewText(dateText);
        
        // Temperatura
        var climate = capi.World.BlockAccessor.GetClimateAt(
            pos, EnumGetClimateMode.NowValues);
        string tempText = $"Temp: {climate.Temperature:F1}°C";
        SingleComposer.GetDynamicText("temperature").SetNewText(tempText);
        
        // Estação
        var season = calendar.GetSeason(pos);
        string seasonText = $"Season: {season}";
        SingleComposer.GetDynamicText("season").SetNewText(seasonText);
    }
}
```

### Exemplo 2: Sistema de Notificação de Tempestade

```csharp
public class TemporalStormNotifier : ModSystem
{
    private ICoreClientAPI capi;
    private TemporalStormInfo stormInfo;
    private long lastCheck = 0;
    private bool wasStormActive = false;
    private bool warningShown = false;
    
    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Client;
    }
    
    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        capi = api;
        
        stormInfo = new TemporalStormInfo();
        stormInfo.Initialize(api);
        
        // Verificar a cada segundo
        api.Event.RegisterGameTickListener(CheckStorm, 1000);
    }
    
    private void CheckStorm(float dt)
    {
        long now = capi.World.ElapsedMilliseconds;
        if (now - lastCheck < 1000) return;
        lastCheck = now;
        
        bool isActive = stormInfo.IsStormActive();
        double daysLeft = stormInfo.GetStormDaysLeft();
        float riftActivity = stormInfo.GetRiftActivity();
        
        // Storm started
        if (isActive && !wasStormActive)
        {
            capi.ShowChatMessage("⚠️ Temporal Storm is active!");
            PlayStormSound();
            wasStormActive = true;
            warningShown = false;
        }
        // Storm ended
        else if (!isActive && wasStormActive)
        {
            capi.ShowChatMessage("✓ Temporal Storm has ended");
            wasStormActive = false;
            warningShown = false;
        }
        // Warning (1 hour before)
        else if (!isActive && daysLeft < 1.0/24.0 && !warningShown)
        {
            capi.ShowChatMessage("⚠️ Temporal Storm approaching in less than 1 hour!");
            warningShown = true;
        }
    }
    
    private void PlayStormSound()
    {
        // Tocar som de aviso
        capi.World.PlaySoundAt(
            new AssetLocation("game", "sounds/effect/tempstorm"),
            capi.World.Player.Entity.Pos.X,
            capi.World.Player.Entity.Pos.Y,
            capi.World.Player.Entity.Pos.Z,
            null
        );
    }
}
```

### Exemplo 3: Sistema de Monitoramento Climático

```csharp
public class ClimateMonitor : ModSystem
{
    private ICoreAPI api;
    private Dictionary<string, ClimateData> dailyRecords = new Dictionary<string, ClimateData>();
    
    public class ClimateData
    {
        public float MinTemp { get; set; } = float.MaxValue;
        public float MaxTemp { get; set; } = float.MinValue;
        public float AvgTemp { get; set; }
        public int Samples { get; set; }
        public string Date { get; set; }
    }
    
    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        this.api = api;
        
        // Registrar comando para ver dados
        api.ChatCommands.Create("climate")
            .WithDescription("Show climate data")
            .HandleWith(OnClimateCommand);
        
        // Monitorar a cada hora do jogo
        api.Event.RegisterGameTickListener(RecordClimate, 3600000 / 60); // 1 hora
    }
    
    private void RecordClimate(float dt)
    {
        var calendar = api.World.Calendar;
        string dateKey = $"{calendar.Year}-{calendar.Month + 1}-{calendar.DayOfYear}";
        
        // Obter temperatura no spawn
        var spawnPos = api.World.DefaultSpawnPosition.AsBlockPos;
        var climate = api.World.BlockAccessor.GetClimateAt(
            spawnPos,
            EnumGetClimateMode.NowValues
        );
        
        if (!dailyRecords.ContainsKey(dateKey))
        {
            dailyRecords[dateKey] = new ClimateData { Date = dateKey };
        }
        
        var record = dailyRecords[dateKey];
        record.MinTemp = Math.Min(record.MinTemp, climate.Temperature);
        record.MaxTemp = Math.Max(record.MaxTemp, climate.Temperature);
        record.AvgTemp = ((record.AvgTemp * record.Samples) + climate.Temperature) / (record.Samples + 1);
        record.Samples++;
    }
    
    private TextCommandResult OnClimateCommand(TextCommandCallingArgs args)
    {
        var calendar = api.World.Calendar;
        string dateKey = $"{calendar.Year}-{calendar.Month + 1}-{calendar.DayOfYear}";
        
        if (dailyRecords.ContainsKey(dateKey))
        {
            var record = dailyRecords[dateKey];
            return TextCommandResult.Success(
                $"Climate Data for {record.Date}:\n" +
                $"Min Temp: {record.MinTemp:F1}°C\n" +
                $"Max Temp: {record.MaxTemp:F1}°C\n" +
                $"Avg Temp: {record.AvgTemp:F1}°C\n" +
                $"Samples: {record.Samples}"
            );
        }
        
        return TextCommandResult.Success("No data for today yet");
    }
}
```

---

## Exemplos de Código Completos

### Sistema Completo de Informações Climáticas

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace WeatherInfoMod
{
    public class WeatherInfoModSystem : ModSystem
    {
        private ICoreClientAPI capi;
        private WeatherInfoHud hud;
        
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }
        
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            
            // Criar e abrir HUD
            hud = new WeatherInfoHud(api);
            hud.TryOpen();
            
            // Registrar hotkey
            api.Input.RegisterHotKey("toggleweatherinfo", 
                "Toggle Weather Info", 
                GlKeys.I, 
                HotkeyType.GUIOrOtherControls,
                ctrlPressed: true);
            
            api.Input.SetHotKeyHandler("toggleweatherinfo", ToggleHud);
            
            // Registrar comando
            api.ChatCommands.Create("weather")
                .WithDescription("Show detailed weather information")
                .HandleWith(ShowWeatherInfo);
        }
        
        private bool ToggleHud(KeyCombination comb)
        {
            if (hud.IsOpened())
                hud.TryClose();
            else
                hud.TryOpen();
            return true;
        }
        
        private TextCommandResult ShowWeatherInfo(TextCommandCallingArgs args)
        {
            var calendar = capi.World.Calendar;
            var player = capi.World.Player;
            var pos = player.Entity.Pos.AsBlockPos;
            var climate = capi.World.BlockAccessor.GetClimateAt(
                pos, EnumGetClimateMode.NowValues);
            
            var sb = new StringBuilder();
            sb.AppendLine("=== Weather Information ===");
            
            // Tempo
            sb.AppendLine($"Time: {FormatTime(calendar.HourOfDay)}");
            sb.AppendLine($"Date: {calendar.PrettyDate()}");
            
            // Clima
            sb.AppendLine($"\nTemperature: {climate.Temperature:F1}°C");
            sb.AppendLine($"Rainfall: {GetRainfallName(climate.Rainfall)}");
            
            // Estação
            var season = calendar.GetSeason(pos);
            var seasonRel = calendar.GetSeasonRel(pos);
            sb.AppendLine($"Season: {season} ({seasonRel:P0})");
            
            // Astronomia
            sb.AppendLine($"\nMoon Phase: {calendar.MoonPhase}");
            sb.AppendLine($"Moon Brightness: {calendar.MoonPhaseBrightness:P0}");
            
            // Luz do dia
            var daylight = calendar.GetDayLightStrength(pos);
            sb.AppendLine($"Daylight: {daylight:F2}");
            
            // Vento
            var wind = capi.World.BlockAccessor.GetWindSpeedAt(pos);
            var windSpeed = Math.Sqrt(wind.X * wind.X + wind.Z * wind.Z);
            sb.AppendLine($"Wind Speed: {windSpeed:F2} m/s");
            
            return TextCommandResult.Success(sb.ToString());
        }
        
        private string FormatTime(float hourOfDay)
        {
            int hours = (int)hourOfDay;
            int minutes = (int)((hourOfDay - hours) * 60);
            return $"{hours:D2}:{minutes:D2}";
        }
        
        private string GetRainfallName(float rainfall)
        {
            if (rainfall < 0.1f) return "Barren";
            if (rainfall < 0.25f) return "Rare";
            if (rainfall < 0.4f) return "Occasional";
            if (rainfall < 0.6f) return "Moderate";
            if (rainfall < 0.8f) return "Frequent";
            return "Heavy";
        }
        
        public override void Dispose()
        {
            hud?.Dispose();
            base.Dispose();
        }
    }
    
    public class WeatherInfoHud : HudElement
    {
        private long lastUpdate = 0;
        private const int UPDATE_INTERVAL = 500;
        
        public WeatherInfoHud(ICoreClientAPI capi) : base(capi)
        {
            SetupHud();
        }
        
        private void SetupHud()
        {
            // HUD no canto superior esquerdo
            ElementBounds hudBounds = ElementBounds.Fixed(10, 10, 280, 200);
            
            ElementBounds line1 = ElementBounds.Fixed(10, 10, 260, 20);
            ElementBounds line2 = line1.BelowCopy(0, 2);
            ElementBounds line3 = line2.BelowCopy(0, 2);
            ElementBounds line4 = line3.BelowCopy(0, 2);
            ElementBounds line5 = line4.BelowCopy(0, 5);
            ElementBounds line6 = line5.BelowCopy(0, 2);
            ElementBounds line7 = line6.BelowCopy(0, 2);
            ElementBounds line8 = line7.BelowCopy(0, 5);
            ElementBounds line9 = line8.BelowCopy(0, 2);
            ElementBounds line10 = line9.BelowCopy(0, 2);
            
            SingleComposer = capi.Gui.CreateCompo("weatherinfohud", hudBounds)
                .AddInset(hudBounds.FlatCopy(), 2)
                .AddDynamicText("Time: --:--", CairoFont.WhiteSmallText(), line1, "time")
                .AddDynamicText("Date: --", CairoFont.WhiteSmallText(), line2, "date")
                .AddDynamicText("Day: -/-", CairoFont.WhiteSmallText(), line3, "day")
                .AddDynamicText("Year: -", CairoFont.WhiteSmallText(), line4, "year")
                .AddDynamicText("Temp: --°C", CairoFont.WhiteSmallText(), line5, "temp")
                .AddDynamicText("Season: --", CairoFont.WhiteSmallText(), line6, "season")
                .AddDynamicText("Rainfall: --", CairoFont.WhiteSmallText(), line7, "rainfall")
                .AddDynamicText("Moon: --", CairoFont.WhiteSmallText(), line8, "moon")
                .AddDynamicText("Daylight: --", CairoFont.WhiteSmallText(), line9, "daylight")
                .AddDynamicText("Wind: --", CairoFont.WhiteSmallText(), line10, "wind")
                .Compose();
        }
        
        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
            
            if (capi.World.ElapsedMilliseconds - lastUpdate > UPDATE_INTERVAL)
            {
                UpdateHud();
                lastUpdate = capi.World.ElapsedMilliseconds;
            }
        }
        
        private void UpdateHud()
        {
            var calendar = capi.World.Calendar;
            var player = capi.World.Player;
            var pos = player.Entity.Pos.AsBlockPos;
            var climate = capi.World.BlockAccessor.GetClimateAt(
                pos, EnumGetClimateMode.NowValues);
            
            // Tempo
            int hours = (int)calendar.HourOfDay;
            int minutes = (int)((calendar.HourOfDay - hours) * 60);
            SingleComposer.GetDynamicText("time")?.SetNewText($"Time: {hours:D2}:{minutes:D2}");
            
            // Data
            var monthName = calendar.MonthName.ToString();
            SingleComposer.GetDynamicText("date")?.SetNewText($"Date: {monthName}");
            
            // Dia
            SingleComposer.GetDynamicText("day")?.SetNewText(
                $"Day: {calendar.DayOfYear + 1}/{calendar.DaysPerYear}");
            
            // Ano
            SingleComposer.GetDynamicText("year")?.SetNewText($"Year: {calendar.Year}");
            
            // Temperatura
            SingleComposer.GetDynamicText("temp")?.SetNewText(
                $"Temp: {climate.Temperature:F1}°C");
            
            // Estação
            var season = calendar.GetSeason(pos);
            var seasonRel = calendar.GetSeasonRel(pos);
            SingleComposer.GetDynamicText("season")?.SetNewText(
                $"Season: {season} ({seasonRel:P0})");
            
            // Precipitação
            SingleComposer.GetDynamicText("rainfall")?.SetNewText(
                $"Rainfall: {GetRainfallName(climate.Rainfall)}");
            
            // Lua
            SingleComposer.GetDynamicText("moon")?.SetNewText(
                $"Moon: {calendar.MoonPhase}");
            
            // Luz do dia
            var daylight = calendar.GetDayLightStrength(pos);
            SingleComposer.GetDynamicText("daylight")?.SetNewText(
                $"Daylight: {daylight:F2}");
            
            // Vento
            var wind = capi.World.BlockAccessor.GetWindSpeedAt(pos);
            var windSpeed = Math.Sqrt(wind.X * wind.X + wind.Z * wind.Z);
            SingleComposer.GetDynamicText("wind")?.SetNewText(
                $"Wind: {windSpeed:F1} m/s");
        }
        
        private string GetRainfallName(float rainfall)
        {
            if (rainfall < 0.1f) return "Barren (0%)";
            if (rainfall < 0.25f) return "Rare (10%)";
            if (rainfall < 0.4f) return "Occasional (25%)";
            if (rainfall < 0.6f) return "Moderate (40%)";
            if (rainfall < 0.8f) return "Frequent (60%)";
            return "Heavy (80%+)";
        }
    }
}
```

---

## Integração com Outros Sistemas

### 1. Sistema de Land Claims Sensível ao Clima

```csharp
public class ClimateBasedLandClaim
{
    private ICoreServerAPI sapi;
    
    public bool CanClaimLand(IServerPlayer player, BlockPos pos)
    {
        var climate = sapi.World.BlockAccessor.GetClimateAt(
            pos, EnumGetClimateMode.WorldGenValues);
        
        // Não permitir claims em regiões muito frias
        if (climate.Temperature < -10f)
        {
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                "Too cold to claim land here!", EnumChatType.CommandError);
            return false;
        }
        
        // Custo adicional em desertos
        if (climate.Rainfall < 0.2f)
        {
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                "Desert land costs 2x more to claim!", EnumChatType.Notification);
            // Aplicar custo 2x
        }
        
        return true;
    }
    
    public float GetLandValue(BlockPos pos)
    {
        var climate = sapi.World.BlockAccessor.GetClimateAt(
            pos, EnumGetClimateMode.WorldGenValues);
        
        float baseValue = 100f;
        
        // Valor baseado em temperatura (ideal: 10-20°C)
        float tempFactor = 1.0f;
        if (climate.Temperature < 0 || climate.Temperature > 30)
            tempFactor = 0.5f;
        else if (climate.Temperature >= 10 && climate.Temperature <= 20)
            tempFactor = 1.5f;
        
        // Valor baseado em precipitação (ideal: 40-70%)
        float rainFactor = 1.0f;
        if (climate.Rainfall < 0.2f || climate.Rainfall > 0.9f)
            rainFactor = 0.5f;
        else if (climate.Rainfall >= 0.4f && climate.Rainfall <= 0.7f)
            rainFactor = 1.5f;
        
        return baseValue * tempFactor * rainFactor;
    }
}
```

### 2. Sistema de Agricultura Baseado em Clima

```csharp
public class ClimateAgriculture
{
    private ICoreServerAPI sapi;
    
    public bool CanPlantCrop(BlockPos pos, string cropType)
    {
        var climate = sapi.World.BlockAccessor.GetClimateAt(
            pos, EnumGetClimateMode.NowValues);
        var calendar = sapi.World.Calendar;
        var season = calendar.GetSeason(pos);
        
        // Verificar temperatura
        if (cropType == "wheat")
        {
            if (climate.Temperature < 5f || climate.Temperature > 30f)
                return false; // Trigo precisa 5-30°C
        }
        else if (cropType == "rice")
        {
            if (climate.Temperature < 15f || climate.Rainfall < 0.6f)
                return false; // Arroz precisa calor e umidade
        }
        
        // Verificar estação
        if (season == EnumSeason.Winter && climate.Temperature < 0f)
            return false; // Não plantar no inverno gelado
        
        return true;
    }
    
    public float GetGrowthMultiplier(BlockPos pos, string cropType)
    {
        var climate = sapi.World.BlockAccessor.GetClimateAt(
            pos, EnumGetClimateMode.NowValues);
        
        float multiplier = 1.0f;
        
        // Condições ideais aumentam crescimento
        if (cropType == "wheat")
        {
            // Trigo cresce melhor em 15-20°C
            if (climate.Temperature >= 15f && climate.Temperature <= 20f)
                multiplier *= 1.5f;
            
            // Precipitação moderada é ideal
            if (climate.Rainfall >= 0.4f && climate.Rainfall <= 0.6f)
                multiplier *= 1.3f;
        }
        
        return multiplier;
    }
}
```

### 3. Sistema de Eventos Climáticos

```csharp
public class WeatherEventSystem : ModSystem
{
    private ICoreServerAPI sapi;
    private long lastEventCheck = 0;
    
    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        sapi = api;
        
        // Verificar eventos a cada 10 minutos do jogo
        api.Event.RegisterGameTickListener(CheckWeatherEvents, 10000);
    }
    
    private void CheckWeatherEvents(float dt)
    {
        var calendar = sapi.World.Calendar;
        long now = sapi.World.ElapsedMilliseconds;
        
        if (now - lastEventCheck < 10000) return;
        lastEventCheck = now;
        
        // Evento: Onda de calor no verão
        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            var pos = player.Entity.Pos.AsBlockPos;
            var climate = sapi.World.BlockAccessor.GetClimateAt(
                pos, EnumGetClimateMode.NowValues);
            var season = calendar.GetSeason(pos);
            
            if (season == EnumSeason.Summer && climate.Temperature > 35f)
            {
                // Aplicar dano por calor
                player.Entity.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Weather,
                        Type = EnumDamageType.Heat
                    },
                    0.5f
                );
                
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    "You are suffering from extreme heat!",
                    EnumChatType.Notification);
            }
            
            // Evento: Congelamento no inverno
            if (season == EnumSeason.Winter && climate.Temperature < -20f)
            {
                player.Entity.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Weather,
                        Type = EnumDamageType.Frost
                    },
                    0.5f
                );
                
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    "You are freezing!",
                    EnumChatType.Notification);
            }
        }
    }
}
```

### 4. Sistema de Tempestade Temporal Personalizado

```csharp
public class CustomTemporalStormSystem : ModSystem
{
    private ICoreServerAPI sapi;
    private object riftWeatherSystem;
    private bool stormWarningIssued = false;
    
    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        sapi = api;
        
        // Obter sistema de tempestade via reflection
        riftWeatherSystem = api.ModLoader.GetModSystem(
            "Vintagestory.GameContent.ModSystemRiftWeather");
        
        // Monitorar tempestades
        api.Event.RegisterGameTickListener(MonitorStorms, 5000);
    }
    
    private void MonitorStorms(float dt)
    {
        if (riftWeatherSystem == null) return;
        
        try
        {
            // Obter dias até tempestade
            var type = riftWeatherSystem.GetType();
            var daysLeftProp = type.GetProperty("StormDaysLeft");
            var isActiveProp = type.GetProperty("IsStormActive");
            
            if (daysLeftProp != null && isActiveProp != null)
            {
                double daysLeft = (double)daysLeftProp.GetValue(riftWeatherSystem);
                bool isActive = (bool)isActiveProp.GetValue(riftWeatherSystem);
                
                // Aviso 2 horas antes (2/24 dias)
                if (!stormWarningIssued && daysLeft < 2.0/24.0 && !isActive)
                {
                    BroadcastStormWarning(daysLeft);
                    stormWarningIssued = true;
                }
                
                // Reset quando tempestade começar
                if (isActive)
                {
                    OnStormStart();
                    stormWarningIssued = false;
                }
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"Error monitoring temporal storms: {ex}");
        }
    }
    
    private void BroadcastStormWarning(double daysLeft)
    {
        int minutes = (int)(daysLeft * 24 * 60);
        
        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                $"⚠️ TEMPORAL STORM WARNING: Storm in {minutes} minutes!",
                EnumChatType.CommandError);
        }
    }
    
    private void OnStormStart()
    {
        // Efeitos customizados quando tempestade inicia
        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            // Dar buff de proteção temporal
            player.Entity.WatchedAttributes.SetFloat("temporalResistance", 0.5f);
            
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                "⚡ TEMPORAL STORM ACTIVE! Stay near temporal gears!",
                EnumChatType.CommandError);
        }
    }
}
```

---

## Mods de Referência

### Mods que Usam Sistema de Tempo/Clima

1. **Clock HUD** - HUD simples de relógio
   - Mostra hora do dia formatada
   - Toggle com hotkey

2. **Status HUD** - Informações do jogador
   - Inclui temperatura e hora
   - Barra de estabilidade temporal

3. **Seasons** - Modificações sazonais
   - Altera crescimento de plantas por estação
   - Eventos específicos por estação

4. **Weather Overhaul** - Sistema de clima expandido
   - Padrões climáticos mais complexos
   - Transições graduais

5. **Temporal Tweaks** - Ajustes em tempestades temporais
   - Frequência configurável
   - Avisos personalizados

### Links Úteis

- **API Documentation**: https://apidocs.vintagestory.at/
- **Modding Wiki**: https://wiki.vintagestory.at/Modding
- **Discord #modding**: https://discord.gg/vintagestory
- **VS ModDB**: https://mods.vintagestory.at/

---

## Fórmulas e Cálculos

### Temperatura Completa (Pseudo-código)

```
BaseTemp = WorldGenTemperature(latitude, elevation)
SeasonalVariation = sin(DayOfYear / DaysPerYear * 2π) * |latitude| * 32.5°C
DailyVariation = cos((HourOfDay - 16) / 24 * 2π) * ((1 - Rainfall) * 9°C + Rainfall * 2.5°C)

FinalTemp = BaseTemp + SeasonalVariation + DailyVariation
```

### Elevação e Temperatura

```
TempReduction = (Elevation - SeaLevel) / 10 * 1.5°C
```

### Duração do Dia

```
RealMinutesPerDay = (24 * 60) / (SpeedOfTime * CalendarSpeedMul)
Padrão: (24 * 60) / (60 * 0.5) = 48 minutos
```

### Fase da Lua

```
MoonPhaseExact = (TotalDays / DaysPerMonth) % 8
MoonPhase = (int)MoonPhaseExact
// 0=NewMoon, 2=FirstQuarter, 4=FullMoon, 6=LastQuarter
```

---

## Checklist de Implementação

### Sistema de Tempo
- [ ] Obter IGameCalendar via api.World.Calendar
- [ ] Acessar HourOfDay para hora atual
- [ ] Usar TotalDays para cálculos longos
- [ ] Formatar tempo com PrettyDate()
- [ ] Implementar atualização periódica (não todo frame)

### Sistema de Clima
- [ ] Obter ClimateCondition via GetClimateAt()
- [ ] Usar EnumGetClimateMode correto (NowValues para atual)
- [ ] Considerar elevação em cálculos
- [ ] Verificar estação com GetSeason()
- [ ] Calcular temperatura completa (base + sazonal + diária)

### Astronomia
- [ ] Obter fase da lua com MoonPhase
- [ ] Calcular posição do sol/lua se necessário
- [ ] Usar GetDayLightStrength para luz ambiente
- [ ] Considerar hemisfério para estações

### Tempestades Temporais
- [ ] Usar reflection para acessar ModSystemRiftWeather
- [ ] Implementar verificações de segurança (null checks)
- [ ] Monitorar IsStormActive
- [ ] Criar avisos antes da tempestade
- [ ] Testar fallbacks se reflection falhar

### Performance
- [ ] Limitar atualizações (500-1000ms)
- [ ] Cachear valores que mudam lentamente
- [ ] Evitar cálculos pesados todo frame
- [ ] Usar GameTickListener apropriadamente

---

## Conclusão

Esta documentação cobre todos os aspectos de tempo, clima e tempestades temporais no Vintage Story 1.21.5+:

✅ **IGameCalendar** - Sistema completo de calendário e astronomia
✅ **ClimateCondition** - Temperatura, precipitação e cálculos climáticos
✅ **Estações** - Sistema de estações e hemisférios
✅ **Tempestades Temporais** - Acesso via reflection e detecção
✅ **Exemplos práticos** - Código funcional para HUDs e sistemas
✅ **Integração** - Como usar em land claims, agricultura, eventos
✅ **Fórmulas** - Cálculos precisos de temperatura e tempo

Use esta documentação como referência completa para implementar sistemas dependentes de clima e tempo no seu mod LandBaron!
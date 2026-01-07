# Mastery RPG System - Plano de Melhoria Completo

**Vintage Story 1.21.5**

---

## 📊 DIAGNÓSTICO ATUAL

### Problemas Identificados

1. **Recompensas Invisíveis**: Buffs passivos (+10% velocidade) não são perceptíveis durante gameplay
2. **Progressão Linear**: Sistema de XP previsível sem escolhas ou momentos épicos
3. **Zero Feedback Tátil**: Falta efeitos visuais/sonoros impactantes
4. **Impacto Gameplay = 0**: Jogador não sente diferença real entre níveis

### Objetivo

Transformar o mod em um sistema RPG que o jogador **sinta**, com recompensas tangíveis, escolhas significativas e momentos memoráveis. O mod passa a ser também, client side (universal), para que o jogador possa sentir a diferença real entre níveis.

---

## 🎯 ROADMAP DE IMPLEMENTAÇÃO

### FASE 1: Quick Wins

**Objetivo**: Impacto máximo com esforço mínimo

#### 1.1 HABILIDADES ATIVAS ⭐ PRIORIDADE MÁXIMA

**O que implementar:**
Cada profissão ganha uma habilidade ativável (tecla V) no nível 3:

- **[Mestre] Minerador**: "Explosão Controlada"
  - Próximos 10 blocos quebram instantaneamente
  - Cooldown: 10 minutos
  - Efeito visual: Partículas de poeira + som de explosão abafada

- **[Mestre] Lenhador**: "Corte Limpo"
  - Derruba árvores inteiras de uma vez em um rádio de 10 blocos.
  - Cooldown: 10 minutos
  - Efeito visual: Linha de corte luminosa + som de serra

- **[Mestre] Agricultor**: "Bênção da Colheita"
  - Todas plantas em raio de 10 blocos crescem instantaneamente
  - Cooldown: 60 minutos
  - Efeito visual: Onda verde de energia + brilho nas plantas

- **[Mestre] Guerreiro**: "Investida Brutal"
  - Speed boost + próximo ataque causa dano em área (3x3) e cura o jogador a vida inteira ao atingir um mob pela primeira vez.
  - Cooldown: 10 minutos
  - Efeito visual: Rastro vermelho + tela treme no impacto

**Implementação técnica:**

```csharp
// Adicionar no StartServerSide
api.Input.RegisterHotKey("mastery_ability", "Habilidade Mastery", GlKeys.V);
api.Input.SetHotKeyHandler("mastery_ability", OnAbilityKeyPressed);

// Novo método
private bool OnAbilityKeyPressed(KeyCombination key)
{
    IServerPlayer player = sapi.World.PlayerByUid(/* get current player */);
    if (!masteryCache.TryGetValue(player.PlayerUID, out var data)) return false;
    
    // Verificar cooldown (usar WatchedAttributes para persistir)
    long lastUsed = player.Entity.WatchedAttributes.GetLong("mastery_ability_cooldown");
    long now = sapi.World.Calendar.TotalHours;
    
    if (now - lastUsed < GetCooldownHours(data)) {
        player.SendMessage("Habilidade em cooldown!", EnumChatType.Notification);
        return true;
    }
    
    // Ativar habilidade baseado na profissão mais alta
    var highestSkill = data.Experience.OrderByDescending(x => x.Value).First();
    ExecuteAbility(player, highestSkill.Key);
    
    player.Entity.WatchedAttributes.SetLong("mastery_ability_cooldown", now);
    return true;
}
```

**Riscos:**

- Keybindings podem conflitar com outros mods
- Precisa testar multiplayer (sincronização server/client)

---

#### 1.2 EFEITOS VISUAIS E SONOROS

**O que implementar:**
Quando jogador sobe de nível:

1. **Efeitos visuais:**
   - Explosão de partículas douradas ao redor do player
   - Tela pisca em branco por 0.2s
   - Player brilha por 3 segundos

2. **Efeitos sonoros:**
   - Som de "level up" épico (usar sons existentes do VS ou criar)
   - Som de sino/gongo no nível 3

3. **Mensagem na tela:**
   - Centro da tela (não só chat)
   - Fonte grande + animação de fade-in/out

**Implementação técnica:**

```csharp
// No método GiveXP, após detectar level up:
private void TriggerLevelUpEffects(IServerPlayer player, MasteryType type, int newLevel)
{
    // Partículas
    player.Entity.World.SpawnParticles(new SimpleParticleProperties 
    {
        MinPos = player.Entity.Pos.XYZ.Add(0, 1, 0),
        AddPos = new Vec3d(0.5, 0.5, 0.5),
        MinVelocity = new Vec3f(-0.5f, 1f, -0.5f),
        AddVelocity = new Vec3f(1f, 2f, 1f),
        Color = ColorUtil.ToRgba(255, 255, 215, 0), // Dourado
        GravityEffect = -0.1f,
        MinSize = 0.3f,
        MaxSize = 1f,
        MinQuantity = 50,
        MaxQuantity = 100,
        LifeLength = 2f,
        ParticleModel = EnumParticleModel.Cube
    });
    
    // Som
    player.Entity.World.PlaySoundAt(
        new AssetLocation("game:sounds/effect/latch"), 
        player.Entity, 
        null, 
        true, 
        32, 
        1f
    );
    
    // Mensagem central (usar API de GUI customizado)
    string titleName = GetTitleName(type, newLevel);
    sapi.SendMessage(player, 0, 
        $"<font size=\"24\" color=\"#FFD700\">★ LEVEL UP! ★</font>\n<font size=\"18\">{titleName}</font>", 
        EnumChatType.Notification);
}
```

**Riscos:**

- Performance em servidores com muitos players
- Limitar spawn de partículas se FPS < 30

---

### FASE 2: Profundidade

**Objetivo**: Adicionar camadas de escolha e replayability

#### 2.1 SISTEMA DE ESPECIALIZAÇÃO

**O que implementar:**
No nível 2, jogador escolhe um "caminho" (permanente):

**Minerador:**

- **Escavador**: +20% velocidade mineração, -30% gasto fome
- **Geólogo**: Minérios em dobro, detecta minérios a 5 blocos (som de brilho)

**Lenhador:**

- **Lenhador Veloz**: +30% velocidade, +20% durabilidade machado
- **Silvicultor**: Árvores derrubadas têm 20% chance de dropar o dobro de madeira.

**Agricultor:**

- **Fazendeiro**: Colheitas dão +50% itens (ex: trigo vira 6 em vez de 4)
- **Herbalista**: Ao agachar por 10 segundos próximo a plantações, todas as plantações começam a crescer 20% mais rápido num raio de 20 blocos. Efeito acaba/para quando o jogador para de agachar. (Particulas verdes saindo do jogador enquanto o efeito está ativo, com um leve som de brilho)

**Guerreiro:**

- **Berserker**: +45% dano, -10% defesa
- **Tanque**: +30% HP, +20% defesa, -10% velocidade

**Implementação técnica:**

```csharp
// Mostrar GUI de escolha quando atingir level 2
private void ShowSpecializationChoice(IServerPlayer player, MasteryType type)
{
    // Criar GUI customizado com 2 botões
    // Salvar escolha em WatchedAttributes
    player.Entity.WatchedAttributes.SetString($"{type}_spec", "excavator");
}

// Aplicar buffs baseado na especialização
private void ApplySpecializationBuffs(IServerPlayer player, string spec)
{
    switch(spec) {
        case "excavator":
            player.Entity.Stats.Set("miningSpeedMultiplier", "spec", 0.2f, true);
            player.Entity.Stats.Set("hungerrate", "spec", 1.3f, true);
            break;
        // ... outros casos
    }
}
```

**Riscos:**

- GUI customizado é trabalhoso no VS
- Alternativa: usar comando chat `/escolher escavador` (Usar em paralelo com a GUI, como um alternativa)

---

#### 2.2 EVENTOS ALEATÓRIOS

**O que implementar:**
1% de chance por ação de triggar evento especial:

**Eventos:**

- **Minerador**: Spawn Golem de Pedra (boss mini)
  - Recompensa: +20 drops de minério raro (quartzo/esmeralda)
  
- **Lenhador**: Árvore Encantada
  - Recompensa: +10 de qualquer semente (crescem 2x mais rápido)
  
- **Agricultor**: Chuva Abençoada
  - Todas plantas em chunk crescem 50% e são regadas totalmente.
  
- **Guerreiro**: Campeão aparece
  - Mob Hostil com 3x HP e dano, dropa equipamento raro (Evento raro)

**Implementação técnica:**

```csharp
// No método GiveXP, adicionar:
if (sapi.World.Rand.NextDouble() < 0.01) // 1% chance
{
    TriggerRandomEvent(player, type);
}

private void TriggerRandomEvent(IServerPlayer player, MasteryType type)
{
    switch(type) {
        case MasteryType.Mining:
            SpawnGolem(player);
            break;
        // ... outros
    }
}

private void SpawnGolem(IServerPlayer player)
{
    EntityProperties entityType = sapi.World.GetEntityType(new AssetLocation("game:drifter-deep"));
    Entity golem = sapi.World.ClassRegistry.CreateEntity(entityType);
    
    // Modificar stats
    golem.Stats.Set("maxhealthExtraPoints", "event", 50f, true);
    golem.WatchedAttributes.SetString("event_type", "mastery_golem");
    
    // Spawn perto do player
    Vec3d pos = player.Entity.Pos.XYZ.Add(5, 0, 5);
    golem.ServerPos.SetPos(pos);
    sapi.World.SpawnEntity(golem);
    
    player.SendMessage("⚠️ UM GOLEM DESPERTA!", EnumChatType.Notification);
}
```

**Riscos:**

- Spawnar mobs pode bugar em caves fechadas
- Adicionar verificação de espaço livre

---

### FASE 3: Ambição Máxima

**Objetivo**: Transformar em sistema definitivo

#### 3.1 PARTY/GUILD BUFFS (Multiplayer)

**O que implementar:**
Players próximos (10 blocos) com profissões diferentes ganham buffs sinérgicos:

**Combinações:**

- Minerador + Guerreiro = Guerreiro +20% dano, Minerador +10% drop gemas
- Agricultor + Lenhador = Ambos +15% velocidade coleta
- Qualquer 2 Mestres = +10% XP para ambos

**Implementação técnica:**

```csharp
// Adicionar no StartServerSide
api.Event.RegisterGameTickListener(CheckPartyBuffs, 5000); // Check a cada 5s

private void CheckPartyBuffs(float dt)
{
    foreach (var player in sapi.World.AllOnlinePlayers)
    {
        var nearby = GetNearbyPlayers(player, 10);
        ApplySynergyBuffs(player, nearby);
    }
}
```

**Riscos:**

- Performance: loop a cada 5s pode laggear em servers grandes
- Otimizar com spatial hashing

---

#### 3.2 ACHIEVEMENTS E TÍTULOS CUSTOMIZÁVEISs

**O que implementar:**
Sistema de conquistas + títulos alternativos:

**Achievements:**

- "Escavador Obcecado": Quebrou 10.000 blocos rocha
- "Lenhador das Trevas": Cortou 1.000 árvores à noite
- "Fazendeiro Persistente": Farmou 5.000 plantas sem morrer
- "Matador de Gigantes": Matou 100 Golems

**Cada achievement desbloqueia título alternativo:**

- `[Obcecado]`, `[Noturno]`, `[Persistente]`, `[Caçador de Gigantes]`

**Implementação técnica:**

```csharp
// Novo arquivo: AchievementSystem.cs
public class Achievement
{
    public string Id;
    public string Name;
    public string Description;
    public MasteryType Type;
    public int TargetValue;
    public string UnlockedTitle;
}

// Tracking de progresso
private void TrackAchievement(IServerPlayer player, string achievementId, int progress)
{
    var tree = player.Entity.WatchedAttributes.GetOrCreateTreeAttribute("achievements");
    int current = tree.GetInt(achievementId);
    tree.SetInt(achievementId, current + progress);
    
    CheckAchievementComplete(player, achievementId);
}
```

---

## 📦 ESTRUTURA DE ARQUIVOS ATUALIZADA

```
MasteryTitles/
├── modinfo.json
├── src/
│   └── MasterySystem.cs (CORE)
│   └── AbilitySystem.cs (NOVO - Fase 1.1)
│   └── SpecializationSystem.cs (NOVO - Fase 2.1)
│   └── EventSystem.cs (NOVO - Fase 2.2)
│   └── PartyBuffSystem.cs (NOVO - Fase 3.1)
│   └── AchievementSystem.cs (NOVO - Fase 3.2)
├── assets/
│   └── masterytitles/
│       ├── recipes/
│       │   ├── reinforced_pickaxe.json
│       │   ├── jade_axe.json
│       │   └── ...
│       ├── sounds/ (se criar sons custom)
│       └── textures/ (ícones de habilidades)
└── MasterySystem.csproj
```

---

## ⚠️ RISCOS E MITIGAÇÕES

### Riscos Técnicos

1. **Performance em multiplayer**
   - Mitigação: Usar eventos com throttling (5-10s)
   - Testar com 10+ players simultâneos

2. **Conflito com outros mods**
   - Mitigação: Usar namespaces únicos, evitar modificar stats base do jogo
   - Testar com mods populares (Extra Ores, Medieval Expansion)

3. **Balanceamento gameplay**
   - Mitigação: Começar com valores conservadores
   - Adicionar config.json editável pelo player

### Riscos de Design

1. **Poder demais = jogo fácil demais**
   - Mitigação: Cooldowns longos, custo de recursos para habilidades

2. **Grind muito longo = frustração**
   - Mitigação: XP curve ajustável via config

---

## 🎮 ORDEM DE IMPLEMENTAÇÃO RECOMENDADA

### Semana 1 (MVP que já melhora 300%)

1. Habilidades Ativas (Fase 1.1)
2. Efeitos Visuais (Fase 1.2)
3. Testar e balancear

### Semana 2 (Sistema robusto)

1. Receitas (Fase 1.3)
2. Especialização (Fase 2.1)
3. Testar multiplayer

### Semana 3 (Polimento)

1. Eventos Aleatórios (Fase 2.2)
2. Ajustes de balanceamento

### Futuro (Se virar projeto sério)

1. Party Buffs (Fase 3.1)
2. Achievements (Fase 3.2)
3. Config GUI ingame

---

## 📝 CHECKLIST DE TESTE

Antes de cada release:

- [ ] Testar cada habilidade 10x (funciona? cooldown ok? efeitos visuais?)
- [ ] Jogar 1 hora minerando (XP tá balanceado? Fica tedioso?)
- [ ] Morrer propositalmente (dados salvam? buffs resetam corretamente?)
- [ ] Testar com 2 players (sincronização ok? party buffs funcionam?)
- [ ] Verificar logs (erros? warnings?)
- [ ] Testar com mods populares (conflitos?)

---

## 💬 NOTAS FINAIS

### Por que essa ordem?

- **Fase 1 primeiro**: Impacto visual imediato mantém motivação
- **Fase 2 depois**: Adiciona profundidade sem quebrar o que funciona
- **Fase 3 por último**: São "nice to have", não essenciais

### O que NÃO fazer

- ❌ Adicionar 50 profissões (foco > quantidade)
- ❌ Fazer sistema de quest complexo (fora do escopo)
- ❌ Criar GUI super elaborado (VS não é Unity)

### O que SIM fazer

- ✅ Manter código limpo e comentado
- ✅ Testar cada feature isoladamente
- ✅ Pedir feedback da comunidade VS cedo
- ✅ Fazer vídeo showcase (marketing = downloads)

---

**Boa sorte, chefe! Se precisar de código específico de alguma fase, é só gritar. 🚀**

---

*Gerado por UEoE v1 - O engenheiro que já viu de tudo*

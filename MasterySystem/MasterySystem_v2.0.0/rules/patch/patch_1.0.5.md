# Patch Notes v1.0.5 - "Mastery Unleashed" ⚔️🌲

## 🌟 Destaques

Esta atualização introduz o **Sistema de Habilidades Ativas** totalmente funcional, refinamento no sistema de cooldowns, e ferramentas robustas de administração e configuração. Agora, ser um Mestre (Nível 3) garante poderes tangíveis e visuais!

---

## ✨ Novas Funcionalidades

### 1. Habilidades Ativas (Tecla [V])

Cada maestria agora possui uma "Ultimate" única que dura **30 segundos** quando ativada.

* **⛏️ Minerador - "Fúria do Subsolo"**
  * **Efeito:** Mineração instantânea (Speed x50) e **Explosão 3x3** ao quebrar pedras/minérios.
  * **Visual:** Partículas de explosão e som de impacto pesado.

* **🪓 Lenhador - "Corte Limpo"**
  * **Efeito:** Corta árvores inteiras instantaneamente em um raio de 10 blocos.
  * **Visual:** Sons de madeira rachando e feedback de toras quebradas.

* **🌾 Agricultor - "Bênção da Natureza"**
  * **Efeito:** Ao andar perto de plantações, elas **crescem instantaneamente** (avança 5 estágios).
  * **Visual:** Partículas verdes mágicas emanando das plantas afetadas.

* **⚔️ Guerreiro - "Investida Brutal"**
  * **Efeito:** Ganha +50% de Velocidade de Movimento e cria uma **Onda de Choque** ao ativar, causando dano e empurrando (Knockback) inimigos próximos.
  * **Visual:** Explosão de partículas vermelhas de combate.

### 2. Sistema de Cooldown Real ⏳

* **Tempo Real:** Os cooldowns agora usam o relógio do sistema (tempo real) e não o tempo do jogo.
* **Ciclo de Uso:**
    1. **Ativação:** Habilidade fica ativa por **30 segundos**.
    2. **Cooldown:** Após acabar o efeito, entra em recarga de **10 minutos**.
* Isso previne o abuso de habilidades ao dormir/avançar o tempo do jogo.

### 3. Configuração do Mod ⚙️

Novo arquivo de configuração `MasteryConfig.json` gerado automaticamente em `ModConfig`.
Permite ajustar:

* `AbilityDurationSeconds`: Tempo de duração da habilidade (Padrão: 30s).
* `AbilityCooldownMinutes`: Tempo de recarga (Padrão: 10m).
* `MiningSpeedMultiplier`: Força da mineração.
* `WarriorDamage`: Dano da investida.

### 4. Novos Comandos de Admin 👮

* `/mastery resetcd [player]`: Reseta imediatamente o cooldown da habilidade do jogador.
* `/mastery setxp [player] [maestria] [valor]`: Define XP e força atualização de nível (aplica buffs/partículas).
* `/mastery reset [player]`: Reseta completamente o progresso do jogador (Master reset).

---

## 🛠️ Correções e Melhorias

* **FIX CRÍTICO:** Resolvido crash ao usar habilidades causado por conflito de versão da biblioteca `protobuf-net`. Agora usa a referência nativa do jogo.
* **FIX:** Corrigido bug onde `setxp` não tocava som de Level Up ou aplicava efeitos visuais.
* **FIX:** Corrigido bug em que o reset de maestria não limpava corretamente os atributos do cliente, exigindo relog.
* **Visual:** Adicionado feedback de partículas para todas as habilidades para melhor "Game Juice".

---

## 📦 Como Instalar/Atualizar

1. Substitua o arquivo `MasteryTitles.dll` na pasta `Mods`.
2. (Opcional) Delete o `MasteryConfig.json` antigo se quiser regenerar os padrões.
3. Reinicie o servidor/jogo.

*Bom jogo e domine o mundo!* 🌍

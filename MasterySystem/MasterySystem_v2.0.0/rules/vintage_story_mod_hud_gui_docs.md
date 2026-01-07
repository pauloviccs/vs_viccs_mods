# Documentação Completa - Criação de Mods HUD/GUI para Vintage Story 1.21.5+

## Índice
1. [Recursos Oficiais](#recursos-oficiais)
2. [Conceitos Fundamentais](#conceitos-fundamentais)
3. [Tipos de GUI/HUD](#tipos-de-guihud)
4. [Classes Base para GUI](#classes-base-para-gui)
5. [Sistema de Bounds e Layout](#sistema-de-bounds-e-layout)
6. [GuiComposer - Construindo Interfaces](#guicomposer---construindo-interfaces)
7. [Tutorial Prático: Criando uma HUD Simples](#tutorial-prático-criando-uma-hud-simples)
8. [Tutorial Avançado: HUD com Scrolling](#tutorial-avançado-hud-com-scrolling)
9. [Exemplos de Código Reais](#exemplos-de-código-reais)
10. [Debug e Ferramentas](#debug-e-ferramentas)
11. [Boas Práticas](#boas-práticas)

---

## Recursos Oficiais

### Documentação Principal
- **Wiki Oficial de Modding**: https://wiki.vintagestory.at/Modding:Getting_Started
- **Documentação de APIs (v1.21.6)**: https://apidocs.vintagestory.at/
- **Documentação GUI Específica**: https://wiki.vintagestory.at/Modding:GUIs
- **Repositório de Exemplos**: https://github.com/anegostudios/vsmodexamples
- **API Source Code**: https://github.com/anegostudios/vsapi

### Mods de Referência (Código Aberto)
- **Status HUD Continued**: https://github.com/Gravydigger/statushud
- **Simple HUD Clock**: Disponível no VS ModDB
- **VSHUD**: Cliente-side HUD com funcionalidades avançadas

---

## Conceitos Fundamentais

### Sistema Cliente/Servidor
O Vintage Story opera em um sistema cliente-servidor onde:
- **Cliente**: Responsável por renderização, inputs e física precisa
- **Servidor**: Gerencia lógica do mundo, entidades e comunicação entre clientes
- **HUD/GUI**: Geralmente implementado no lado do cliente (`EnumAppSide.Client`)

### Estrutura Básica de um Mod
```
MinhaModHUD/
├── modinfo.json
├── MinhaModHUD.dll (compilado)
└── assets/
    └── minhamodhud/
        ├── lang/
        │   └── en.json
        └── textures/
            └── icons/
```

### modinfo.json Exemplo
```json
{
  "type": "code",
  "name": "Minha HUD Personalizada",
  "modid": "minhamodhud",
  "version": "1.0.0",
  "authors": ["Seu Nome"],
  "description": "Uma HUD customizada para Vintage Story",
  "side": "Client",
  "dependencies": {
    "game": "1.21.5"
  }
}
```

---

## Tipos de GUI/HUD

### 1. HudElement
**Uso**: Elementos não-interativos na tela (informações permanentes)
- Não captura o mouse
- Sempre visível
- Exemplos: relógios, coordenadas, barra de status

### 2. GuiDialog
**Uso**: Diálogos interativos (janelas que abrem/fecham)
- Captura foco e mouse
- Pode ser fechado com ESC
- Exemplos: inventário, menus, configurações

### 3. GuiDialogBlockEntity
**Uso**: GUIs vinculadas a entidades de bloco
- Herda de GuiDialog
- Sincronização automática com block entity
- Exemplos: fornalha, moinho de mão

---

## Classes Base para GUI

### HudElement - Para HUDs Não-Interativas

```csharp
using Vintagestory.API.Client;

public class MinhaHud : HudElement
{
    public override string ToggleKeyCombinationCode => null; // Sempre visível
    
    public MinhaHud(ICoreClientAPI capi) : base(capi)
    {
        ComposeHud();
    }
    
    private void ComposeHud()
    {
        // Posição no canto superior esquerdo
        ElementBounds bounds = ElementBounds.Fixed(10, 10, 200, 50);
        
        // Criar composer
        SingleComposer = capi.Gui.CreateCompo("minhahudsimples", bounds)
            .AddStaticText("Minha HUD!", CairoFont.WhiteSmallText(), bounds)
            .Compose();
    }
    
    public override void OnRenderGUI(float deltaTime)
    {
        base.OnRenderGUI(deltaTime);
        // Código de renderização customizado aqui
    }
}
```

### GuiDialog - Para Janelas Interativas

```csharp
public class MinhaJanela : GuiDialog
{
    public override string ToggleKeyCombinationCode => "minhajanela";
    
    public MinhaJanela(ICoreClientAPI capi) : base(capi)
    {
        SetupDialog();
    }
    
    private void SetupDialog()
    {
        // Diálogo centralizado auto-dimensionado
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);
        
        ElementBounds textBounds = ElementBounds.Fixed(0, 40, 300, 100);
        
        // Background que se ajusta aos filhos
        ElementBounds bgBounds = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(textBounds);
        
        SingleComposer = capi.Gui.CreateCompo("minhajanela", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Minha Janela", OnTitleBarClose)
            .AddStaticText("Conteúdo da janela", 
                CairoFont.WhiteDetailText(), textBounds)
            .Compose();
    }
    
    private void OnTitleBarClose()
    {
        TryClose();
    }
}
```

---

## Sistema de Bounds e Layout

### ElementBounds - O Sistema de Coordenadas

ElementBounds define a posição e tamanho dos elementos GUI. Estrutura de caixas:

```
┌─────────────────────────────────┐ Padding Box
│  ┌───────────────────────────┐  │
│  │                           │  │ Content Box
│  │    Conteúdo Real          │  │
│  │                           │  │
│  └───────────────────────────┘  │
└─────────────────────────────────┘
```

### Principais Campos

| Campo | Uso |
|-------|-----|
| `fixedX/fixedY` | Posição fixa em pixels |
| `fixedWidth/fixedHeight` | Tamanho fixo em pixels |
| `fixedPaddingX/fixedPaddingY` | Espaço interno |
| `Alignment` | Alinhamento automático (cantos, bordas) |
| `horizontalSizing/verticalSizing` | Modo de dimensionamento |

### Métodos Úteis de ElementBounds

```csharp
// Criar bounds fixo
ElementBounds bounds = ElementBounds.Fixed(x, y, width, height);

// Criar bounds com padding
ElementBounds bounds = ElementBounds.Fill.WithFixedPadding(10);

// Copiar bounds abaixo do anterior
ElementBounds nextBounds = bounds.BelowCopy(marginTop, marginBottom);

// Copiar bounds à direita do anterior
ElementBounds rightBounds = bounds.RightCopy(marginLeft, marginRight);

// Alinhamento automático
ElementBounds centered = ElementBounds.Fixed(0, 0, 200, 100)
    .WithAlignment(EnumDialogArea.CenterMiddle);

// Dimensionamento automático
ElementBounds auto = ElementBounds.Fill
    .WithSizing(ElementSizing.FitToChildren);
```

### Tipos de Alinhamento (EnumDialogArea)

- `None`: Usa posição fixa
- `LeftTop`, `CenterTop`, `RightTop`
- `LeftMiddle`, `CenterMiddle`, `RightMiddle`
- `LeftBottom`, `CenterBottom`, `RightBottom`
- `LeftFixed`, `CenterFixed`, `RightFixed`
- `FixedTop`, `FixedMiddle`, `FixedBottom`

### Modos de Dimensionamento

- `Fixed`: Tamanho fixo definido manualmente
- `Percentual`: Tamanho em % do pai
- `FitToChildren`: Ajusta para conter todos os filhos

---

## GuiComposer - Construindo Interfaces

O `GuiComposer` é o construtor de GUI. Use encadeamento de métodos para adicionar elementos.

### Criando um Composer

```csharp
GuiComposer composer = capi.Gui.CreateCompo("identificador_unico", bounds);
```

### Elementos de Fundo e Estrutura

```csharp
// Fundo sombreado com borda
.AddShadedDialogBG(bounds, withTitleBar: true)

// Fundo simples
.AddDialogBG(bounds, withTitleBar: true)

// Barra de título com botão fechar
.AddDialogTitleBar("Título", onClose)

// Área com borda recuada
.AddInset(bounds, depth: 3)
```

### Elementos de Texto

```csharp
// Texto estático
.AddStaticText("Meu texto", CairoFont.WhiteSmallText(), bounds)

// Texto dinâmico (atualizável)
.AddDynamicText("Texto inicial", CairoFont.WhiteDetailText(), bounds, "textkey")

// Texto rico (suporta VTML - HTML simplificado)
.AddRichtext("Texto <strong>formatado</strong>", bounds, "richtextkey")

// Texto de hover (tooltip)
.AddHoverText("Texto do tooltip", CairoFont.WhiteSmallText(), 300, bounds)
```

### Controles de Input

```csharp
// Botão
.AddButton("Texto do Botão", OnButtonClick, bounds, "buttonkey")

// Campo de texto
.AddTextInput(bounds, OnTextChanged, CairoFont.WhiteSmallText(), "inputkey")

// Campo numérico
.AddNumberInput(bounds, OnValueChanged, CairoFont.WhiteSmallText(), "numberkey")

// Dropdown
.AddDropDown(values, names, selectedIndex, OnSelectionChanged, bounds, "dropkey")

// Slider
.AddSlider(OnSliderChanged, bounds, "sliderkey")

// Checkbox
.AddSwitch(OnToggle, bounds, "switchkey")
```

### Scrolling e Clipping

```csharp
// Área com scroll vertical
.BeginClip(clipBounds)
    .AddContainer(containerBounds, "scroll-content")
.EndClip()
.AddVerticalScrollbar(OnScroll, scrollbarBounds, "scrollbar")

// Após Compose(), configurar alturas:
composer.GetScrollbar("scrollbar").SetHeights(visibleHeight, totalHeight);
```

### Finalização

```csharp
// SEMPRE finalizar com Compose()
.Compose();
```

---

## Tutorial Prático: Criando uma HUD Simples

### Passo 1: Criar a Classe HUD

```csharp
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace MinhaModHUD
{
    public class SimpleInfoHud : HudElement
    {
        private long lastUpdateMs = 0;
        
        public SimpleInfoHud(ICoreClientAPI capi) : base(capi)
        {
            SetupHud();
        }
        
        private void SetupHud()
        {
            // Posição: 10px da esquerda, 10px do topo
            ElementBounds hudBounds = ElementBounds.Fixed(10, 10, 250, 120);
            
            // Bounds para cada linha de texto
            ElementBounds line1 = ElementBounds.Fixed(5, 5, 240, 25);
            ElementBounds line2 = line1.BelowCopy(0, 5);
            ElementBounds line3 = line2.BelowCopy(0, 5);
            ElementBounds line4 = line3.BelowCopy(0, 5);
            
            SingleComposer = capi.Gui.CreateCompo("simplehudinfo", hudBounds)
                .AddInset(hudBounds.FlatCopy(), 2)
                .AddDynamicText("Posição: --", CairoFont.WhiteSmallText(), 
                    line1, "position")
                .AddDynamicText("Bioma: --", CairoFont.WhiteSmallText(), 
                    line2, "biome")
                .AddDynamicText("Temperatura: --", CairoFont.WhiteSmallText(), 
                    line3, "temperature")
                .AddDynamicText("FPS: --", CairoFont.WhiteSmallText(), 
                    line4, "fps")
                .Compose();
        }
        
        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
            
            // Atualizar a cada 500ms
            if (capi.World.ElapsedMilliseconds - lastUpdateMs > 500)
            {
                UpdateHudText();
                lastUpdateMs = capi.World.ElapsedMilliseconds;
            }
        }
        
        private void UpdateHudText()
        {
            var player = capi.World.Player;
            var pos = player.Entity.Pos;
            
            // Atualizar posição
            string posText = $"Posição: X:{(int)pos.X} Y:{(int)pos.Y} Z:{(int)pos.Z}";
            SingleComposer.GetDynamicText("position").SetNewText(posText);
            
            // Atualizar bioma
            var climate = capi.World.BlockAccessor.GetClimateAt(
                pos.AsBlockPos, EnumGetClimateMode.NowValues);
            string biomeText = $"Temperatura: {climate.Temperature:F1}°C";
            SingleComposer.GetDynamicText("temperature").SetNewText(biomeText);
            
            // Atualizar FPS
            string fpsText = $"FPS: {capi.Render.FrameTime}";
            SingleComposer.GetDynamicText("fps").SetNewText(fpsText);
        }
    }
}
```

### Passo 2: Criar o ModSystem

```csharp
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace MinhaModHUD
{
    public class SimpleHudModSystem : ModSystem
    {
        private ICoreClientAPI capi;
        private SimpleInfoHud hud;
        
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }
        
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            
            // Criar e exibir HUD
            hud = new SimpleInfoHud(capi);
            hud.TryOpen();
            
            // Opcional: registrar hotkey para toggle
            capi.Input.RegisterHotKey("togglesimplehud", 
                "Toggle Simple HUD", 
                GlKeys.H, 
                HotkeyType.GUIOrOtherControls,
                ctrlPressed: true);
                
            capi.Input.SetHotKeyHandler("togglesimplehud", ToggleHud);
        }
        
        private bool ToggleHud(KeyCombination comb)
        {
            if (hud.IsOpened())
                hud.TryClose();
            else
                hud.TryOpen();
                
            return true;
        }
        
        public override void Dispose()
        {
            hud?.Dispose();
            base.Dispose();
        }
    }
}
```

---

## Tutorial Avançado: HUD com Scrolling

```csharp
public class ScrollingListHud : GuiDialog
{
    public override string ToggleKeyCombinationCode => "scrolllisthud";
    
    public ScrollingListHud(ICoreClientAPI capi) : base(capi)
    {
        SetupDialog();
    }
    
    private void SetupDialog()
    {
        int insetWidth = 400;
        int insetHeight = 300;
        int rowHeight = 30;
        int rowCount = 20; // 20 itens na lista
        
        // Bounds do diálogo
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);
        
        // Área de scroll
        ElementBounds insetBounds = ElementBounds.Fixed(
            0, GuiStyle.TitleBarHeight, insetWidth, insetHeight);
        ElementBounds scrollbarBounds = insetBounds.RightCopy().WithFixedWidth(20);
        
        // Área de clipping
        ElementBounds clipBounds = insetBounds.ForkContainingChild(
            GuiStyle.HalfPadding, GuiStyle.HalfPadding, 
            GuiStyle.HalfPadding, GuiStyle.HalfPadding);
            
        // Container para conteúdo scrollável
        ElementBounds containerBounds = insetBounds.ForkContainingChild(
            GuiStyle.HalfPadding, GuiStyle.HalfPadding,
            GuiStyle.HalfPadding, GuiStyle.HalfPadding);
            
        ElementBounds rowBounds = ElementBounds.Fixed(0, 0, insetWidth - 40, rowHeight);
        
        // Background
        ElementBounds bgBounds = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(insetBounds, scrollbarBounds);
        
        // Criar diálogo
        SingleComposer = capi.Gui.CreateCompo("scrolllisthud", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Lista Scrollável", OnClose)
            .BeginChildElements()
                .AddInset(insetBounds, 3)
                .BeginClip(clipBounds)
                    .AddContainer(containerBounds, "scroll-content")
                .EndClip()
                .AddVerticalScrollbar(OnScroll, scrollbarBounds, "scrollbar")
            .EndChildElements();
        
        // Adicionar itens ao container
        GuiElementContainer scrollArea = SingleComposer.GetContainer("scroll-content");
        for (int i = 0; i < rowCount; i++)
        {
            scrollArea.Add(new GuiElementStaticText(
                capi, 
                $"Item {i + 1} da Lista",
                EnumTextOrientation.Left,
                rowBounds,
                CairoFont.WhiteSmallText()
            ));
            rowBounds = rowBounds.BelowCopy();
        }
        
        // Finalizar
        SingleComposer.Compose();
        
        // Configurar scrollbar
        float visibleHeight = (float)clipBounds.fixedHeight;
        float totalHeight = rowHeight * rowCount;
        SingleComposer.GetScrollbar("scrollbar").SetHeights(visibleHeight, totalHeight);
    }
    
    private void OnScroll(float value)
    {
        ElementBounds bounds = SingleComposer.GetContainer("scroll-content").Bounds;
        bounds.fixedY = 5 - value;
        bounds.CalcWorldBounds();
    }
    
    private void OnClose()
    {
        TryClose();
    }
}
```

---

## Exemplos de Código Reais

### Exemplo 1: HUD de Status com Ícones

```csharp
public class StatusIconHud : HudElement
{
    private long lastUpdate = 0;
    
    public StatusIconHud(ICoreClientAPI capi) : base(capi)
    {
        SetupHud();
    }
    
    private void SetupHud()
    {
        // HUD no canto inferior direito
        ElementBounds hudBounds = ElementBounds.Fixed(
            EnumDialogArea.RightBottom, 
            -200, -60, 180, 50);
        
        // Bounds para ícone e texto lado a lado
        ElementBounds iconBounds = ElementBounds.Fixed(5, 5, 32, 32);
        ElementBounds textBounds = ElementBounds.Fixed(42, 12, 130, 20);
        
        SingleComposer = capi.Gui.CreateCompo("statusiconhud", hudBounds)
            .AddInset(hudBounds.FlatCopy(), 2)
            .AddDynamicCustomDraw(iconBounds, OnDrawIcon, "iconarea")
            .AddDynamicText("Status: OK", CairoFont.WhiteSmallText(), 
                textBounds, "statustext")
            .Compose();
    }
    
    private void OnDrawIcon(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        // Desenhar um círculo colorido como ícone
        ctx.SetSourceRGBA(0, 1, 0, 1); // Verde
        ctx.Arc(bounds.InnerWidth / 2, bounds.InnerHeight / 2, 12, 0, Math.PI * 2);
        ctx.Fill();
    }
    
    public override void OnRenderGUI(float deltaTime)
    {
        base.OnRenderGUI(deltaTime);
        
        if (capi.World.ElapsedMilliseconds - lastUpdate > 1000)
        {
            UpdateStatus();
            lastUpdate = capi.World.ElapsedMilliseconds;
        }
    }
    
    private void UpdateStatus()
    {
        var player = capi.World.Player;
        string status = $"HP: {player.Entity.Health:F0}/{player.Entity.MaxHealth:F0}";
        SingleComposer.GetDynamicText("statustext").SetNewText(status);
    }
}
```

### Exemplo 2: Janela de Configuração

```csharp
public class ConfigDialog : GuiDialog
{
    private ModConfig config;
    
    public override string ToggleKeyCombinationCode => "configdialog";
    
    public ConfigDialog(ICoreClientAPI capi, ModConfig config) : base(capi)
    {
        this.config = config;
        SetupDialog();
    }
    
    private void SetupDialog()
    {
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);
        
        // Layout vertical de opções
        ElementBounds option1Bounds = ElementBounds.Fixed(0, 40, 300, 30);
        ElementBounds option2Bounds = option1Bounds.BelowCopy(0, 10);
        ElementBounds option3Bounds = option2Bounds.BelowCopy(0, 10);
        ElementBounds buttonBounds = option3Bounds.BelowCopy(0, 20)
            .WithFixedWidth(100);
        
        ElementBounds bgBounds = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(option1Bounds, option2Bounds, option3Bounds, buttonBounds);
        
        SingleComposer = capi.Gui.CreateCompo("configdialog", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Configurações", OnClose)
            .AddStaticText("Mostrar HUD:", CairoFont.WhiteDetailText(),
                option1Bounds)
            .AddSwitch(OnToggleHud, option1Bounds.RightCopy(150), "showhud")
            .AddStaticText("Tamanho do Texto:", CairoFont.WhiteDetailText(),
                option2Bounds)
            .AddDropDown(
                new string[] { "small", "medium", "large" },
                new string[] { "Pequeno", "Médio", "Grande" },
                config.TextSize,
                OnTextSizeChanged,
                option2Bounds.RightCopy(150),
                "textsize")
            .AddStaticText("Opacidade:", CairoFont.WhiteDetailText(),
                option3Bounds)
            .AddSlider(OnOpacityChanged, option3Bounds.RightCopy(150), "opacity")
            .AddSmallButton("Salvar", OnSave, buttonBounds)
            .Compose();
        
        // Configurar valores iniciais
        SingleComposer.GetSwitch("showhud").On = config.ShowHud;
        SingleComposer.GetSlider("opacity").SetValues(
            config.Opacity, 0, 1, 0.1f);
    }
    
    private void OnToggleHud(bool on)
    {
        config.ShowHud = on;
    }
    
    private void OnTextSizeChanged(string code, bool selected)
    {
        config.TextSize = Array.IndexOf(new[] { "small", "medium", "large" }, code);
    }
    
    private void OnOpacityChanged(float value)
    {
        config.Opacity = value;
    }
    
    private bool OnSave()
    {
        config.Save();
        TryClose();
        return true;
    }
    
    private void OnClose()
    {
        TryClose();
    }
}
```

---

## Debug e Ferramentas

### Modo de Outline de Diálogos

Pressione `Alt + F10` (ou redefina se conflitar) para ciclar entre 3 modos:

1. **Modo 0**: Sem outlines (padrão)
2. **Modo 1**: Outlines de elementos E composers (retângulos brancos em negrito)
3. **Modo 2**: Apenas outlines de elementos

Cores dos outlines:
- Branco: GuiElement genérico
- Amarelo: GuiElementHoverText
- Verde: GuiElementItemSlotGridBase
- Vermelho: GuiElementClip

**Use este modo para visualizar o layout e debug de posicionamento!**

### Logging

```csharp
// No ModSystem
capi.Logger.Debug("Mensagem de debug");
capi.Logger.Warning("Aviso");
capi.Logger.Error("Erro");
capi.Logger.Notification("Notificação");
```

### Verificação de Erros Comuns

```csharp
// Sempre verificar se composer foi criado
if (SingleComposer == null) return;

// Verificar se elemento existe antes de usar
var element = SingleComposer.GetDynamicText("mykey");
if (element != null)
{
    element.SetNewText("Novo texto");
}

// Tratar exceções em callbacks
private bool OnButtonClick()
{
    try
    {
        // Seu código aqui
        return true;
    }
    catch (Exception ex)
    {
        capi.Logger.Error($"Erro no botão: {ex}");
        return false;
    }
}
```

---

## Boas Práticas

### 1. Performance

```csharp
// ❌ RUIM: Atualizar HUD todo frame
public override void OnRenderGUI(float deltaTime)
{
    UpdateComplexCalculation(); // Cálculo pesado
}

// ✅ BOM: Atualizar em intervalos
private long lastUpdate = 0;
public override void OnRenderGUI(float deltaTime)
{
    if (capi.World.ElapsedMilliseconds - lastUpdate > 500)
    {
        UpdateComplexCalculation();
        lastUpdate = capi.World.ElapsedMilliseconds;
    }
}
```

### 2. Gerenciamento de Recursos

```csharp
public override void Dispose()
{
    // Sempre limpar recursos
    SingleComposer?.Dispose();
    base.Dispose();
}
```

### 3. Responsividade

```csharp
// Usar alinhamento automático para diferentes resoluções
ElementBounds bounds = ElementBounds.Fixed(EnumDialogArea.RightTop, 
    -10, 10, 200, 50);
```

### 4. Organização de Código

```csharp
// Separar lógica em métodos claros
private void SetupDialog() { }
private void UpdateHudValues() { }
private void HandleButtonClick() { }
private void SaveConfiguration() { }
```

### 5. Configuração Persistente

```csharp
// Salvar configurações em JSON
public class ModConfig
{
    public bool ShowHud { get; set; } = true;
    public int TextSize { get; set; } = 1;
    public float Opacity { get; set; } = 0.8f;
    
    public void Save()
    {
        string path = Path.Combine(
            GamePaths.DataPath, 
            "ModConfig", 
            "minhahud.json");
            
        File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
    }
    
    public static ModConfig Load()
    {
        string path = Path.Combine(
            GamePaths.DataPath, 
            "ModConfig", 
            "minhahud.json");
            
        if (File.Exists(path))
        {
            return JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(path));
        }
        return new ModConfig();
    }
}
```

### 6. Compatibilidade Multiplayer

```csharp
// HUDs geralmente são client-side only
public override bool ShouldLoad(EnumAppSide forSide)
{
    return forSide == EnumAppSide.Client;
}

// Se precisar sincronizar dados do servidor:
// Use NetworkAPI para comunicação cliente-servidor
capi.Network.RegisterChannel("meucanal")
    .RegisterMessageType<MinhaMensagem>()
    .SetMessageHandler<MinhaMensagem>(OnMessageReceived);
```

### 7. Teclas de Atalho (Hotkeys)

```csharp
// Registrar hotkey no StartClientSide
capi.Input.RegisterHotKey(
    "minhahotkey",           // Código único
    "Descrição da Hotkey",   // Descrição
    GlKeys.H,                // Tecla
    HotkeyType.GUIOrOtherControls,
    ctrlPressed: true        // Requer Ctrl
);

capi.Input.SetHotKeyHandler("minhahotkey", OnHotkeyPressed);

private bool OnHotkeyPressed(KeyCombination comb)
{
    // Sua lógica aqui
    return true; // true = consumir evento
}
```

---

## Referências Rápidas

### Fontes Padrão (CairoFont)

```csharp
CairoFont.WhiteSmallText()      // Pequeno, branco
CairoFont.WhiteDetailText()     // Médio, branco
CairoFont.WhiteMediumText()     // Médio-grande, branco
CairoFont.WhiteSmallishText()   // Entre pequeno e médio
CairoFont.ButtonText()          // Texto de botão
```

### Constantes de Estilo (GuiStyle)

```csharp
GuiStyle.TitleBarHeight         // Altura da barra de título (31)
GuiStyle.ElementToDialogPadding // Padding padrão (10)
GuiStyle.HalfPadding            // Metade do padding (5)
```

### Bounds Pré-definidos (ElementStdBounds)

```csharp
ElementStdBounds.AutosizedMainDialog     // Diálogo auto-dimensionado
ElementStdBounds.MenuButton()            // Botão de menu
ElementStdBounds.Rowed(row, width)       // Linha em grid
ElementStdBounds.SlotGrid(cols, rows)    // Grid de slots
```

### Alinhamentos Comuns

```csharp
// Cantos
EnumDialogArea.LeftTop
EnumDialogArea.RightTop
EnumDialogArea.LeftBottom
EnumDialogArea.RightBottom

// Centros
EnumDialogArea.CenterTop
EnumDialogArea.CenterMiddle
EnumDialogArea.CenterBottom

// Fixos
EnumDialogArea.LeftFixed
EnumDialogArea.RightFixed
EnumDialogArea.CenterFixed
```

---

## Exemplos Completos de Mods

### Mod Completo: HUD de Relógio

```csharp
// ClockHudMod.cs
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ClockHudMod
{
    public class ClockHudModSystem : ModSystem
    {
        private ICoreClientAPI capi;
        private ClockHud clockHud;
        
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }
        
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            
            clockHud = new ClockHud(capi);
            clockHud.TryOpen();
            
            capi.Input.RegisterHotKey("toggleclock", 
                "Toggle Clock HUD", 
                GlKeys.K, 
                HotkeyType.GUIOrOtherControls);
                
            capi.Input.SetHotKeyHandler("toggleclock", ToggleClock);
        }
        
        private bool ToggleClock(KeyCombination comb)
        {
            if (clockHud.IsOpened())
                clockHud.TryClose();
            else
                clockHud.TryOpen();
            return true;
        }
        
        public override void Dispose()
        {
            clockHud?.Dispose();
            base.Dispose();
        }
    }
    
    public class ClockHud : HudElement
    {
        private long lastUpdate = 0;
        
        public ClockHud(ICoreClientAPI capi) : base(capi)
        {
            SetupHud();
        }
        
        private void SetupHud()
        {
            ElementBounds hudBounds = ElementBounds.Fixed(
                EnumDialogArea.RightTop, -210, 10, 200, 80);
            
            ElementBounds timeBounds = ElementBounds.Fixed(10, 10, 180, 30);
            ElementBounds dateBounds = timeBounds.BelowCopy(0, 5);
            
            SingleComposer = capi.Gui.CreateCompo("clockhud", hudBounds)
                .AddShadedDialogBG(hudBounds.FlatCopy(), false)
                .AddDynamicText("--:--:--", CairoFont.WhiteMediumText(), 
                    timeBounds, "time")
                .AddDynamicText("Day -", CairoFont.WhiteSmallText(), 
                    dateBounds, "date")
                .Compose();
        }
        
        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
            
            if (capi.World.ElapsedMilliseconds - lastUpdate > 100)
            {
                UpdateClock();
                lastUpdate = capi.World.ElapsedMilliseconds;
            }
        }
        
        private void UpdateClock()
        {
            var calendar = capi.World.Calendar;
            
            // Tempo do jogo
            double hourOfDay = calendar.HourOfDay;
            int hours = (int)hourOfDay;
            int minutes = (int)((hourOfDay - hours) * 60);
            int seconds = (int)(((hourOfDay - hours) * 60 - minutes) * 60);
            
            string timeText = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            SingleComposer.GetDynamicText("time")?.SetNewText(timeText);
            
            // Data do jogo
            string dateText = $"Day {calendar.TotalDays}, Year {calendar.Year}";
            SingleComposer.GetDynamicText("date")?.SetNewText(dateText);
        }
    }
}
```

---

## Checklist de Desenvolvimento

Ao criar seu mod de HUD/GUI, siga este checklist:

- [ ] Definir se é HudElement (não-interativo) ou GuiDialog (interativo)
- [ ] Criar modinfo.json com versão correta do jogo
- [ ] Implementar ShouldLoad() retornando Client
- [ ] Criar bounds apropriados com alinhamento correto
- [ ] Adicionar elementos ao GuiComposer
- [ ] Finalizar com .Compose()
- [ ] Implementar atualização eficiente (não todo frame)
- [ ] Testar com Outline Mode (Alt+F10)
- [ ] Adicionar hotkey para toggle (se aplicável)
- [ ] Implementar Dispose() corretamente
- [ ] Testar em diferentes resoluções
- [ ] Adicionar logging para debug
- [ ] Documentar configurações do usuário
- [ ] Testar multiplayer (se relevante)

---

## Troubleshooting Comum

### Problema: HUD não aparece

**Solução:**
```csharp
// Certifique-se de chamar TryOpen()
hud.TryOpen();

// Verifique se o mod está carregando
capi.Logger.Debug("Mod carregado!");

// Verifique se SingleComposer foi criado
if (SingleComposer == null)
    capi.Logger.Error("Composer é null!");
```

### Problema: Elementos sobrepostos

**Solução:**
```csharp
// Use BelowCopy() ou RightCopy() corretamente
ElementBounds bounds1 = ElementBounds.Fixed(0, 0, 100, 30);
ElementBounds bounds2 = bounds1.BelowCopy(0, 10); // 10px de margem
ElementBounds bounds3 = bounds2.BelowCopy(0, 10);
```

### Problema: Texto cortado

**Solução:**
```csharp
// Aumentar tamanho do bounds ou usar FitToChildren
ElementBounds textBounds = ElementBounds.Fixed(0, 0, 300, 50); // Mais largura

// Ou auto-ajustar
.AddStaticTextAutoBoxSize(text, font, orientation, bounds)
```

### Problema: Performance ruim

**Solução:**
```csharp
// Limitar taxa de atualização
private long lastUpdate = 0;
private const int UPDATE_INTERVAL_MS = 500;

public override void OnRenderGUI(float deltaTime)
{
    if (capi.World.ElapsedMilliseconds - lastUpdate > UPDATE_INTERVAL_MS)
    {
        // Atualizar aqui
        lastUpdate = capi.World.ElapsedMilliseconds;
    }
}
```

---

## Recursos Adicionais

### Links Importantes

1. **Discord Oficial**: https://discord.gg/vintagestory
   - Canal #modding para suporte
   
2. **Fórum de Modding**: https://www.vintagestory.at/forums/forum/23-modding/

3. **VS ModDB**: https://mods.vintagestory.at/
   - Baixe e estude outros mods

4. **GitHub de Exemplos**: https://github.com/anegostudios/vsmodexamples

5. **API Docs**: https://apidocs.vintagestory.at/

### Mods de Estudo Recomendados

1. **Status HUD** - HUD de informações do jogador
2. **Carry Capacity** - HUD com barra de progresso
3. **Clock** - HUD simples de relógio
4. **Waystones** - GUI complexa com teleporte
5. **Trader** - GUI de comércio avançada

---

## Conclusão

Esta documentação cobre os fundamentos e práticas avançadas para criação de mods HUD/GUI no Vintage Story 1.21.5+. 

**Próximos passos recomendados:**

1. Comece com um HUD simples (exemplo do relógio)
2. Experimente diferentes posicionamentos e alinhamentos
3. Adicione interatividade com GuiDialog
4. Implemente configurações persistentes
5. Publique seu mod no VS ModDB!

**Lembre-se:** Use `Alt + F10` para visualizar bounds durante desenvolvimento e sempre teste em diferentes resoluções de tela.

Boa sorte com seu mod! 🎮
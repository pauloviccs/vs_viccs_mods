# Documentação Completa - MapLayer e Integração com Mapa Vintage Story 1.21.5+

## Índice
1. [Introdução - O Problema e a Solução](#introdução---o-problema-e-a-solução)
2. [Arquitetura do Sistema de Mapa](#arquitetura-do-sistema-de-mapa)
3. [Solução: Acesso via Reflection](#solução-acesso-via-reflection)
4. [Implementação Completa - MapLayer](#implementação-completa---maplayer)
5. [Tutorial Passo a Passo](#tutorial-passo-a-passo)
6. [Exemplos de Código Reais](#exemplos-de-código-reais)
7. [Performance e Otimizações](#performance-e-otimizações)
8. [Troubleshooting](#troubleshooting)
9. [Alternativas e Workarounds](#alternativas-e-workarounds)

---

## Introdução - O Problema e a Solução

### O Problema que Você Está Enfrentando

Você está tentando adicionar camadas de visualização no mapa do mundo (tecla M) para mostrar **land claims** (reivindicações de terreno) do seu mod LandBaron. O problema é que as classes necessárias não estão disponíveis nas DLLs públicas:

- ❌ `MapLayer` - Classe base para layers personalizadas
- ❌ `WorldMapManager` - Gerenciador de registros de layers
- ❌ `GuiElementMap` - Elemento GUI do mapa

Estas classes residem em assemblies internos:
- `Vintagestory.exe` ou `VSSurvivalMod.dll` (survival.dll)
- Parte do mod `VSEssentials` (não API pública)

### A Solução: Reflection + Herança Dinâmica

A solução envolve usar **Reflection** do .NET para acessar os tipos internos e registrar seu próprio MapLayer. Este é o método usado por mods como:
- **Prospector Info** - Overlay de informações de prospecção
- **GiMap** - Mapas temáticos (minérios, temperatura, etc.)
- **Cartographer** - Waypoints compartilhados
- **Medieval Map** - Estilização do mapa

---

## Arquitetura do Sistema de Mapa

### Como o Sistema Funciona

```
WorldMapManager (VSEssentials)
├── Dictionary<string, MapLayer> mapLayers
├── RegisterMapLayer<T>(string layerName) onde T : MapLayer
├── OnRender() → Chama OnRender() de cada layer
└── OnMouseMove() → Chama OnMouseMove() de cada layer

MapLayer (Base Class - VSEssentials)
├── OnRender(GuiElementMap map, float dt)
├── OnMouseMove(MouseEvent e, GuiElementMap map, StringBuilder hoverText)
├── OnMapOpenedClient()
├── OnMapClosedClient()
└── OnLoaded()
```

### Ciclo de Vida de um MapLayer

1. **Registro**: Mod chama `WorldMapManager.RegisterMapLayer<T>("layer_name")`
2. **Inicialização**: `OnLoaded()` é chamado quando o mapa está pronto
3. **Abertura**: `OnMapOpenedClient()` quando usuário abre o mapa (M)
4. **Renderização**: `OnRender()` a cada frame enquanto mapa aberto
5. **Mouse**: `OnMouseMove()` quando mouse se move sobre o mapa
6. **Fechamento**: `OnMapClosedClient()` quando mapa é fechado

---

## Solução: Acesso via Reflection

### Passo 1: Obter o WorldMapManager via Reflection

```csharp
using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

public class LandBaronMapIntegration
{
    private ICoreClientAPI capi;
    private object worldMapManager; // Tipo: Vintagestory.GameContent.WorldMapManager
    private Type mapLayerType; // Tipo: Vintagestory.GameContent.MapLayer
    
    public bool Initialize(ICoreClientAPI api)
    {
        this.capi = api;
        
        try
        {
            // Obter WorldMapManager via ModLoader
            worldMapManager = capi.ModLoader.GetModSystem("Vintagestory.GameContent.WorldMapManager");
            
            if (worldMapManager == null)
            {
                capi.Logger.Error("[LandBaron] WorldMapManager não encontrado!");
                return false;
            }
            
            // Obter o tipo MapLayer do assembly
            var assembly = worldMapManager.GetType().Assembly;
            mapLayerType = assembly.GetType("Vintagestory.GameContent.MapLayer");
            
            if (mapLayerType == null)
            {
                capi.Logger.Error("[LandBaron] Tipo MapLayer não encontrado!");
                return false;
            }
            
            capi.Logger.Notification("[LandBaron] Map integration initialized successfully!");
            return true;
        }
        catch (Exception ex)
        {
            capi.Logger.Error($"[LandBaron] Erro ao inicializar integração com mapa: {ex}");
            return false;
        }
    }
}
```

### Passo 2: Criar Sua Classe MapLayer Personalizada

**IMPORTANTE**: Sua classe NÃO pode herdar diretamente de `MapLayer` pois não temos acesso em tempo de compilação. Precisamos criar via herança em runtime ou usar uma abordagem de composição.

#### Abordagem Recomendada: Classe Wrapper

```csharp
public class LandClaimMapLayerWrapper
{
    private ICoreClientAPI capi;
    private LandBaronSystem landSystem;
    private object actualMapLayer; // A instância real criada via reflection
    
    public LandClaimMapLayerWrapper(ICoreClientAPI api, LandBaronSystem system)
    {
        this.capi = api;
        this.landSystem = system;
    }
    
    // Estes métodos serão chamados via delegates/events
    public void OnRender(object mapElement, float deltaTime)
    {
        try
        {
            // Obter o GuiElementMap via reflection
            Type mapType = mapElement.GetType();
            
            // Obter viewport do mapa (área visível)
            var boundsProperty = mapType.GetProperty("Bounds");
            var bounds = boundsProperty?.GetValue(mapElement);
            
            // Renderizar claims
            RenderLandClaims(mapElement, bounds);
        }
        catch (Exception ex)
        {
            capi.Logger.Error($"[LandBaron] Erro ao renderizar claims no mapa: {ex}");
        }
    }
    
    private void RenderLandClaims(object mapElement, object bounds)
    {
        // Obter todos os claims do sistema
        var claims = landSystem.GetAllClaims();
        
        // Para cada claim, desenhar um retângulo no mapa
        foreach (var claim in claims)
        {
            DrawClaimOnMap(mapElement, claim);
        }
    }
    
    private void DrawClaimOnMap(object mapElement, LandClaim claim)
    {
        // Implementação de desenho via Cairo ou API de renderização
        // Veja seção "Renderização de Claims" abaixo
    }
    
    public void OnMouseMove(object mouseEvent, object mapElement, object hoverText)
    {
        try
        {
            // Obter posição do mouse no mundo
            var worldPos = GetMouseWorldPosition(mouseEvent, mapElement);
            
            // Verificar se está sobre um claim
            var claim = landSystem.GetClaimAtPosition(worldPos);
            
            if (claim != null)
            {
                // Adicionar informações ao tooltip
                StringBuilder sb = hoverText as StringBuilder;
                if (sb != null)
                {
                    sb.AppendLine($"Land Claim: {claim.OwnerName}");
                    if (claim.ForSale)
                    {
                        sb.AppendLine($"For Sale: {claim.Price} gears");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            capi.Logger.Error($"[LandBaron] Erro no mouse move: {ex}");
        }
    }
    
    private Vec3d GetMouseWorldPosition(object mouseEvent, object mapElement)
    {
        // Usar reflection para obter a posição do mouse em coordenadas do mundo
        Type mapType = mapElement.GetType();
        var method = mapType.GetMethod("ScreenPosToWorldPos");
        
        // Obter X,Y do mouse event
        Type mouseType = mouseEvent.GetType();
        int mouseX = (int)mouseType.GetProperty("X")?.GetValue(mouseEvent);
        int mouseY = (int)mouseType.GetProperty("Y")?.GetValue(mouseEvent);
        
        // Converter para coordenadas do mundo
        var worldPos = method?.Invoke(mapElement, new object[] { mouseX, mouseY });
        return worldPos as Vec3d ?? new Vec3d();
    }
}
```

### Passo 3: Registrar o MapLayer via Reflection

```csharp
public bool RegisterMapLayer()
{
    try
    {
        // Criar o wrapper
        var layerWrapper = new LandClaimMapLayerWrapper(capi, landSystem);
        
        // Obter o método RegisterMapLayer via reflection
        Type wmType = worldMapManager.GetType();
        var registerMethod = wmType.GetMethod("RegisterMapLayer", 
            BindingFlags.Public | BindingFlags.Instance);
        
        if (registerMethod == null)
        {
            capi.Logger.Error("[LandBaron] RegisterMapLayer method not found!");
            return false;
        }
        
        // Criar instância da layer dinamicamente
        // Precisamos criar uma classe que herda de MapLayer em runtime
        var layerInstance = CreateDynamicMapLayer(layerWrapper);
        
        if (layerInstance == null)
        {
            capi.Logger.Error("[LandBaron] Failed to create map layer instance!");
            return false;
        }
        
        // Registrar a layer
        registerMethod.Invoke(worldMapManager, new object[] { "landclaims", layerInstance });
        
        capi.Logger.Notification("[LandBaron] Map layer registered successfully!");
        return true;
    }
    catch (Exception ex)
    {
        capi.Logger.Error($"[LandBaron] Failed to register map layer: {ex}");
        return false;
    }
}
```

### Passo 4: Criar MapLayer Dinâmica (Avançado)

Esta é a parte mais complexa - criar uma classe que herda de MapLayer em runtime:

```csharp
using System.Reflection.Emit;

private object CreateDynamicMapLayer(LandClaimMapLayerWrapper wrapper)
{
    try
    {
        // Criar um tipo dinâmico que herda de MapLayer
        var assemblyName = new AssemblyName("LandBaronDynamicMapLayer");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        
        var typeBuilder = moduleBuilder.DefineType(
            "LandClaimMapLayer",
            TypeAttributes.Public | TypeAttributes.Class,
            mapLayerType); // Herdar de MapLayer
        
        // Adicionar campo para o wrapper
        var wrapperField = typeBuilder.DefineField(
            "_wrapper",
            typeof(LandClaimMapLayerWrapper),
            FieldAttributes.Private);
        
        // Criar construtor
        CreateConstructor(typeBuilder, wrapperField);
        
        // Override OnRender
        OverrideOnRender(typeBuilder, wrapperField);
        
        // Override OnMouseMove
        OverrideOnMouseMove(typeBuilder, wrapperField);
        
        // Criar o tipo
        Type dynamicType = typeBuilder.CreateType();
        
        // Instanciar
        return Activator.CreateInstance(dynamicType, new object[] { wrapper });
    }
    catch (Exception ex)
    {
        capi.Logger.Error($"[LandBaron] Error creating dynamic map layer: {ex}");
        return null;
    }
}

private void CreateConstructor(TypeBuilder typeBuilder, FieldBuilder wrapperField)
{
    var constructor = typeBuilder.DefineConstructor(
        MethodAttributes.Public,
        CallingConventions.Standard,
        new Type[] { typeof(LandClaimMapLayerWrapper) });
    
    var il = constructor.GetILGenerator();
    
    // Chamar construtor base
    var baseConstructor = mapLayerType.GetConstructor(Type.EmptyTypes);
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Call, baseConstructor);
    
    // Armazenar wrapper
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Ldarg_1);
    il.Emit(OpCodes.Stfld, wrapperField);
    il.Emit(OpCodes.Ret);
}

private void OverrideOnRender(TypeBuilder typeBuilder, FieldBuilder wrapperField)
{
    // Encontrar método OnRender na classe base
    var baseMethod = mapLayerType.GetMethod("OnRender", 
        BindingFlags.Public | BindingFlags.Instance);
    
    if (baseMethod == null) return;
    
    var paramTypes = baseMethod.GetParameters().Select(p => p.ParameterType).ToArray();
    
    var method = typeBuilder.DefineMethod(
        "OnRender",
        MethodAttributes.Public | MethodAttributes.Virtual,
        baseMethod.ReturnType,
        paramTypes);
    
    var il = method.GetILGenerator();
    
    // Chamar wrapper.OnRender
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Ldfld, wrapperField);
    il.Emit(OpCodes.Ldarg_1); // mapElement parameter
    il.Emit(OpCodes.Ldarg_2); // deltaTime parameter
    
    var wrapperMethod = typeof(LandClaimMapLayerWrapper).GetMethod("OnRender");
    il.Emit(OpCodes.Callvirt, wrapperMethod);
    il.Emit(OpCodes.Ret);
    
    typeBuilder.DefineMethodOverride(method, baseMethod);
}

private void OverrideOnMouseMove(TypeBuilder typeBuilder, FieldBuilder wrapperField)
{
    var baseMethod = mapLayerType.GetMethod("OnMouseMove",
        BindingFlags.Public | BindingFlags.Instance);
    
    if (baseMethod == null) return;
    
    var paramTypes = baseMethod.GetParameters().Select(p => p.ParameterType).ToArray();
    
    var method = typeBuilder.DefineMethod(
        "OnMouseMove",
        MethodAttributes.Public | MethodAttributes.Virtual,
        baseMethod.ReturnType,
        paramTypes);
    
    var il = method.GetILGenerator();
    
    // Chamar wrapper.OnMouseMove
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Ldfld, wrapperField);
    il.Emit(OpCodes.Ldarg_1); // mouseEvent
    il.Emit(OpCodes.Ldarg_2); // mapElement
    il.Emit(OpCodes.Ldarg_3); // hoverText
    
    var wrapperMethod = typeof(LandClaimMapLayerWrapper).GetMethod("OnMouseMove");
    il.Emit(OpCodes.Callvirt, wrapperMethod);
    il.Emit(OpCodes.Ret);
    
    typeBuilder.DefineMethodOverride(method, baseMethod);
}
```

---

## Implementação Completa - MapLayer

### Renderização de Claims no Mapa

```csharp
private void RenderLandClaims(object mapElement, object bounds)
{
    // Obter contexto de renderização Cairo
    Type mapType = mapElement.GetType();
    var getSurfaceMethod = mapType.GetMethod("get_Surface");
    var surface = getSurfaceMethod?.Invoke(mapElement, null);
    
    if (surface == null) return;
    
    // Criar contexto Cairo
    using (var ctx = new Context(surface as ImageSurface))
    {
        var claims = landSystem.GetAllClaims();
        
        foreach (var claim in claims)
        {
            DrawClaim(ctx, mapElement, claim);
        }
    }
}

private void DrawClaim(Context ctx, object mapElement, LandClaim claim)
{
    // Converter coordenadas do mundo para coordenadas da tela do mapa
    var screenPos = WorldPosToScreenPos(mapElement, claim.StartPos);
    var screenEnd = WorldPosToScreenPos(mapElement, claim.EndPos);
    
    if (screenPos == null || screenEnd == null) return;
    
    double x = screenPos.X;
    double y = screenPos.Y;
    double width = screenEnd.X - screenPos.X;
    double height = screenEnd.Y - screenPos.Y;
    
    // Definir cor baseada no tipo de claim
    double r, g, b, a;
    if (claim.OwnerId == capi.World.Player.PlayerUID)
    {
        // Próprio claim - Verde
        r = 0.0; g = 1.0; b = 0.0; a = 0.3;
    }
    else if (claim.ForSale)
    {
        // À venda - Amarelo
        r = 1.0; g = 1.0; b = 0.0; a = 0.3;
    }
    else
    {
        // Claim de outro jogador - Vermelho
        r = 1.0; g = 0.0; b = 0.0; a = 0.3;
    }
    
    // Desenhar retângulo preenchido
    ctx.SetSourceRGBA(r, g, b, a);
    ctx.Rectangle(x, y, width, height);
    ctx.Fill();
    
    // Desenhar borda
    ctx.SetSourceRGBA(r, g, b, 0.8);
    ctx.LineWidth = 2.0;
    ctx.Rectangle(x, y, width, height);
    ctx.Stroke();
}

private Vec2d WorldPosToScreenPos(object mapElement, BlockPos worldPos)
{
    try
    {
        Type mapType = mapElement.GetType();
        var method = mapType.GetMethod("WorldPosToScreenPos");
        
        if (method == null) return null;
        
        // Criar Vec3d para a posição do mundo
        var vec3d = new Vec3d(worldPos.X, worldPos.Y, worldPos.Z);
        
        var result = method.Invoke(mapElement, new object[] { vec3d });
        return result as Vec2d;
    }
    catch
    {
        return null;
    }
}
```

---

## Tutorial Passo a Passo

### Passo 1: Configurar Projeto com Reflection

Adicione ao seu `.csproj`:

```xml
<PropertyGroup>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>

<ItemGroup>
    <Reference Include="VintagestoryAPI">
        <HintPath>$(VINTAGE_STORY)/VintagestoryAPI.dll</HintPath>
        <Private>false</Private>
    </Reference>
    <!-- NÃO adicionar VintagestoryLib.dll - vamos acessar via reflection -->
</ItemGroup>
```

### Passo 2: Criar Sistema de Integração

Crie `LandBaronMapIntegration.cs`:

```csharp
using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace LandBaron
{
    public class LandBaronMapIntegration
    {
        private ICoreClientAPI capi;
        private LandBaronSystem landSystem;
        private object worldMapManager;
        private Type mapLayerType;
        private LandClaimMapLayerWrapper layerWrapper;
        
        public LandBaronMapIntegration(ICoreClientAPI api, LandBaronSystem system)
        {
            this.capi = api;
            this.landSystem = system;
        }
        
        public bool Initialize()
        {
            try
            {
                // 1. Obter WorldMapManager
                worldMapManager = capi.ModLoader.GetModSystem("Vintagestory.GameContent.WorldMapManager");
                if (worldMapManager == null)
                {
                    capi.Logger.Warning("[LandBaron] WorldMapManager not found - map integration disabled");
                    return false;
                }
                
                // 2. Obter tipo MapLayer
                var assembly = worldMapManager.GetType().Assembly;
                mapLayerType = assembly.GetType("Vintagestory.GameContent.MapLayer");
                if (mapLayerType == null)
                {
                    capi.Logger.Warning("[LandBaron] MapLayer type not found - map integration disabled");
                    return false;
                }
                
                // 3. Criar wrapper
                layerWrapper = new LandClaimMapLayerWrapper(capi, landSystem);
                
                // 4. Registrar layer
                return RegisterMapLayer();
            }
            catch (Exception ex)
            {
                capi.Logger.Warning($"[LandBaron] Map integration failed: {ex.Message}");
                return false;
            }
        }
        
        private bool RegisterMapLayer()
        {
            try
            {
                // Criar instância dinâmica da layer
                var layerInstance = CreateDynamicMapLayer(layerWrapper);
                if (layerInstance == null) return false;
                
                // Registrar via reflection
                var registerMethod = worldMapManager.GetType().GetMethod(
                    "RegisterMapLayer",
                    BindingFlags.Public | BindingFlags.Instance);
                
                if (registerMethod == null)
                {
                    capi.Logger.Warning("[LandBaron] RegisterMapLayer method not found");
                    return false;
                }
                
                registerMethod.Invoke(worldMapManager, new object[] { "landclaims", layerInstance });
                
                capi.Logger.Notification("[LandBaron] Map layer registered successfully!");
                return true;
            }
            catch (Exception ex)
            {
                capi.Logger.Error($"[LandBaron] Failed to register map layer: {ex}");
                return false;
            }
        }
        
        // Incluir métodos CreateDynamicMapLayer, CreateConstructor, etc. aqui
        // (Código da seção anterior)
    }
}
```

### Passo 3: Integrar com Seu ModSystem

Em `LandBaronSystem.cs`:

```csharp
public class LandBaronSystem : ModSystem
{
    private ICoreClientAPI capi;
    private LandBaronMapIntegration mapIntegration;
    
    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        capi = api;
        
        // Tentar inicializar integração com mapa
        mapIntegration = new LandBaronMapIntegration(api, this);
        
        // Aguardar o mundo estar pronto
        api.Event.BlockTexturesLoaded += () =>
        {
            if (mapIntegration.Initialize())
            {
                api.Logger.Notification("[LandBaron] Map integration enabled");
            }
            else
            {
                api.Logger.Warning("[LandBaron] Map integration disabled - mod will work without map features");
            }
        };
    }
}
```

---

## Exemplos de Código Reais

### Exemplo 1: Prospector Info Mod

Este mod adiciona overlay de informações de prospecção no mapa:

```csharp
// Fonte: https://github.com/p3t3rix-vsmods/VsProspectorInfo

public class ProspectorInfoModSystem : ModSystem
{
    private const string MapLayerName = "prospectorInfo";
    
    public override void StartClientSide(ICoreClientAPI api)
    {
        var mapManager = api.ModLoader.GetModSystem<WorldMapManager>();
        mapManager.RegisterMapLayer<ProspectorOverlayLayer>(MapLayerName);
    }
}

// NOTA: Este código usa RegisterMapLayer<T>() com tipo genérico
// Funciona porque ProspectorOverlayLayer herda de MapLayer
// No seu caso, você precisa usar reflection pois não tem acesso direto ao MapLayer
```

### Exemplo 2: GiMap - Mapas Temáticos

GiMap adiciona várias camadas de mapa (minérios, temperatura, etc.):

```csharp
// Estrutura aproximada baseada nos stack traces

public class GiMapModSystem : ModSystem
{
    public override void StartClientSide(ICoreClientAPI api)
    {
        var mapManager = api.ModLoader.GetModSystem<WorldMapManager>();
        
        // Registrar múltiplas layers
        mapManager.RegisterMapLayer<OreMapLayer>("oremap");
        mapManager.RegisterMapLayer<TopographicMapLayer>("topomap");
        mapManager.RegisterMapLayer<TemperatureMapLayer>("tempmap");
    }
}

// Cada layer implementa OnRender e desenha sua informação específica
public class OreMapLayer : MapLayer
{
    public override void OnRender(GuiElementMap map, float dt)
    {
        // Itera chunks visíveis
        // Gera imagem colorida baseada em dados de minérios
        // Usa Cairo Context para desenhar
    }
}
```

---

## Performance e Otimizações

### 1. Viewport Culling (CRÍTICO)

Apenas renderize claims que estão visíveis no viewport atual:

```csharp
private List<LandClaim> GetVisibleClaims(object mapElement)
{
    // Obter bounds visíveis do mapa
    var viewport = GetMapViewport(mapElement);
    if (viewport == null) return new List<LandClaim>();
    
    // Filtrar apenas claims que intersectam o viewport
    return landSystem.GetAllClaims()
        .Where(claim => ClaimIntersectsViewport(claim, viewport))
        .ToList();
}

private bool ClaimIntersectsViewport(LandClaim claim, ViewportBounds viewport)
{
    // Verificar interseção de retângulos
    return !(claim.EndPos.X < viewport.MinX ||
             claim.StartPos.X > viewport.MaxX ||
             claim.EndPos.Z < viewport.MinZ ||
             claim.StartPos.Z > viewport.MaxZ);
}
```

### 2. Cache de Renderização

Não redesenhe tudo a cada frame:

```csharp
private Dictionary<long, ImageSurface> claimCache = new Dictionary<long, ImageSurface>();
private long lastCacheUpdate = 0;

public void OnRender(object mapElement, float deltaTime)
{
    long now = capi.World.ElapsedMilliseconds;
    
    // Atualizar cache a cada 500ms
    if (now - lastCacheUpdate > 500)
    {
        UpdateClaimCache(mapElement);
        lastCacheUpdate = now;
    }
    
    // Renderizar do cache
    RenderFromCache();
}
```

### 3. Level of Detail (LOD)

Simplifique renderização baseado no zoom:

```csharp
private void DrawClaim(Context ctx, LandClaim claim, float zoomLevel)
{
    if (zoomLevel < 0.5)
    {
        // Muito zoom out - apenas pintar pixel
        DrawPixel(ctx, claim);
    }
    else if (zoomLevel < 2.0)
    {
        // Zoom médio - retângulo simples
        DrawRectangle(ctx, claim);
    }
    else
    {
        // Zoom in - detalhes completos
        DrawDetailedClaim(ctx, claim);
    }
}
```

---

## Troubleshooting

### Problema 1: "WorldMapManager não encontrado"

**Causa**: O mod VSEssentials não está carregado ou versão incompatível.

**Solução**:
```csharp
// Adicionar verificação de versão do jogo
if (capi.World.Api.GetVersion() < new GameVersion(1, 21, 5))
{
    capi.Logger.Error("[LandBaron] Game version too old for map integration");
    return false;
}

// Verificar se VSEssentials está presente
var essentialsMod = capi.ModLoader.GetMod("game");
if (essentialsMod == null)
{
    capi.Logger.Error("[LandBaron] VSEssentials mod not found!");
    return false;
}
```

### Problema 2: "MapLayer type not found"

**Causa**: Nome do tipo ou assembly incorreto.

**Solução**:
```csharp
// Tentar ambos os nomes possíveis
var assembly = worldMapManager.GetType().Assembly;
mapLayerType = assembly.GetType("Vintagestory.GameContent.MapLayer");

if (mapLayerType == null)
{
    // Tentar namespace alternativo
    mapLayerType = assembly.GetType("Vintagestory.GameContent.Systems.WorldMap.MapLayer");
}

// Listar todos os tipos do assembly para debug
if (mapLayerType == null)
{
    capi.Logger.Debug("[LandBaron] Available types in assembly:");
    foreach (var type in assembly.GetTypes())
    {
        if (type.Name.Contains("Map"))
        {
            capi.Logger.Debug($"  - {type.FullName}");
        }
    }
}
```

### Problema 3: Performance ruim ao renderizar

**Solução**: Implementar todas as otimizações mencionadas:

```csharp
// Checklist de performance
✓ Viewport culling ativo
✓ Cache de renderização implementado
✓ LOD baseado em zoom
✓ Atualização limitada (não todo frame)
✓ Evitar alocações desnecessárias
✓ Usar Cairo Context de forma eficiente
```

### Problema 4: Crashes com NullReferenceException

**Causa**: Métodos obtidos via reflection retornando null.

**Solução**:
```csharp
// SEMPRE verificar null antes de usar
var method = mapType.GetMethod("MethodName");
if (method == null)
{
    capi.Logger.Warning($"[LandBaron] Method 'MethodName' not found");
    return;
}

var result = method.Invoke(instance, parameters);
if (result == null)
{
    capi.Logger.Warning($"[LandBaron] Method returned null");
    return;
}

// Uso seguro depois das verificações
ProcessResult(result);
```

---

## Alternativas e Workarounds

### Alternativa 1: HUD Overlay ao Invés de MapLayer

Se a integração com mapa for muito complexa, considere criar um HUD overlay quando o mapa estiver aberto:

```csharp
public class LandClaimMapOverlay : HudElement
{
    private LandBaronSystem landSystem;
    
    public LandClaimMapOverlay(ICoreClientAPI capi, LandBaronSystem system) : base(capi)
    {
        this.landSystem = system;
        SetupOverlay();
    }
    
    private void SetupOverlay()
    {
        // Criar HUD que cobre toda a tela
        ElementBounds bounds = ElementBounds.Fixed(EnumDialogArea.None, 0, 0, 
            capi.Render.FrameWidth, capi.Render.FrameHeight);
        
        SingleComposer = capi.Gui.CreateCompo("landclaimoverlay", bounds)
            .AddDynamicCustomDraw(bounds, OnDrawOverlay, "overlay")
            .Compose();
    }
    
    private void OnDrawOverlay(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        // Verificar se mapa está aberto
        if (!IsMapOpen()) return;
        
        // Desenhar claims sobre o mapa
        var claims = landSystem.GetAllClaims();
        foreach (var claim in claims)
        {
            DrawClaimOverlay(ctx, claim);
        }
    }
    
    private bool IsMapOpen()
    {
        // Detectar se diálogo do mapa está aberto
        return capi.Gui.OpenedGuis.Any(g => 
            g.GetType().Name.Contains("WorldMap") || 
            g.ToggleKeyCombinationCode == "worldmap");
    }
}

// No ModSystem
public override void StartClientSide(ICoreClientAPI api)
{
    var overlay = new LandClaimMapOverlay(api, this);
    overlay.TryOpen();
    
    // Registrar hotkey do mapa para sincronizar
    api.Input.RegisterHotKey("worldmap", "Open World Map", GlKeys.M);
}
```

**Vantagens**:
- Não depende de reflection
- Controle total sobre renderização
- Funciona independente de versão

**Desvantagens**:
- Não integra "nativamente" com o mapa
- Coordenadas precisam ser calculadas manualmente
- Não usa o sistema de zoom do mapa

### Alternativa 2: Comando de Chat com Visualização

Criar um comando que mostra claims próximos:

```csharp
public override void StartClientSide(ICoreClientAPI api)
{
    api.ChatCommands.Create("showclaims")
        .WithDescription("Show nearby land claims")
        .HandleWith((args) =>
        {
            var player = api.World.Player;
            var pos = player.Entity.Pos.AsBlockPos;
            
            var nearbyClaims = landSystem.GetClaimsNearPosition(pos, radius: 500);
            
            api.ShowChatMessage($"Found {nearbyClaims.Count} claims nearby:");
            
            foreach (var claim in nearbyClaims)
            {
                var distance = pos.DistanceTo(claim.CenterPos);
                var direction = GetDirection(pos, claim.CenterPos);
                
                api.ShowChatMessage(
                    $"- {claim.OwnerName}: {(int)distance}m {direction}" +
                    (claim.ForSale ? $" (For Sale: {claim.Price})" : ""));
            }
            
            return TextCommandResult.Success();
        });
}

private string GetDirection(BlockPos from, BlockPos to)
{
    var dx = to.X - from.X;
    var dz = to.Z - from.Z;
    var angle = Math.Atan2(dz, dx) * 180 / Math.PI;
    
    if (angle < -157.5 || angle > 157.5) return "West";
    if (angle > -22.5 && angle < 22.5) return "East";
    if (angle > 67.5 && angle < 112.5) return "South";
    if (angle > -112.5 && angle < -67.5) return "North";
    if (angle > 22.5 && angle < 67.5) return "Southeast";
    if (angle > 112.5 && angle < 157.5) return "Southwest";
    if (angle > -67.5 && angle < -22.5) return "Northeast";
    return "Northwest";
}
```

### Alternativa 3: Waypoints Automaticos

Use o sistema de waypoints existente para marcar claims:

```csharp
public void AddClaimWaypoint(LandClaim claim)
{
    var color = claim.OwnerId == capi.World.Player.PlayerUID 
        ? ColorUtil.ColorFromRgba(0, 255, 0, 128) // Verde para próprio
        : ColorUtil.ColorFromRgba(255, 0, 0, 128); // Vermelho para outros
    
    var icon = claim.ForSale ? "sale" : "claim";
    var title = claim.ForSale 
        ? $"[SALE] {claim.OwnerName}: {claim.Price}" 
        : $"{claim.OwnerName}'s Land";
    
    capi.World.Player.WorldData.Waypoints.Add(new Waypoint
    {
        Position = claim.CenterPos.ToVec3d(),
        Color = color,
        Icon = icon,
        Title = title,
        Pinned = false,
        OwningPlayerUid = capi.World.Player.PlayerUID
    });
}

// Atualizar waypoints quando claims mudarem
public override void StartClientSide(ICoreClientAPI api)
{
    api.Event.RegisterGameTickListener((dt) =>
    {
        UpdateClaimWaypoints();
    }, 5000); // Atualizar a cada 5 segundos
}
```

---

## Código Completo de Referência

### LandBaronMapIntegration.cs (Versão Completa)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LandBaron.Integration
{
    public class LandBaronMapIntegration
    {
        private ICoreClientAPI capi;
        private LandBaronSystem landSystem;
        private object worldMapManager;
        private Type mapLayerType;
        private LandClaimMapLayerWrapper layerWrapper;
        private bool initialized = false;
        
        public bool IsInitialized => initialized;
        
        public LandBaronMapIntegration(ICoreClientAPI api, LandBaronSystem system)
        {
            this.capi = api;
            this.landSystem = system;
        }
        
        public bool Initialize()
        {
            try
            {
                capi.Logger.Notification("[LandBaron] Initializing map integration...");
                
                // 1. Obter WorldMapManager
                worldMapManager = capi.ModLoader.GetModSystem("Vintagestory.GameContent.WorldMapManager");
                if (worldMapManager == null)
                {
                    capi.Logger.Warning("[LandBaron] WorldMapManager not found");
                    return false;
                }
                
                // 2. Obter tipo MapLayer
                var assembly = worldMapManager.GetType().Assembly;
                mapLayerType = assembly.GetType("Vintagestory.GameContent.MapLayer");
                
                if (mapLayerType == null)
                {
                    // Tentar namespace alternativo
                    mapLayerType = assembly.GetType("Vintagestory.GameContent.Systems.WorldMap.MapLayer");
                }
                
                if (mapLayerType == null)
                {
                    capi.Logger.Warning("[LandBaron] MapLayer type not found");
                    LogAvailableTypes(assembly);
                    return false;
                }
                
                // 3. Criar wrapper
                layerWrapper = new LandClaimMapLayerWrapper(capi, landSystem);
                
                // 4. Registrar layer
                if (RegisterMapLayer())
                {
                    initialized = true;
                    capi.Logger.Notification("[LandBaron] Map integration initialized successfully!");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                capi.Logger.Error($"[LandBaron] Failed to initialize map integration: {ex}");
                return false;
            }
        }
        
        private void LogAvailableTypes(Assembly assembly)
        {
            capi.Logger.Debug("[LandBaron] Available types containing 'Map':");
            foreach (var type in assembly.GetTypes().Where(t => t.Name.Contains("Map")))
            {
                capi.Logger.Debug($"  - {type.FullName}");
            }
        }
        
        private bool RegisterMapLayer()
        {
            try
            {
                // Criar instância dinâmica
                var layerInstance = CreateDynamicMapLayer();
                if (layerInstance == null)
                {
                    capi.Logger.Error("[LandBaron] Failed to create dynamic map layer");
                    return false;
                }
                
                // Obter método RegisterMapLayer
                var registerMethod = worldMapManager.GetType().GetMethod(
                    "RegisterMapLayer",
                    BindingFlags.Public | BindingFlags.Instance);
                
                if (registerMethod == null)
                {
                    capi.Logger.Error("[LandBaron] RegisterMapLayer method not found");
                    return false;
                }
                
                // Registrar
                registerMethod.Invoke(worldMapManager, new object[] { "landclaims", layerInstance });
                
                capi.Logger.Notification("[LandBaron] Map layer 'landclaims' registered!");
                return true;
            }
            catch (Exception ex)
            {
                capi.Logger.Error($"[LandBaron] Failed to register map layer: {ex}");
                return false;
            }
        }
        
        private object CreateDynamicMapLayer()
        {
            try
            {
                // Criar assembly dinâmico
                var assemblyName = new AssemblyName("LandBaronDynamicMapLayer");
                var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                    assemblyName, AssemblyBuilderAccess.Run);
                var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
                
                // Criar tipo que herda de MapLayer
                var typeBuilder = moduleBuilder.DefineType(
                    "DynamicLandClaimMapLayer",
                    TypeAttributes.Public | TypeAttributes.Class,
                    mapLayerType);
                
                // Campo para wrapper
                var wrapperField = typeBuilder.DefineField(
                    "_wrapper",
                    typeof(LandClaimMapLayerWrapper),
                    FieldAttributes.Private);
                
                // Criar construtor
                BuildConstructor(typeBuilder, wrapperField);
                
                // Override OnRender
                BuildOnRenderOverride(typeBuilder, wrapperField);
                
                // Override OnMouseMove
                BuildOnMouseMoveOverride(typeBuilder, wrapperField);
                
                // Criar tipo
                Type dynamicType = typeBuilder.CreateType();
                
                // Instanciar com wrapper
                return Activator.CreateInstance(dynamicType, new object[] { layerWrapper });
            }
            catch (Exception ex)
            {
                capi.Logger.Error($"[LandBaron] Error creating dynamic type: {ex}");
                return null;
            }
        }
        
        private void BuildConstructor(TypeBuilder typeBuilder, FieldBuilder wrapperField)
        {
            var constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new Type[] { typeof(LandClaimMapLayerWrapper) });
            
            var il = constructor.GetILGenerator();
            
            // Chamar base()
            var baseConstructor = mapLayerType.GetConstructor(Type.EmptyTypes);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseConstructor);
            
            // this._wrapper = wrapper
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, wrapperField);
            
            il.Emit(OpCodes.Ret);
        }
        
        private void BuildOnRenderOverride(TypeBuilder typeBuilder, FieldBuilder wrapperField)
        {
            var baseMethod = mapLayerType.GetMethod("OnRender",
                BindingFlags.Public | BindingFlags.Instance);
            
            if (baseMethod == null)
            {
                capi.Logger.Warning("[LandBaron] OnRender method not found in MapLayer");
                return;
            }
            
            var paramTypes = baseMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            
            var method = typeBuilder.DefineMethod(
                "OnRender",
                MethodAttributes.Public | MethodAttributes.Virtual,
                baseMethod.ReturnType,
                paramTypes);
            
            var il = method.GetILGenerator();
            
            // this._wrapper.OnRender(arg1, arg2)
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, wrapperField);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            
            var wrapperMethod = typeof(LandClaimMapLayerWrapper).GetMethod("OnRender");
            il.Emit(OpCodes.Callvirt, wrapperMethod);
            
            il.Emit(OpCodes.Ret);
            
            typeBuilder.DefineMethodOverride(method, baseMethod);
        }
        
        private void BuildOnMouseMoveOverride(TypeBuilder typeBuilder, FieldBuilder wrapperField)
        {
            var baseMethod = mapLayerType.GetMethod("OnMouseMove",
                BindingFlags.Public | BindingFlags.Instance);
            
            if (baseMethod == null)
            {
                capi.Logger.Warning("[LandBaron] OnMouseMove method not found in MapLayer");
                return;
            }
            
            var paramTypes = baseMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            
            var method = typeBuilder.DefineMethod(
                "OnMouseMove",
                MethodAttributes.Public | MethodAttributes.Virtual,
                baseMethod.ReturnType,
                paramTypes);
            
            var il = method.GetILGenerator();
            
            // this._wrapper.OnMouseMove(arg1, arg2, arg3)
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, wrapperField);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_3);
            
            var wrapperMethod = typeof(LandClaimMapLayerWrapper).GetMethod("OnMouseMove");
            il.Emit(OpCodes.Callvirt, wrapperMethod);
            
            il.Emit(OpCodes.Ret);
            
            typeBuilder.DefineMethodOverride(method, baseMethod);
        }
    }
}
```

### LandClaimMapLayerWrapper.cs (Versão Completa)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace LandBaron.Integration
{
    public class LandClaimMapLayerWrapper
    {
        private ICoreClientAPI capi;
        private LandBaronSystem landSystem;
        
        // Cache
        private long lastCacheUpdate = 0;
        private List<LandClaim> cachedVisibleClaims = new List<LandClaim>();
        
        public LandClaimMapLayerWrapper(ICoreClientAPI api, LandBaronSystem system)
        {
            this.capi = api;
            this.landSystem = system;
        }
        
        public void OnRender(object mapElement, float deltaTime)
        {
            try
            {
                // Atualizar cache periodicamente
                long now = capi.World.ElapsedMilliseconds;
                if (now - lastCacheUpdate > 500)
                {
                    UpdateVisibleClaimsCache(mapElement);
                    lastCacheUpdate = now;
                }
                
                // Renderizar claims do cache
                RenderClaims(mapElement);
            }
            catch (Exception ex)
            {
                capi.Logger.Error($"[LandBaron] Error in OnRender: {ex}");
            }
        }
        
        private void UpdateVisibleClaimsCache(object mapElement)
        {
            var viewport = GetMapViewport(mapElement);
            if (viewport == null)
            {
                cachedVisibleClaims.Clear();
                return;
            }
            
            cachedVisibleClaims = landSystem.GetAllClaims()
                .Where(c => ClaimIntersectsViewport(c, viewport))
                .ToList();
        }
        
        private ViewportBounds GetMapViewport(object mapElement)
        {
            try
            {
                Type mapType = mapElement.GetType();
                
                // Obter propriedades do viewport
                var mapBoundsProperty = mapType.GetProperty("Bounds");
                var currentChunkProperty = mapType.GetProperty("CurrentChunkViewBounds");
                
                if (currentChunkProperty != null)
                {
                    var chunkBounds = currentChunkProperty.GetValue(mapElement);
                    if (chunkBounds != null)
                    {
                        // Extrair min/max X e Z
                        var chunkType = chunkBounds.GetType();
                        var minX = (int)chunkType.GetProperty("MinX")?.GetValue(chunkBounds);
                        var maxX = (int)chunkType.GetProperty("MaxX")?.GetValue(chunkBounds);
                        var minZ = (int)chunkType.GetProperty("MinZ")?.GetValue(chunkBounds);
                        var maxZ = (int)chunkType.GetProperty("MaxZ")?.GetValue(chunkBounds);
                        
                        return new ViewportBounds
                        {
                            MinX = minX * 32, // Chunks são 32x32
                            MaxX = (maxX + 1) * 32,
                            MinZ = minZ * 32,
                            MaxZ = (maxZ + 1) * 32
                        };
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private bool ClaimIntersectsViewport(LandClaim claim, ViewportBounds viewport)
        {
            return !(claim.EndPos.X < viewport.MinX ||
                     claim.StartPos.X > viewport.MaxX ||
                     claim.EndPos.Z < viewport.MinZ ||
                     claim.StartPos.Z > viewport.MaxZ);
        }
        
        private void RenderClaims(object mapElement)
        {
            if (cachedVisibleClaims.Count == 0) return;
            
            // Obter surface do mapa
            Type mapType = mapElement.GetType();
            var getSurfaceMethod = mapType.GetMethod("get_Surface");
            var surface = getSurfaceMethod?.Invoke(mapElement, null) as ImageSurface;
            
            if (surface == null) return;
            
            using (var ctx = new Context(surface))
            {
                foreach (var claim in cachedVisibleClaims)
                {
                    DrawClaim(ctx, mapElement, claim);
                }
            }
        }
        
        private void DrawClaim(Context ctx, object mapElement, LandClaim claim)
        {
            // Converter coordenadas
            var screenStart = WorldPosToScreenPos(mapElement, claim.StartPos);
            var screenEnd = WorldPosToScreenPos(mapElement, claim.EndPos);
            
            if (screenStart == null || screenEnd == null) return;
            
            double x = screenStart.X;
            double y = screenStart.Y;
            double width = screenEnd.X - x;
            double height = screenEnd.Y - y;
            
            // Cor baseada no tipo
            double r, g, b, a;
            if (claim.OwnerId == capi.World.Player.PlayerUID)
            {
                r = 0.0; g = 0.8; b = 0.0; a = 0.25; // Verde
            }
            else if (claim.ForSale)
            {
                r = 1.0; g = 1.0; b = 0.0; a = 0.25; // Amarelo
            }
            else
            {
                r = 0.8; g = 0.0; b = 0.0; a = 0.25; // Vermelho
            }
            
            // Preencher
            ctx.SetSourceRGBA(r, g, b, a);
            ctx.Rectangle(x, y, width, height);
            ctx.Fill();
            
            // Borda
            ctx.SetSourceRGBA(r, g, b, 0.7);
            ctx.LineWidth = 1.5;
            ctx.Rectangle(x, y, width, height);
            ctx.Stroke();
        }
        
        private Vec2d WorldPosToScreenPos(object mapElement, BlockPos worldPos)
        {
            try
            {
                Type mapType = mapElement.GetType();
                var method = mapType.GetMethod("WorldPosToMapPos");
                
                if (method == null) return null;
                
                var vec3d = new Vec3d(worldPos.X, worldPos.Y, worldPos.Z);
                var result = method.Invoke(mapElement, new object[] { vec3d });
                
                return result as Vec2d;
            }
            catch
            {
                return null;
            }
        }
        
        public void OnMouseMove(object mouseEvent, object mapElement, object hoverText)
        {
            try
            {
                var worldPos = GetMouseWorldPosition(mouseEvent, mapElement);
                if (worldPos == null) return;
                
                var blockPos = worldPos.AsBlockPos;
                var claim = landSystem.GetClaimAtPosition(blockPos);
                
                if (claim != null)
                {
                    StringBuilder sb = hoverText as StringBuilder;
                    if (sb != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"§6Land Claim§r");
                        sb.AppendLine($"Owner: {claim.OwnerName}");
                        
                        if (claim.ForSale)
                        {
                            sb.AppendLine($"§eFor Sale: {claim.Price} gears§r");
                        }
                        
                        var size = (claim.EndPos.X - claim.StartPos.X) * 
                                   (claim.EndPos.Z - claim.StartPos.Z);
                        sb.AppendLine($"Size: {size} blocks²");
                    }
                }
            }
            catch (Exception ex)
            {
                capi.Logger.Error($"[LandBaron] Error in OnMouseMove: {ex}");
            }
        }
        
        private Vec3d GetMouseWorldPosition(object mouseEvent, object mapElement)
        {
            try
            {
                Type mapType = mapElement.GetType();
                var method = mapType.GetMethod("ScreenPosToWorldPos");
                
                if (method == null) return null;
                
                Type mouseType = mouseEvent.GetType();
                int mouseX = (int)mouseType.GetProperty("X")?.GetValue(mouseEvent);
                int mouseY = (int)mouseType.GetProperty("Y")?.GetValue(mouseEvent);
                
                var result = method.Invoke(mapElement, new object[] { mouseX, mouseY });
                return result as Vec3d;
            }
            catch
            {
                return null;
            }
        }
    }
    
    // Classe auxiliar
    public class ViewportBounds
    {
        public int MinX { get; set; }
        public int MaxX { get; set; }
        public int MinZ { get; set; }
        public int MaxZ { get; set; }
    }
}
```

---

## Checklist de Implementação

Use este checklist ao implementar integração com mapa:

### Preparação
- [ ] Versão do jogo é 1.21.5 ou superior
- [ ] Projeto configurado com `AllowUnsafeBlocks`
- [ ] Referência a `VintagestoryAPI.dll` configurada
- [ ] System.Reflection.Emit disponível

### Implementação
- [ ] Classe `LandBaronMapIntegration` criada
- [ ] Classe `LandClaimMapLayerWrapper` criada
- [ ] Método `CreateDynamicMapLayer` implementado
- [ ] Construtores dinâmicos via IL Emit
- [ ] Override de `OnRender` via IL Emit
- [ ] Override de `OnMouseMove` via IL Emit

### Renderização
- [ ] Viewport culling implementado
- [ ] Cache de claims visíveis
- [ ] Conversão WorldPos → ScreenPos
- [ ] Desenho com Cairo Context
- [ ] Cores diferenciadas por tipo
- [ ] Performance aceitável (>30 FPS)

### Tooltips
- [ ] Detecção de hover sobre claim
- [ ] Informações do owner exibidas
- [ ] Preço mostrado se à venda
- [ ] Tamanho do claim calculado

### Testes
- [ ] Zoom in/out funciona
- [ ] Pan do mapa funciona
- [ ] Tooltips aparecem corretamente
- [ ] Performance com 100+ claims
- [ ] Sem crashes ou memory leaks
- [ ] Multiplayer testado

### Fallback
- [ ] Sistema funciona sem integração de mapa
- [ ] Mensagem clara se mapa não disponível
- [ ] Alternativa (waypoints/HUD) considerada

---

## Recursos Adicionais

### Documentação Oficial
- **VS API Docs**: https://apidocs.vintagestory.at/
- **Modding Wiki**: https://wiki.vintagestory.at/Modding:Getting_Started
- **Discord**: https://discord.gg/vintagestory (canal #modding)

### Mods Open-Source com MapLayer
- **Prospector Info**: https://github.com/p3t3rix-vsmods/VsProspectorInfo
- **GiMap**: Disponível no VS ModDB (código parcialmente público)
- **Cartographer**: Sistema de waypoints compartilhados

### Ferramentas
- **dnSpy**: Decompilador para inspecionar assemblies do VS
- **ILSpy**: Alternativa ao dnSpy
- **Visual Studio**: Debugging de reflection

---

## Conclusão

A integração com o sistema de mapa do Vintage Story requer uso avançado de Reflection e IL Emit devido às classes internas não expostas na API pública. Esta documentação fornece:

1. ✅ Explicação completa do problema e arquitetura
2. ✅ Solução via Reflection com código completo
3. ✅ Implementação passo a passo
4. ✅ Otimizações de performance
5. ✅ Troubleshooting de problemas comuns
6. ✅ Alternativas caso reflection não funcione

**Próximos Passos para Seu Mod LandBaron:**

1. Implementar `LandBaronMapIntegration` e `LandClaimMapLayerWrapper`
2. Testar com reflection em ambiente de desenvolvimento
3. Implementar viewport culling para performance
4. Adicionar tooltips informativos
5. Testar em servidor multiplayer
6. Considerar fallback com waypoints se necessário

**Importante**: Este método usa APIs internas que podem mudar entre versões. Sempre teste após atualizações do jogo e mantenha fallbacks funcionais.

Boa sorte com a implementação! 🗺️
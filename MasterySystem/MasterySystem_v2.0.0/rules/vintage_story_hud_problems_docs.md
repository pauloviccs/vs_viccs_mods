# Problemas do HUD e Regras de Implementação

## Diagnóstico do Crash Anterior

O jogador sofria um crash imediato ao entrar no servidor quando estava dentro de um território reivindicado. O crash ocorria especificamente quando `hud.ShowNotification()` estava ativo.

### Causas Raiz Identificadas em `TerritoryHud`

1. **Gerenciamento Impróprio do Compositor**: Criar um novo `GuiComposer` ("territoryhud") a cada tick do cliente (`ClientTick`) é perigoso. O padrão do Vintage Story é construir o compositor uma vez e atualizar o conteúdo textual dinamicamente.
2. **Violações de Threading/Recursos**: A recriação rápida de elementos de UI (60 vezes por segundo) causava vazamento de memória ou condições de corrida no motor de renderização.
3. **Uso Incorreto de Fonte**: Tentativas de clonar fontes estáticas (`CairoFont`) podem causar erros se a API não suportar, e a recriação constante agravava isso.
4. **Chamada `Init` Manual**: Estávamos construindo `Composers` manualmente sem respeitar o ciclo de vida da classe base `HudElement`.

### 5. Análise do csproj

- Verificado `LandBaron.csproj`. As referências para `VintagestoryAPI` e `VintagestoryLib` estão presentes e corretas.
- Nenhum problema de referência ausente foi encontrado.

## Plano de Correção e TODO (Status: CONCLUÍDO)

### 1. Refatoração para o Ciclo de Vida Padrão de `HudElement`

- [x] Implementar método `ComposeGuis()` separado.
- [x] Construir o compositor *uma única vez* no `ComposeGuis`.
- [x] Usar `GetDynamicText("lbl").SetNewText(newText)` para atualizar apenas o texto, em vez de destruir e recriar todo o compositor.
- [x] Usar `TryOpen()` apenas para alternar a visibilidade.

### 2. Correção no Uso de Fontes

- [x] Removido uso de `.Clone()` que causava erro de build. Utilizado `CairoFont.WhiteSmallText().WithOrientation(...)` de forma segura dentro da estruturação única.

### 3. Otimização

- [x] Removidas verificações de string manuais (`currentDisplayedText`); o próprio motor de UI lida com atualizações de texto de forma eficiente agora.
- [x] `OnRenderGUI` agora gerencia apenas o tempo de fade e fechamento, sem tocar na estrutura do compositor.

### 4. Limpeza

- [x] Removidos campos não utilizados (`lastText`, etc) para limpar avisos do compilador (CS0414).

## Padrão de Implementação Correto (Aplicado)

```csharp
public class TerritoryHud : HudElement
{
    // ... campos ...

    // Método auxiliar para criar a estrutura da GUI (chamado uma vez ou quando necessário recriar)
    public void ComposeGuis()
    {
        ElementBounds textBounds = ElementBounds.Fixed(0, 0, 300, 80);
        // ... definição de bounds ...
        
        Composers["territoryhud"] = capi.Gui.CreateCompo(...)
            .AddDynamicText("", ..., "lbl") // Texto inicia vazio
            .Compose();
    }

    public void ShowNotification(LandClaim claim, ...)
    {
        // 1. Garantir que o Compositor existe
        if (!Composers.ContainsKey("territoryhud")) ComposeGuis();

        // 2. Atualizar apenas o Texto Dinâmico
        var textElem = Composers["territoryhud"].GetDynamicText("lbl");
        textElem.SetNewText(novoTexto);

        // 3. Abrir se estiver fechado
        if (!isVisible) TryOpen();
    }
}
```

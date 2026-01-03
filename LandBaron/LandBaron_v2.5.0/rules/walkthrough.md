# Walkthrough - Restauração do HUD de Território

Eu restaurei e aprimorei com sucesso a classe `TerritoryHud` no mod LandBaron.

## Alterações

- **Descomentei a classe `TerritoryHud`** em `src/LandBaronSystem.cs`.
- **Aprimorei o `TerritoryHud`**:
  - Adicionei exibição para:
    - **Nome do Dono** (Verde se for seu, Vermelho se for de outros)
    - **Preço de Venda** (Amarelo se estiver à venda)
    - **Contagem de Territórios** (Tamanho do Império)
    - **Saldo do Jogador** (Ícone de carteira)
  - Implementei uma exibição persistente que permanece por 8 segundos após entrar em um chunk.
  - Adicionei lógica para estender o tempo de exibição se a informação não tiver mudado.
- **Integrado no `ClientTick`**:
  - Substituí o spam de mensagens no chat por `hud.ShowNotification()`.
  - Passei o saldo dinâmico do jogador para o HUD.

## Resultados da Verificação

### Verificação de Build

- Executei `dotnet build` com sucesso.
- Corrigi um erro de compilação referente a `WithFixedAlignment` (alterado para `WithAlignment`).

### Guia de Teste Manual

Para verificar no jogo:

1. Entre no servidor.
2. Caminhe para um chunk reivindicado.
3. Observe o HUD aparecendo no centro inferior.
4. Verifique se as informações correspondem à saída do chat das versões anteriores.
5. Verifique se o saldo atualiza corretamente ao realizar transações.

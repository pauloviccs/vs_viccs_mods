**Patch Notes 0.0.5** | *Land Baron 2.5.0 - Status & Roadmap*

Este documento sumariza o estado atual do mod **LandBaron**, suas funcionalidades, comandos, limitações técnicas e o plano de melhorias para a próxima versão.

---

**Resumo do Mod** (Estado Atual)

O **LandBaron** é um mod de economia e território focado em servidores. Ele permite que jogadores comprem, vendam e protejam chunks (áreas de 32x32 blocos) usando uma moeda física (`game:gear-rusty`). O mod inclui um sistema bancário robusto e visualização 3D de fronteiras.

---

**Funcionalidades e Features**

### 1. Sistema Bancário

* **Moeda Física:** Utiliza itens do jogo como moeda (configurável).
* **Contas Virtuais:** Armazenamento seguro de moedas em "contas bancárias" persistentes.
* **Caixa Eletrônico (Vacuum):** Blocos especiais criados por admins que sugam moedas jogadas no chão e as depositam na conta do jogador mais próximo.
* **Transferências:** Envio de dinheiro entre jogadores online.
* **Sons:** Efeitos sonoros de caixa registradora para transações.

### 2. Sistema de Território (Land Claim)

* **Compra de Chunks:** Jogadores podem comprar a área onde estão pisando.
* **Custo Progressivo:** O preço dos terrenos aumenta exponencialmente baseado na quantidade de terras que o jogador já possui (evita monopólio).
* **Proteção de Blocos:** Impede que não-donos quebrem, coloquem ou interajam com blocos no território (exceto Admins).
* **Comércio de Terras:** Jogadores podem definir um preço de venda para seus terrenos.
* **Permissões:** Sistema para adicionar amigos (atualmente global para todos os terrenos do dono).
* **Visualização 3D:** Partículas coloridas mostram as fronteiras do chunk no mundo (Verde = Seu, Vermelho = Outros, Amarelo = Venda).
* **Abandono:** Possibilidade de descadar terrenos.

---

**Referência de Comandos**

### 1. Economia

| Comando | Descrição | Permissão |
| :--- | :--- | :--- |
| `/saldo` | Exibe seu saldo bancário atual. | Todos |
| `/depositar [qtd]` | Deposita o valor da mão para o banco. | Todos |
| `/sacar [qtd]` | Saca dinheiro do banco para o inventário. | Todos |
| `/transferir [player] [qtd]` | Transfere dinheiro para outro jogador online. | Todos |
| `/banco criar` | Cria um ponto de coleta (Caixa Eletrônico) no bloco mirado. | **Admin** |

### 2. Território

| Comando | Descrição | Permissão |
| :--- | :--- | :--- |
| `/terreno comprar` | Compra o chunk atual (se livre ou à venda). | Todos |
| `/terreno vender [preço]` | Coloca o chunk atual à venda. `-1` remove a venda. | Dono |
| `/terreno abandonar` | Remove sua posse do chunk atual. | Dono |
| `/terreno add [player]` | Dá permissão de construção para um jogador em **TODAS** as suas terras. | Dono |
| `/terreno ver` | Ativa a visualização de partículas das fronteiras por 60s. | Todos |

---

**Bugs Conhecidos e Limitações**

1. **HUD Desativado:** O código referente à interface gráfica (`TerritoryHud`) está comentado (`//`) na classe principal. Atualmente, todas as informações são enviadas via Chat, o que pode poluir a tela.
2. **Permissões Globais:** O comando `/terreno add` adiciona o amigo a *todos* os terrenos do jogador. Não há como dar permissão apenas em um chunk específico.
3. **Sync Pesado:** Ao entrar no servidor, toda a lista de claims é enviada para o cliente. Em servidores massivos, isso pode causar lag na entrada.
4. **Vacuum Agressivo:** O sistema de banco (Vacuum) verifica todas as entidades a cada 250ms. Pode precisar de otimização se houverem muitos itens no chão.

---

**Melhorias para o Patch 0.0.5**

Com base na análise do código, as seguintes melhorias são recomendadas para a próxima versão:

### 1. Restaurar e Melhorar o HUD

**Prioridade Alta.** Descomentar e corrigir a classe `TerritoryHud`.

* Mostrar nome do dono, preço de venda e saldo atual de forma persistente na tela ao entrar em um chunk.
* Substituir mensagens de chat excessivas por pop-ups elegantes.

### 2. Permissões Granulares

**Refatoração Necessária.**

* Alterar `/terreno add` para funcionar apenas no chunk atual por padrão?
* Ou criar `/terreno trust [player]` (para todos) vs `/terreno permit [player]` (apenas este chunk).

### 3. Configuração

**Expansão do `ModConfig`.**

* Adicionar lista de blocos ignorados (que podem ser usados por todos, ex: portas, baús públicos).
* Configurar intervalo do Vacuum.

### 4. Integração com Mapa (Opcional)

* Investigar possibilidade de desenhar os claims no mapa mundial (M).

---

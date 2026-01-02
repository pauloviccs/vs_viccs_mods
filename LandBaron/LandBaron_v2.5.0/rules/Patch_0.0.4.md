:8299_Loading:  •  :scroll: **Patch Notes 0.0.4** | *Land Baron 2.0.0*

:Alert~1:  • A economia do servidor acaba de subir de nível! O mod **LandBaron** foi completamente reescrito para a versão **2.0.0**. Agora ele roda tanto no cliente quanto no servidor, trazendo efeitos visuais, correções de crashes e o retorno triunfal do sistema bancário.

:GreenCheck: •  :sparkles: **Novidades Principais**

:9104verifypurple:  • **Visualização 3D de Fronteiras:** Chega de adivinhar onde termina o seu terreno! Agora você pode ver as bordas do chunk com partículas brilhantes.

:9104verifypurple:  • **Sistema Bancário Restaurado:** Os comandos de depósito e saque voltaram e estão 100% funcionais.

:9104verifypurple:  • **Notificações Elegantes:** Ao entrar nas terras de alguém, você recebe um aviso discreto no chat com o nome do dono e o tamanho do império dele (sem spam!).

:9104verifypurple:  • **Proteção & Venda:** Compre terras livres, coloque-as à venda para outros jogadores ou abandone-as se falir.

||----------------------------------------------------------------------------------||

:8299_Loading:   •  :tools: **Lista de Comandos**

:_carregando_:   •  :moneybag: **Economia & Banco**

:GreenCheck:   • Use estes comandos para gerenciar suas moedas (`game:gear-rusty`).

:9104verifypurple:  •  `/saldo`
    *** Exibe quanto dinheiro você tem na conta bancária.**

:9104verifypurple:  •  `/depositar [quantidade]`
    *** Transfere o dinheiro da sua mão para o banco.**
   ** * *Ex:* `/depositar 50`**

:9104verifypurple:  •  `/sacar [quantidade]`
**    * Retira dinheiro do banco e coloca no seu inventário.
    * *Ex:* `/sacar 100`**

:8299_Loading:   •  :european_castle: **Gestão de Terras**

:9104verifypurple:  •   `/terreno ver` **(NOVO!)**
   :_carregando_:   •   Faz as bordas do chunk atual brilharem por **60 segundos**.
   :_carregando_:   •   :green_circle: **Verde Neon:** Terras que são suas.
   :_carregando_:   •   :red_circle: **Vermelho:** Terras de outros jogadores.
   :_carregando_:   •   :yellow_circle: **Amarelo:** Terras que estão à venda.

:9104verifypurple:  •   `/terreno comprar`
   :_carregando_:   • Compra o terreno onde você está pisando (se não tiver dono ou estiver à venda).
   :_carregando_:   •  *Se tentar comprar terra de outro jogador que não está à venda, você receberá um alerta.*

:9104verifypurple:  •   `/terreno vender [preço]`
  :_carregando_:   • Coloca o seu terreno atual à venda pelo preço definido. Qualquer um poderá comprar.
  :_carregando_:   • *Ex:* `/terreno vender 500`

:9104verifypurple:  •  `/terreno abandonar`
  :_carregando_:   •  Remove sua posse do terreno. Cuidado: não há reembolso!

:8299_Loading:   •  :shield: **Admin (Apenas OP)**

:9104verifypurple:  •  `/banco criar`
  :_carregando_:   • Transforma o bloco que você está olhando em um "Caixa Eletrônico" físico que suga moedas jogadas perto dele.

||----------------------------------------------------------------------------------||

:8299_Loading:   •  :bug: **Correções de Bugs**
  :1476verifygreen: • Corrigido crash crítico ao entrar no servidor (||NullReferenceException||).
  :1476verifygreen: • Corrigido erro de "||GetNearestPlayer||" que impedia o banco automático de funcionar.
  :1476verifygreen: • Otimização de rede: O servidor não envia mais pacotes desnecessários, reduzindo o lag.
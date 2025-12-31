# Animal Transport — Mod GGD & Technical Summary

> **Version:** 1.0.3
> **Author:** VICCS

## 1. Conceito Central (The North Star)
O objetivo é permitir que jogadores capturem e transportem animais de pequeno porte (Galinhas, Coelhos, Guaxinins) utilizando recipientes vanilla (Cestas e Baús) de forma intuitiva, sem a necessidade de cordas complexas ou mods de teleporte pesados.

---

## 2. Mecânicas de Jogo (User Flow)

### Captura
*   **Input:** Clique Direito (Interagir) com o Item na mão apontando para a entidade.
*   **Itens Válidos:** `Reed Basket` (Cesta de Junco), `Reed Chest` (Baú de Junco).
*   **Restrições:**
    *   Tamanho da Entidade: Caixa de seleção < 1.1 blocos (X/Y/Z).
    *   Estado: O item deve estar vazio (sem animal capturado).
*   **Feedback:**
    *   Som: `game:sounds/effect/squish1`
    *   Visual: Entidade desaparece.
    *   UI: Nome do item muda para `Item Name (Animal Name)`.

### Transporte
*   O animal "vive" dentro dos Atributos do ItemStack (NBT) enquanto capturado.
*   Dados preservados: Saúde, Nome (se houver), Atributos genéricos.

### Liberação
*   **Input:** Clique Direito (Interagir) no chão (BlockSelection).
*   **Lógica:** O mod verifica se a posição é válida e recria a entidade com os dados salvos.
*   **Feedback:**
    *   Som: `game:sounds/effect/squish2`
    *   Visual: Animal reaparece no mundo.

---

## 3. Arquitetura Técnica

### Componentes Principais
1.  **`CollectibleBehaviorEntityCatch` (Behavior):**
    *   Classe principal que gerencia a lógica de `OnHeldInteractStart`.
    *   Injetada via JSON (`transport_behavior.json`) em Itens específicos.
    *   Realiza a serialização/deserialização da entidade.

2.  **`EntityInteractPatch` (Harmony Patch):**
    *   **Alvo:** `Entity.OnInteract`
    *   **Objetivo:** Interceptar interações vanilla quando o jogador segura um item de transporte.
    *   **Obs:** Crítico para evitar que mensagens como "Este animal é selvagem demais" bloqueiem a captura.

3.  **Sistema de Serialização:**
    *   Usa **Reflection** para invocar `ToAttribute` e `FromAttribute` (métodos internos do jogo) para garantir persistência máxima de dados.


### Bugs Críticos
*   **[CRITICAL] "Too wild to be captured":** Em animais ariscos (Galos, etc), o jogo dispara a mensagem de erro vanilla antes (ou apesar) da nossa lógica de captura, impedindo a ação. O Harmony Patch atual ainda não resolveu isso completamente (Investigação em andamento).

---

## 5. Roadmap (Protocolo de Melhoria para também implementar)
*   **Feedback Visual:** Adicionar partículas ou renderizar o modelo do animal dentro da cesta (se possível).
*   **Refatoração:** Remover dependência de Reflection se API pública permitir.
*   **Configuração:** Permitir whitelist/blacklist de entidades via config file.

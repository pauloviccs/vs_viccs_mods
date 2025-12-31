# Design Spec: Vintagestory Waypoint Mod (v1.21.5)

## 1. Visão Geral (The North Star)
Um mod *Server-Side* leve que permite aos jogadores salvar até 10 locais pessoais e se teletransportar para eles ao custo de nutrição (satiety).

* **Target Version:** Vintage Story 1.21.5+
* **Tech Stack:** C# .NET 7.0 (Padrão do VS 1.20+), Vintagestory API.

## 2. Estrutura de Arquivos
Você deve criar a seguinte estrutura no seu projeto C# (Visual Studio ou VS Code):

```text
MyWaypointMod/
├── modinfo.json            <-- Metadados do Mod
├── src/
│   ├── WaypointModSystem.cs  <-- Lógica Principal e Comandos
│   └── WaypointData.cs       <-- Modelo de Dados (O que é salvo)
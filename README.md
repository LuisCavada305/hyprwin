# HyprWin

Tiling window manager no estilo [Hyprland](https://hyprland.org) para Windows 11, **sem instalar nada**.
Usa só Windows PowerShell 5.1, C# compilado em tempo de execução (`Add-Type`) e a Win32 API.

Inclui também o rice do terminal: prompt em duas linhas, fetch no estilo fastfetch e config do Windows Terminal (Rosé Pine).

## Recursos

- Layout **dwindle** com gaps e borda colorida na janela ativa.
- **Workspaces** 1–10 por monitor; workspaces podem ser fixados a um monitor.
- **Animações**: janelas novas crescem (popin), retile desliza, troca de workspace desliza.
- Foco segue o mouse; o cursor vai até a janela focada pelo teclado.
- Config com a sintaxe do Hyprland (`bind`, `bindm`, `workspace`, `windowrulev2`) e recarga ao salvar.
- Indicador de workspaces na tela e ícone na bandeja com o workspace atual.

## Atalhos padrão

| Atalho | Ação |
|---|---|
| `Win+Q` / `Win+Enter` | Terminal (`wt.exe`) |
| `Win+C` | Fecha a janela ativa |
| `Win+1..9`, `Win+0` | Workspace 1–10 |
| `Win+Shift+1..0` | Move a janela para o workspace |
| `Win+setas` | Move o foco (cruza monitores) |
| `Win+Shift+setas` | Troca a janela de lugar / de monitor |
| `Win+V` | Floating |
| `Win+F` | Fullscreen |
| `Win+J` | Toggle split |
| `Win+E` | Explorer |
| `Win+M` | Sai do HyprWin (mostra todas as janelas) |
| `Win+scroll` | Workspace seguinte / anterior no monitor |
| `Win+arrastar` | Esquerdo move, direito redimensiona |

## Instalação

```powershell
git clone https://github.com/LuisCavada305/hyprwin.git $env:USERPROFILE\hyprwin
& "$env:USERPROFILE\hyprwin\hyprwin.ps1" -Install   # cria o atalho na inicialização e inicia
```

Outros comandos:

```powershell
.\hyprwin.ps1 -Stop        # para e mostra todas as janelas
.\hyprwin.ps1 -Uninstall   # para e remove o atalho
.\hyprwin.ps1 -Restore     # mostra janelas que ficaram ocultas após uma falha
```

Edite `hyprwin.conf` e salve para recarregar. Os nomes dos monitores (`DISPLAY1`, `DISPLAY2`…) aparecem em `hyprwin.log`.

### Rice do terminal (opcional)

- `powershell/Microsoft.PowerShell_profile.ps1` → copie para o caminho de `$PROFILE`.
  Prompt em duas linhas (caminho curto em cima, `❯` embaixo), cores do PSReadLine, `ll` com ícones e `fetch`.
- `terminal/settings.json` → referência para o Windows Terminal (modo foco sem barra, Rosé Pine, `-NoLogo`).
- Requer a fonte [JetBrainsMono Nerd Font](https://www.nerdfonts.com/).

## Limitações

- `Win+L` não pode ser capturada (o Windows reserva para bloquear a tela).
- Janelas de apps elevados (admin) não são gerenciadas.
- Workspaces ocultam janelas: elas somem da barra de tarefas e do Alt+Tab até você voltar ao workspace.
  Não use as áreas de trabalho virtuais nativas junto.
- Sem fade nem animação ao fechar.

## Licença

MIT

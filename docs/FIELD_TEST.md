# Teste de campo autorizado

Status: preparação técnica. Sem execução em uma máquina Windows autorizada,
registre `NEEDS_AUTHORIZED_WINDOWS_FIELD_TEST`.

## Pré-requisitos

- um Server/Admin e uma estação Client;
- Windows 10/11 em edição documentada;
- conta administrativa separada para recovery;
- backup do estado da estação;
- Agent publicado, instalado e provisionado com segredo DPAPI;
- HTTPS e firewall configurados pelo installer quando a LAN exigir;
- operador autorizado e janela de manutenção registrada.

## Pareamento

- [ ] Admin usa `INICIAR CONFIGURAÇÃO`.
- [ ] código de seis dígitos aparece com expiração de 10 minutos.
- [ ] Client usa `PAREAR CONFIGURAÇÃO`.
- [ ] solicitação aparece no Admin com hostname.
- [ ] aprovação explícita conclui o fluxo.
- [ ] credencial individual é salva pelo Client usando DPAPI.
- [ ] código incorreto, expirado, reutilizado e uso simultâneo são rejeitados.
- [ ] revogação impede novas comunicações.

## Estação BLOCKED

- [ ] fechar Client;
- [ ] `Alt+F4`;
- [ ] `Alt+Tab`;
- [ ] tecla Windows;
- [ ] `Win+R`;
- [ ] Start;
- [ ] taskbar;
- [ ] Explorer/Desktop;
- [ ] iniciar aplicativo externo;
- [ ] Task Manager conforme a política;
- [ ] `Ctrl+Alt+Del` sem obter Desktop livre;
- [ ] reboot;
- [ ] desligar e ligar;
- [ ] desconectar rede;
- [ ] crash do Client;
- [ ] restart do Agent;
- [ ] Server offline.

Resultado esperado: o usuário comum não transforma a estação bloqueada em um
Desktop livre. O produto não intercepta a Secure Attention Sequence.

## Sessão

- [ ] Admin inicia/libera sessão.
- [ ] login funciona.
- [ ] somente aplicativos permitidos funcionam.
- [ ] tempo, saldo e heartbeat são atualizados.
- [ ] reconnect não duplica sessão.
- [ ] fim automático retorna imediatamente a `BLOCKED`.
- [ ] fim manual retorna imediatamente a `BLOCKED`.
- [ ] fechar o Client durante a sessão não remove o controle da estação.

## Comandos administrativos

- [ ] `LOCK`;
- [ ] `UNLOCK`;
- [ ] `RESTART`;
- [ ] `SHUTDOWN`;
- [ ] `LOGOFF`;
- [ ] `ENTER_MAINTENANCE`;
- [ ] `EXIT_MAINTENANCE`;
- [ ] cada comando é allowlisted, idempotente, expirável e auditado;
- [ ] payload `cmd.exe /c ...` e PowerShell arbitrário são rejeitados.

## Recovery e uninstall

- [ ] Agent quebrado;
- [ ] Client quebrado;
- [ ] política inválida;
- [ ] configuração inválida;
- [ ] Server indisponível;
- [ ] conta administrativa separada recupera acesso;
- [ ] `Uninstall-ClientAgent.ps1` restaura as políticas;
- [ ] shell, taskbar e Task Manager retornam ao estado anterior;
- [ ] serviço, startup, firewall e URL ACL criados pelo produto são removidos;
- [ ] Registry e backup local são verificados.

## Critério

Relatar separadamente `CODE VERIFIED`, `WINDOWS VERIFIED` e `FIELD VERIFIED`.
Sem execução deste checklist em Windows autorizado, o status máximo é
`READY FOR AUTHORIZED FIELD TEST`, nunca `production ready`.

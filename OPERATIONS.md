# Operação

## Estados operacionais

O Server usa `Offline`, `Idle`, `InSession`, `Locked` e `Maintenance` para
expor o estado da estação. Uma máquina recém-pareada inicia bloqueada; sem
sessão válida, o Agent permanece bloqueando as políticas locais. A sessão só
é liberada pelo fluxo autenticado do Server e termina retornando ao bloqueio.

Perda do Server não cria uma sessão offline: sem sessão ativa, o Client mantém
o bloqueio. Durante uma sessão, o comportamento de lease/grace period ainda
deve ser definido antes da operação comercial; esta versão não deve ser
considerada validada para uso offline prolongado.

## Pareamento

1. No Admin, cadastre a estação e use `INICIAR CONFIGURAÇÃO`.
2. Informe o código exibido no Client usando `PAREAR CONFIGURAÇÃO`.
3. Confirme o hostname no Admin.
4. Clique em aprovar e aguarde a verificação final do Client.
5. Instale e inicie o Agent na mesma estação, seguindo
   `docs/INSTALLATION.md`.

Não reutilize códigos. Em suspeita de exposição, revogue a estação, gere novo
pareamento e registre o incidente na auditoria.

## Recovery e uninstall

Mantenha uma conta administrativa separada e uma janela de manutenção. Em caso
de política inválida, execute o Agent elevado com `--recover`; para remoção,
use `deployment/Uninstall-ClientAgent.ps1`, que só remove o serviço depois de
tentar restaurar as políticas. Verifique Registry, shell, taskbar, Desktop,
Task Manager e backup local após a operação.

## Diagnóstico

O Admin exibe Client/Agent, versão de política e última comunicação. Os logs
técnicos ficam em `%LocalAppData%\\Adrenalina\\Admin\\logs`,
`%LocalAppData%\\Adrenalina\\Client\\logs` e no Event Log do Windows para o
Agent. Nunca solicite ou copie segredos para os logs.

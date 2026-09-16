# Segurança

## Limites do componente privilegiado

`Adrenalina.Client` é uma aplicação WPF sem privilégio administrativo
permanente. Alterações do Windows ficam no `Adrenalina.Agent`, um serviço
separado que aceita somente ações allowlisted pelo named pipe `Adrenalina.Agent`.
Cada requisição IPC exige protocolo, timestamp, nonce e prova HMAC usando o
segredo protegido por DPAPI LocalMachine.

O Agent não aceita shell, PowerShell, caminho de executável, processo ou payload
arbitrário. As ações são lock, unlock, manutenção, restart, shutdown e logoff.
O Registry alterado é limitado a políticas documentadas; o estado anterior é
salvo para recovery/uninstall. Ctrl+Alt+Del não é interceptado.

## Credenciais e pareamento

- o código temporário é aleatório, tem seis dígitos, expira em dez minutos e é
  armazenado somente como SHA-256;
- a aprovação é explícita no Admin e a credencial individual é retornada uma
  única vez;
- o Client armazena a credencial com DPAPI CurrentUser;
- o Server armazena apenas o hash de assinatura e o identificador da
  credencial;
- revogação invalida a máquina e rejeita novas comunicações;
- heartbeat, login e solicitações usam HMAC, janela temporal e nonce com
  proteção contra replay.

Segredos, códigos e provas completas não são escritos nos logs. O instalador
do Agent recebe o segredo por `SecureString` e o grava protegido por DPAPI.

## Revisão realizada

Foram revisados command injection, execução arbitrária, autorização HTTP,
replay, brute force de pareamento, IPC, recovery, uninstall, exposição de
segredos e consistência Client/Agent/Server. Não há P0/P1 conhecido no código
revisado.

Isso não substitui uma revisão operacional nem o teste em Windows autorizado.
Em especial, políticas de shell, edição do Windows, ACL efetiva do serviço,
reboot, perda de rede e comportamento de Secure Attention Sequence precisam de
evidência no ambiente alvo.

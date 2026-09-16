# Checklist de entrega

Preencha este documento para cada cliente e arquive junto com a versão
publicada. O sistema só deve ser declarado operacional depois que todos os
itens do ambiente-alvo tiverem evidência.

| Item | Responsável | Evidência | Resultado |
|---|---|---|---|
| Release publicada pelo `Publish-Release.ps1` | | `release-manifest.json` | |
| Certificado emitido/importado e válido | | thumbprint e validade | |
| Certificado confiável em todas as estações | | lista de máquinas | |
| Firewall Private/LocalSubnet validado | | saída do `Validate-Production.ps1` | |
| Agent instalado, provisionado e saudável | | `docs/FIELD_TEST.md` | |
| Pairing aprovado e credencial DPAPI validada | | `docs/FIELD_TEST.md` | |
| Recovery/uninstall restaura as políticas | | `docs/FIELD_TEST.md` | |
| `/health` e `/health/ready` aprovados | | saída do validador | |
| Cada estação cadastrada e sincronizada | | lista de máquinas no Admin | |
| Reconexão, relógio e múltiplas estações testados | | roteiro assinado | |
| Backup local e cópia fora do servidor | | caminho, data e hash | |
| Restauração em cópia/ambiente de ensaio testada | | data e resultado | |
| Upgrade e rollback exercitados | | release anterior/nova | |
| Política Windows opcional de quiosque validada | | responsável de TI | |

O bloqueio visual do Client não substitui Assigned Access, Shell Launcher ou
outra política de estação. Se o cliente exigir essa proteção, ela deve ser
configurada pelo responsável de TI e testada com uma conta administrativa de
recuperação.

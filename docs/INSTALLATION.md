# Instalação e suporte Windows

## Pacote

Execute `deployment/Publish-Release.ps1` em uma máquina de build. O resultado
inclui `Admin`, `Client`, `Agent`, `Launcher`, scripts de produção e scripts
de recovery/uninstall. Use somente pacotes publicados a partir de uma revisão
identificada.

## Server/Admin e Client

1. Publique o release e instale o Server/Admin conforme
   `deployment/README.md`.
2. Em produção, use HTTPS com certificado real e instale a confiança do
   certificado nas estações.
3. Cadastre cada máquina no Admin e conclua o pareamento antes do login.

## Agent

Em PowerShell elevado, publique o Agent em uma pasta protegida e execute:

```powershell
.\Install-ClientAgent.ps1 -AgentDirectory 'C:\Program Files\Adrenalina'
```

O serviço é instalado como `AdrenalinaClientAgent`, inicia automaticamente e
mantém políticas bloqueadas até receber uma sessão válida. Para recovery e
remoção:

```powershell
.\Uninstall-ClientAgent.ps1 -AgentExecutable 'C:\Program Files\Adrenalina\Adrenalina.Agent.exe'
```

O instalador tenta importar a cópia DPAPI do Client com o mesmo usuário
Windows. Se isso não for possível, use `-MachineSecret` como `SecureString`.
Nunca registre o segredo em arquivo ou chat.

## Matriz de suporte a validar

| Capacidade | Windows 10 Home | Windows 10 Pro/Enterprise | Windows 11 Home | Windows 11 Pro/Enterprise |
|---|---|---|---|---|
| Serviço Windows, DPAPI e named pipe | verificar | verificar | verificar | verificar |
| Políticas Registry allowlisted | verificar | verificar | verificar | verificar |
| Shell/Assigned Access/Shell Launcher | fallback seguro e verificar | verificar edição | fallback seguro e verificar | verificar edição |
| AppLocker/WDAC | não presumir disponível | verificar edição/política | não presumir disponível | verificar edição/política |
| Firewall/URL ACL | somente installer autorizado | somente installer autorizado | somente installer autorizado | somente installer autorizado |

O produto não presume equivalência entre edições. A matriz só pode ser marcada
como suportada após `docs/FIELD_TEST.md` ser executado em uma estação Windows
autorizada. Até lá, registre `NEEDS_AUTHORIZED_WINDOWS_FIELD_TEST`.

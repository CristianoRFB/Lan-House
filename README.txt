============================================================
ADRENALINA - GUIA DE PRIMEIRO ACESSO E OPERACAO
============================================================

Leia este arquivo antes de tocar no sistema pela primeira vez.

1. ONDE FICA CADA ARQUIVO
--------------------------

Depois de publicar o sistema, a distribuicao fica separada por funcao:

   artifacts\release\

Pacote da maquina ADMIN:

   artifacts\release\ADMIN\
   artifacts\release\ADMIN\Adrenalina.Launcher.exe
   artifacts\release\ADMIN\Adrenalina.Admin.exe
   artifacts\release\ADMIN\Adrenalina.Server.exe
   artifacts\release\ADMIN\INSTALAR_ADMIN.bat
   artifacts\release\ADMIN\SE VAI INSTALAR EM ADMIN CLICA AQUI.txt

Pacote da maquina CLIENTE:

   artifacts\release\CLIENTE\
   artifacts\release\CLIENTE\Adrenalina.Launcher.exe
   artifacts\release\CLIENTE\Adrenalina.Client.exe
   artifacts\release\CLIENTE\Adrenalina.Agent.exe
   artifacts\release\CLIENTE\INSTALAR_CLIENTE.bat
   artifacts\release\CLIENTE\SE VAI INSTALAR EM CLIENTE CLICA AQUI.txt

Nao misture os arquivos dos dois pacotes. Cada pasta ja contem as dependencias
necessarias para a sua funcao.

Depois da instalacao de producao, a pasta padrao e:

   C:\Program Files\Adrenalina\

Na maquina ADMIN, essa pasta deve conter:

   C:\Program Files\Adrenalina\Adrenalina.Launcher.exe
   C:\Program Files\Adrenalina\Adrenalina.Admin.exe
   C:\Program Files\Adrenalina\Adrenalina.Server.exe

Na maquina CLIENTE, essa pasta deve conter:

   C:\Program Files\Adrenalina\Adrenalina.Launcher.exe
   C:\Program Files\Adrenalina\Adrenalina.Client.exe
   C:\Program Files\Adrenalina\Adrenalina.Agent.exe

O Launcher abre o Admin ou o Client. O Agent e um servico Windows separado e
nao deve ser aberto manualmente durante a operacao normal.

2. PARA QUE SERVE CADA EXECUTAVEL
---------------------------------

Adrenalina.Launcher.exe

- e o ponto de entrada visual do sistema;
- apresenta a opcao disponivel no pacote instalado (ADMIN ou CLIENTE);
- abre Adrenalina.Admin.exe ou Adrenalina.Client.exe na mesma pasta;
- nao possui o banco, nao controla a estacao e nao substitui o Agent;
- pode ser usado como atalho na Area de Trabalho ou no Menu Iniciar.

Adrenalina.Admin.exe

- e o programa do caixa/administracao;
- hospeda o painel web e o servidor local;
- administra usuarios, maquinas, sessoes, comandos, pareamento, backups e
  configuracoes;
- deve ser executado na maquina que funciona como servidor da lan house;
- nao deve ser instalado ou usado por usuarios comuns nas estacoes.

Adrenalina.Server.exe

- e o componente executavel do servidor local usado pelo pacote ADMIN;
- trabalha junto com o Admin para atender o painel, a API e as estacoes;
- nao deve ser aberto manualmente nem copiado para o pacote CLIENTE.

Adrenalina.Client.exe

- e o programa aberto na estacao usada pelo cliente;
- mostra login, PIN, sessao, tempo, saldo, avisos e estado da conexao;
- envia heartbeat e solicitacoes para o ADMIN;
- conversa com o Agent por IPC autenticado;
- nao deve acessar o banco do Admin nem ser executado como Administrador.

Adrenalina.Agent.exe

- e o componente privilegiado da estacao Windows;
- roda como o servico AdrenalinaClientAgent;
- aplica politicas Windows reversiveis e executa somente acoes autorizadas;
- mantem a politica bloqueada quando nao existe sessao valida;
- nao deve ser aberto com duplo clique durante a operacao;
- so deve ser executado manualmente para instalacao, provisionamento ou
  recovery autorizado.

Os nomes dos projetos podem ser diferentes dos nomes dos executaveis. Use os
nomes dos arquivos acima quando for instalar o pacote publicado.

Dados do Admin:

   %LocalAppData%\Adrenalina\Admin\
   %LocalAppData%\Adrenalina\Admin\adrenalina.db
   %LocalAppData%\Adrenalina\Admin\backups\
   %LocalAppData%\Adrenalina\Admin\logs\Admin.log
   %LocalAppData%\Adrenalina\Admin\logs\Server.log
   %LocalAppData%\Adrenalina\Admin\initial-admin-access.txt

Dados do Client:

   %LocalAppData%\Adrenalina\Client\clientsettings.json
   %LocalAppData%\Adrenalina\Client\machine-credential.dpapi
   %LocalAppData%\Adrenalina\Client\logs\Client.log
   %LocalAppData%\Adrenalina\Runtime\NOME-DA-MAQUINA\

Dados do Agent:

   %ProgramData%\Adrenalina\Agent\machine-secret.dpapi
   %ProgramData%\Adrenalina\Agent\policy-backup.json

Configuracao do servidor instalado:

   %ProgramData%\Adrenalina\server-settings.json
   %ProgramData%\Adrenalina\certs\
   %ProgramData%\Adrenalina\releases\

Os arquivos .dpapi contem credenciais protegidas. Nao copie, renomeie ou
envie esses arquivos sem autorizacao.

3. PUBLICAR O SISTEMA
---------------------

Na raiz do repositorio, abra o PowerShell e execute:

   .\deployment\Publish-Release.ps1

O script restaura dependencias, compila, testa e publica Admin, Client, Agent
e Launcher. O pacote final fica em:

   artifacts\release\

 Na pasta `ADMIN` devem existir `Adrenalina.Admin.exe` e
 `Adrenalina.Launcher.exe`. Na pasta `CLIENTE` devem existir
 `Adrenalina.Client.exe`, `Adrenalina.Agent.exe` e `Adrenalina.Launcher.exe`.
Nao misture os pacotes e nao mova executaveis para fora dos arquivos de suporte
publicados.
Os arquivos .bat devem ser executados dentro da pasta ADMIN ou CLIENTE
publicada. A pasta deployment do repositorio contem apenas os modelos.
Se a instalacao falhar, o motivo completo fica em
%ProgramData%\Adrenalina\logs\Install-ADMIN.latest.log ou
%ProgramData%\Adrenalina\logs\Install-CLIENTE.latest.log.

4. INSTALACAO CORRETA - MAQUINA ADMIN
-------------------------------------

A maquina ADMIN e o computador do caixa/servidor. Ela deve ficar em local
controlado e com conta administrativa protegida.

1. Publique o pacote com Publish-Release.ps1.
2. Abra a pasta correta: ADMIN ou CLIENTE.
3. Copie essa pasta para uma pasta de instalacao, de preferencia:

      C:\Program Files\Adrenalina\

4. Execute o arquivo INSTALAR_ADMIN.bat dentro da pasta ADMIN.
5. Autorize a janela do Windows quando aparecer.
6. Abra C:\Program Files\Adrenalina\Adrenalina.Launcher.exe.
7. Escolha ADMIN na primeira vez.
8. Finalize o primeiro acesso e troque a senha inicial. Se o arquivo nao
   existir ou a senha for perdida, clique em Recuperar acesso na tela de login
   do proprio computador ADMIN; o botao recria o arquivo e gera uma senha
   temporaria.
9. Confirme /health e /health/ready.
10. Faca um backup antes de cadastrar as estacoes.

O instalador ADMIN chama automaticamente Install-Production.ps1, configura o
servidor, cria os atalhos e coloca o Admin na inicializacao do Windows.

Para referencia, a instalacao manual equivalente seria:

      .\Install-Production.ps1 `
          -PackageRoot .\ `
          -EnableLan

Na maquina ADMIN, o executavel essencial e Adrenalina.Admin.exe. O Launcher e
recomendado para o operador. O Adrenalina.Client.exe e o Adrenalina.Agent.exe
nao sao necessarios nessa maquina, exceto se ela tambem for usada como uma
estacao cliente. Nao os apague de uma pasta de release compartilhada sem criar
um pacote especifico.

5. INSTALACAO CORRETA - MAQUINA CLIENTE
---------------------------------------

Cada maquina CLIENTE e uma estacao separada. Ela nao deve abrir o banco nem o
servidor do Admin.

1. Abra somente a pasta CLIENTE do pacote publicado.
2. Copie a pasta CLIENTE para a estacao, de preferencia:

      C:\Program Files\Adrenalina\

3. Execute o arquivo INSTALAR_CLIENTE.bat dentro da pasta CLIENTE.
4. Autorize a janela do Windows quando aparecer.
5. O instalador cria atalho e inicializacao automatica do Client.
6. Abra Adrenalina.Launcher.exe pelo atalho.
7. Escolha CLIENTE.
8. Informe a URL do ADMIN quando solicitado.
9. Conclua o pareamento descrito na proxima secao.
10. Depois da aprovacao, o proprio Client chama a instalacao do Agent.
11. Autorize a janela UAC do Windows quando aparecer.
12. Nao abra Adrenalina.Admin.exe na estacao.

O pacote CLIENTE nao contem o Admin. O executavel essencial da estacao e
Adrenalina.Client.exe; para controle real, Adrenalina.Agent.exe tambem precisa
estar instalado como servico. O Launcher fica junto para facilitar o primeiro
acesso.

6. PRIMEIRA INICIALIZACAO
-------------------------

1. No computador autorizado que sera o caixa/servidor, abra:

      Adrenalina.Launcher.exe

2. Escolha ADMIN.
3. Aguarde o servidor local iniciar.
4. Na primeira inicializacao, procure:

      %LocalAppData%\Adrenalina\Admin\initial-admin-access.txt

5. Use o login e a senha desse arquivo para entrar. A senha inicial e:
   admin admin
6. Clique em Trocar senha no menu lateral e defina uma senha nova com pelo
   menos 12 caracteres.
7. Confirme que o arquivo de acesso inicial foi removido.
8. Se perder a senha depois, use Recuperar acesso na tela de login do ADMIN.
   A acao so funciona localmente, restaura admin admin, invalida a senha atual
   e recria o arquivo.

Nunca coloque senha, PIN, segredo de maquina, certificado privado ou token em
um arquivo do repositorio, em chat ou em log.

7. CONFIGURAR O ADMIN E O SERVIDOR
----------------------------------

No painel ADMIN:

1. Configure o nome da lan house.
2. Confira data, hora, moeda, valores e regras de sessao.
3. Para usar somente no proprio computador, mantenha a escuta local.
4. Para atender a LAN, configure HTTPS com certificado confiavel.
5. Distribua o certificado publico nas estacoes autorizadas.
6. Faca um backup antes de cadastrar maquinas.
7. Confirme /health e /health/ready.

Instalacao de producao em PowerShell elevado:

   .\Install-Production.ps1 `
       -PackageRoot .\ `
       -EnableLan

Use -EnableLan somente quando outras estacoes precisarem acessar o servidor.
O instalador cria regras limitadas ao perfil Private e a sub-rede local.

8. CADASTRAR E PAREAR UMA ESTACAO
---------------------------------

No ADMIN:

1. Abra a lista de maquinas.
2. Cadastre um nome unico para a estacao.
3. Selecione o tipo e o grupo.
4. Clique no botao:

      INICIAR CONFIGURACAO

5. Anote o codigo de seis digitos e sua expiracao.
6. Aguarde a solicitacao do computador cliente.
7. Confira hostname e identidade apresentados.
8. Aprove somente a estacao correta.

No computador cliente:

1. Abra Adrenalina.Launcher.exe.
2. Escolha CLIENTE.
3. Informe a URL do ADMIN, se solicitado.
4. Informe o codigo temporario.
5. Clique em:

      PAREAR CONFIGURACAO

6. Aguarde "Aguardando aprovacao do administrador".
7. Depois da aprovacao, aguarde a conclusao.
8. Nao feche o Client enquanto a credencial estiver sendo salva.

O codigo e temporario, expiravel e de uso unico. Se estiver errado, expirado ou
reutilizado, inicie outra configuracao no ADMIN. Nao tente adivinhar o codigo.

9. INSTALAR O AGENT
-------------------

O Agent e responsavel pelas politicas Windows reversiveis e pelas acoes
privilegiadas autorizadas. O Client nao deve rodar como Administrador.

 Somente se for necessario reparar manualmente, abra o PowerShell como
 Administrador na pasta `CLIENTE` publicada e execute:

   .\Install-ClientAgent.ps1 `
       -AgentDirectory 'C:\Program Files\Adrenalina'

O instalador tenta importar a credencial DPAPI do Client usando o mesmo usuario
Windows. Se necessario, informe manualmente uma SecureString:

   .\Install-ClientAgent.ps1 `
       -AgentDirectory 'C:\Program Files\Adrenalina' `
       -MachineSecret (Read-Host 'Segredo da estacao' -AsSecureString)

O servico criado chama-se:

   AdrenalinaClientAgent

Confirme em services.msc que o servico esta Automatico e em execucao.

10. PRIMEIRO SELF-CHECK
----------------------

Antes de liberar a estacao para usuarios, confira no ADMIN:

- Client online;
- Agent saudavel;
- versao do Client;
- versao do Agent;
- versao do protocolo;
- versao da politica;
- ultima comunicacao;
- estacao inicialmente bloqueada;
- nenhuma sessao inesperada;
- credencial nao revogada.

Se o Client estiver online e o Agent indisponivel, nao use a estacao
comercialmente. Corrija o Agent e repita o self-check.

11. OPERACAO NORMAL
------------------

Para iniciar uma sessao:

1. Confira a maquina correta no ADMIN.
2. O usuario informa login e PIN no CLIENTE.
3. O servidor valida usuario, PIN, saldo e sessao.
4. O Client recebe o estado autorizado.
5. O Agent aplica o estado permitido.

Ao terminar a sessao, o Server encerra, o Client recebe a atualizacao e o
Agent retorna a estacao ao estado bloqueado.

Comandos administrativos disponiveis:

- Bloquear;
- Liberar;
- Reiniciar;
- Desligar;
- Logoff;
- Entrar em manutencao;
- Sair da manutencao;
- Atualizar configuracao.

Reiniciar, desligar, logoff e revogar exigem confirmacao do operador.
Nao use cmd.exe, PowerShell, caminhos ou processos como comando remoto.

12. MANUTENCAO, RECOVERY E DESINSTALACAO
----------------------------------------

Antes de manutencao, registre operador, motivo e horario. Nao mantenha uma
estacao em manutencao sem responsavel.

Para restaurar as politicas do Agent em uma emergencia autorizada:

   .\Adrenalina.Agent.exe --recover

Para remover o Agent:

   .\deployment\Uninstall-ClientAgent.ps1 `
       -AgentExecutable 'C:\Program Files\Adrenalina\Adrenalina.Agent.exe'

Depois, confirme que o servico foi removido, o Registry foi restaurado, o
Desktop voltou ao estado anterior e nenhuma sessao comercial ficou ativa.

Para uma desinstalacao normal, abra a pasta ADMIN ou CLIENTE instalada e
execute:

   DESINSTALAR.bat

Esse arquivo remove os programas, o Agent, os atalhos e as regras de firewall,
mas preserva dados e backups. Ele solicita a permissao de Administrador sozinho.

Para apagar tambem dados, configuracoes, backups e o certificado configurado
pelo Adrenalina, execute somente quando tiver um backup externo:

   DESINSTALAR_COMPLETO.bat

O desinstalador completo usa o mesmo fluxo automatico, restaura as politicas
do Windows antes de remover o Agent e reinicia a maquina ao final.

13. TESTE DE CAMPO
------------------

Execute todos os itens de:

   docs\FIELD_TEST.md

Teste fechamento do Client, Alt+F4, Alt+Tab, tecla Windows, Win+R, Start,
taskbar, Explorer, aplicativo externo, Task Manager, Ctrl+Alt+Del, reboot,
desligar/ligar, rede desconectada, Server offline, crash do Client e restart
do Agent.

Ctrl+Alt+Del nao deve ser interceptado pelo produto; esse comportamento e
controlado pelo Windows e deve ser validado conforme a edicao instalada.

Registre separadamente CODE VERIFIED, WINDOWS VERIFIED e FIELD VERIFIED.
Sem teste real em Windows autorizado, nao declare production ready.

14. ARQUIVOS OBRIGATORIOS E AUXILIARES
--------------------------------------

Arquivos que devem permanecer junto dos executaveis ou na pasta publicada:

- arquivos .dll: bibliotecas usadas pelos programas; nao apagar;
- arquivos .json de configuracao: podem ser necessarios pelo Admin ou Client;
- Views, wwwroot e appsettings do servidor: necessarios para o painel web;
- arquivos .deps.json e .runtimeconfig.json: necessarios para iniciar o .NET;
- subpastas runtimes: necessarias em algumas publicacoes self-contained;
- arquivos de certificado publico: necessarios quando HTTPS exigir confianca;
- scripts deployment: usados para instalar, validar, atualizar e recuperar.

Arquivos importantes que ficam fora da pasta dos executaveis:

- adrenalina.db: banco do Admin;
- backups\: backups do banco;
- logs\: evidencias tecnicas da operacao;
- clientsettings.json: configuracao local do Client;
- machine-credential.dpapi: credencial protegida do Client;
- machine-secret.dpapi: credencial protegida do Agent;
- policy-backup.json: copia das politicas Windows alteradas pelo Agent.

Arquivos que podem ser dispensaveis, mas nao devem ser removidos sem saber a
origem:

- arquivos .pdb: simbolos de depuracao; normalmente nao sao necessarios para
  operar, mas podem ser importantes para diagnostico;
- arquivos .xml de documentacao: normalmente opcionais em runtime;
- arquivos .tmp: temporarios, desde que nenhum instalador ou programa esteja
  usando o arquivo;
- pastas .publish: somente staging interno do script e nao devem ir para o
  pacote final.

Nunca apague .dll, .deps.json, .runtimeconfig.json, runtimes, Views, wwwroot,
appsettings, banco, backups, logs ou arquivos .dpapi achando que sao lixo.
Nunca apague uma pasta inteira do Program Files para corrigir um erro. Use o
uninstaller ou o procedimento de recovery.

O arquivo initial-admin-access.txt e temporario. Depois que a senha inicial for
alterada, ele deve desaparecer; antes disso, trate-o como segredo. Se ele nao
existir ou a senha for perdida, abra o painel no proprio computador ADMIN e
clique em Recuperar acesso. O botao restaura admin admin, recria o arquivo e
desbloqueia o admin. A recuperacao nao fica disponivel pela rede.

15. DOCUMENTOS IMPORTANTES
--------------------------

   README.md
   ARCHITECTURE.md
   SECURITY.md
   OPERATIONS.md
   docs\INSTALLATION.md
   docs\FIELD_TEST.md
   DEPLOYMENT_CHECKLIST.md
   PRODUCTION_READINESS.md
   deployment\README.md

FIM DO README

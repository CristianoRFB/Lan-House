# Roadmap

O roadmap prioriza confiabilidade e implantação antes de novas funções. Itens marcados representam o estado desta auditoria, não uma release publicada.

## Versão 0.9 — estabilização técnica

- [x] separação operacional entre Admin, Client e Server;
- [x] banco e configurações isolados por aplicação;
- [x] SQLite com WAL, índices e sessão ativa única por máquina;
- [x] API local, health check, timeout e rate limiting;
- [x] fila offline com gravação atômica e idempotência;
- [x] redelivery de mensagens até confirmação do Client;
- [x] logs separados em `Admin.log`, `Client.log` e `Server.log`;
- [x] dependências sem advisories conhecidos de severidade alta;
- [x] suíte segura de integração sem controle do Windows;
- [x] interface alinhada às capacidades reais do produto.

## Versão 1.0 — produto comercial mínimo

- [ ] assistente obrigatório de primeiro acesso, sem senha fixa de produção;
- [ ] políticas de autorização para Admin e Operador Especial;
- [ ] credencial individual e rotacionável por máquina;
- [x] HTTPS obrigatório para exposição de produção na rede local, com configuração externa e pacote de certificado;
- [ ] migrations versionadas, teste de upgrade e estratégia de rollback;
- [x] verificação de integridade, cópia off-machine e restauração guiada por scripts de implantação;
- [ ] instalador assinado e validado no ambiente do cliente; pacote PowerShell de implantação e rollback incluído;
- [ ] limpeza versionada dos artefatos `bin`/`obj` antigos ainda rastreados no histórico;
- [ ] cobertura automatizada dos casos financeiros e de concorrência;
- [ ] teste de carga com quantidade-alvo de máquinas definida;
- [ ] acessibilidade e teste visual em 100%, 125% e 150% de escala do Windows;
- [ ] documentação operacional e política de suporte.

## Versão 1.1 — operação e gestão

- [ ] dashboard com filtros e indicadores operacionais validados;
- [ ] relatórios financeiros reconciliáveis;
- [ ] estatísticas por máquina, período e categoria;
- [ ] impressão de comprovantes;
- [ ] atualização segura, assinada e com rollback;
- [ ] exportação e retenção configurável de logs;
- [ ] alertas de disco, banco e backup;
- [ ] recuperação de acesso administrativo.

## Versão 1.2 — escala local

- [ ] testes com 50, 100 e 250 estações simultâneas;
- [ ] paginação e projeções em todas as consultas de histórico;
- [ ] cache somente onde métricas demonstrarem benefício;
- [ ] telemetria opt-in e anonimizada;
- [ ] filas persistentes versionadas;
- [ ] política de compatibilidade entre versões de Server e Client.

## Versão 2.0 — plataforma

- [ ] múltiplas lan houses por organização;
- [ ] banco remoto suportado;
- [ ] API pública versionada;
- [ ] aplicativo móvel;
- [ ] PIX e gateway de pagamento;
- [ ] administração central e operação offline por filial;
- [ ] trilha de auditoria imutável e retenção regulatória.

## Fora de escopo até decisão explícita

- bloqueio de teclado/mouse;
- encerramento de processos;
- alteração de firewall ou Registro;
- instalação de serviços;
- reinício, desligamento ou logoff remoto;
- captura de tela.

Esses recursos elevam risco jurídico, de segurança e de suporte e não devem retornar por reutilização acidental do código legado.

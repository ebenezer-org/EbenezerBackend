# AGENTS.md

Instrucoes para agentes e colaboradores que forem trabalhar neste repositorio.

## Contexto do Projeto

Este repositorio contem o backend do Ebenezer App, uma API em .NET para um produto real e tambem para um TCC. O estudo compara abordagens de persistencia, com uma etapa atual focada em CRUDs usando ArangoDB e uma etapa futura em uma branch separada usando um sistema poliglota heterogeneo de bancos de dados com Redis, Neo4j e MongoDB.

No momento, nao antecipe a arquitetura poliglota na branch atual. Implemente os CRUDs e fluxos com ArangoDB de forma clara, consistente e mensuravel. Mantenha as fronteiras de dados em repositories e services para que a futura branch de comparacao consiga trocar ou dividir persistencias sem reescrever regras de negocio.

## Proposito do App

O Ebenezer App existe para ajudar usuarios a lembrarem do amor, da presenca e das respostas de Deus aos pedidos de oracao. O produto deve incentivar gratidao, memoria espiritual e reflexao sobre a soberania de Deus.

O app nao deve incentivar teologia da prosperidade, triunfalismo materialista, manipulacao emocional, toxicidade ou qualquer comportamento abusivo. Qualquer funcionalidade social deve ser desenhada com tolerancia zero a toxicidade.

## Stack Atual

- Backend: ASP.NET Core / .NET 10.
- Persistencia atual: ArangoDB via `ArangoDBNetStandard`.
- Autenticacao: Identity/JWT conforme configuracao existente.
- Documentacao local em desenvolvimento: OpenAPI e Scalar.
- Containerizacao: Docker e `docker-compose.yaml`.
- A composicao da aplicacao fica em `Program.cs`, que registra ArangoDB, Identity, JWT, servicos compartilhados e modulos por feature via `Infrastructure/Extensions/ServiceCollection/ModulesExtensions.cs`; a inicializacao do banco roda antes do pipeline em `UseArangoDbInitialization()`.

Ao validar alteracoes, rode pelo menos:

```powershell
dotnet build
```

Se testes forem adicionados futuramente, rode tambem a suite de testes aplicavel.

## Arquitetura por Feature

Preserve a organizacao modular por feature:

```text
Features/
  <Feature>/
    Data/
      Models/
      <Feature>Repository.cs
    Domain/
      Entities/
      Exceptions/
      Repositories/
      Services/
        Interfaces/
        Implementations/
    Presentation/
      Dtos/
        <Endpoint>/
          <Endpoint>RequestDto.cs
          <Endpoint>ResponseDto.cs
      <Feature>Controller.cs
    <Feature>DependencyInjection.cs
```

Regras praticas:

- Controllers devem ser finos e delegar regras para services.
- Services devem conter orquestracao e regras de negocio da feature.
- Repositories devem conter acesso a banco, AQL e mapeamento de models.
- Domain entities nao devem depender de detalhes de banco ou HTTP.
- DTOs de Presentation sao contratos da API; DTOs de Repository sao contratos internos de persistencia.
- Cada nova feature deve registrar suas dependencias no proprio arquivo de dependency injection e ser integrada ao carregamento de modulos existente.
- Quando um service precisar do usuario autenticado, injete `Shared/Services/UserContext/IUserContext`; os services de `Categories`, `Prayers` e `Profile` usam esse contrato em vez de ler claims diretamente no controller.
- Novas features devem ser conectadas em `Infrastructure/Extensions/ServiceCollection/ModulesExtensions.cs`; servicos transversais vao em `Shared/Services/SharedServicesDependencyInjection.cs`.

## Diretrizes para ArangoDB

Na branch atual, use ArangoDB como fonte principal de persistencia.

- Use document collections para entidades principais como `Users`, `Prayers`, `Comments` e `Categories`.
- Use edge collections para relacionamentos como `PostedBy`, `CreatedCategory`, `CategorizedAs`, `Friendships` e `InteractsWith`.
- Prefira AQL com bind variables. Nunca concatene entrada do usuario diretamente em queries.
- Garanta que o shape retornado pela AQL corresponda exatamente ao DTO usado em `PostCursorAsync<T>()`.
- Models persistidos precisam ter membros publicos serializaveis. Use `[CollectionName("...")]` nos models e `Shared/BaseRepository<TDataModel>` quando a collection precisar ser resolvida a partir do atributo; propriedades privadas nao serao enviadas corretamente ao ArangoDB.
- Use `[JsonProperty("_key")]` ou `Infrastructure/Data/ArangoDbBaseModel.cs` quando for necessario mapear `_key`.
- Prefira datas em UTC para novos fluxos. Se alterar codigo existente que usa `DateTime.Now`, avalie impacto de compatibilidade.
- Em AQL, prefira sintaxe explicita e compativel com ArangoDB 3.12, por exemplo `INSERT ... INTO Collection`.
- Quando criar documentos e arestas no mesmo fluxo, retorne dados suficientes para o service montar a resposta sem nova query desnecessaria.

## Diretrizes para o Futuro Sistema Poliglota

A branch futura deve comparar ArangoDB com uma abordagem heterogenea envolvendo Redis, Neo4j e MongoDB. Para facilitar isso agora:

- Nao acople regras de negocio a AQL fora dos repositories.
- Evite espalhar nomes de colecoes pelo dominio ou presentation.
- Modele interfaces de repositories por caso de uso, nao por tecnologia.
- Nao introduza Redis, Neo4j ou MongoDB nesta branch sem pedido explicito.
- Registre decisoes que afetem comparacao futura, como modelagem de grafo, custo de queries, consistencia, complexidade operacional e latencia.

Possivel distribuicao futura, sujeita ao desenho do TCC:

- MongoDB: documentos principais e agregados de leitura.
- Neo4j: grafo social, amizades, conexoes, recomendacoes e relacionamentos complexos.
- Redis: cache, sessoes, rate limiting, filas simples ou contadores temporarios.
- ArangoDB: baseline multimodelo para comparacao com a abordagem heterogenea.

## Dominio da Aplicacao

### Perfis

- Um usuario deve ter perfil com nome, email, senha, telefone opcional e bio opcional.
- Senha e hashes sao dados sensiveis e nunca devem ser retornados em responses.
- Exponha apenas dados seguros do usuario em DTOs publicos.

### Pedidos de Oracao / Postagens

- Toda postagem representa um pedido de oracao.
- Toda postagem pertence a um usuario.
- O conteudo inicial e texto com suporte a markdown.
- Postagens podem ser publicas ou privadas.
- Postagens podem ter varias categorias.
- Privacidade deve ser respeitada em queries e services.

### Respostas de Oracao

- A resposta especial de oracao pertence ao dono da postagem.
- Ela pode ser classificada como positiva, negativa ou "aguarde".
- Essa resposta deve iniciar ou participar de uma thread associada ao pedido.
- Nao trate resposta negativa como falha espiritual; mensagens devem refletir soberania, cuidado e encorajamento.

### Comentarios e Reacoes

- Outros usuarios podem comentar em pedidos de oracao.
- Comentarios podem receber respostas.
- Postagens e comentarios podem receber reacoes.
- Reacoes e comentarios devem alimentar notificacoes e retrospectivas.

### Categorias

- Categorias servem para vincular e filtrar postagens.
- Usuarios podem criar categorias personalizadas com ids unicos.
- Descricoes de categorias personalizadas aparecem apenas no perfil do proprio usuario.

### Conexoes

- Usuarios podem solicitar amizade.
- Amizade so existe plenamente depois de aceita.
- Fluxos de privacidade devem considerar amizades aceitas.

### Notificacoes

Notifique usuarios quando:

- Suas postagens receberem interacoes.
- Seus comentarios receberem interacoes.
- Receberem solicitacao de amizade.
- Tiverem solicitacao de amizade aceita.
- Alguem adicionar uma resposta de oracao especial em uma postagem com a qual o usuario interagiu.

### Retrospectivas

Retrospectivas devem trazer informacoes relevantes e mensagens encorajadoras:

- Quantidade de oracoes feitas.
- Quantidade de oracoes respondidas e classificacao das respostas.
- Quantidade de amigos feitos.
- Quantidade de comentarios e reacoes recebidos em posts.
- Categorias mais e menos usadas em pedidos.
- Categorias mais e menos associadas a respostas.

Deve existir retrospectiva periodica configuravel com default e retrospectiva anual. Mensagens devem ser cuidadosas: se muitas respostas forem negativas, encoraje fe na soberania de Deus e reflexao, sem culpa, prosperidade materialista ou simplificacoes teologicas.

## Qualidade, Seguranca e Manutencao

- Preserve `Nullable` habilitado e evite suprimir warnings sem motivo.
- Nao retorne entidades de banco diretamente em endpoints se houver risco de vazar dados internos.
- Nunca registre secrets, senhas, tokens ou conteudo de `.env`.
- Use cancellation tokens em chamadas async quando disponivel.
- Nao reverta alteracoes do usuario sem pedido explicito.
- Ao corrigir bugs, procure a causa raiz, especialmente em serializacao, mapeamento DTO/model/entity e shape de AQL.
- Prefira nomes claros em portugues ou ingles conforme o padrao da area existente; nao misture sem necessidade dentro do mesmo contexto.
- Mantenha comentarios de codigo raros e objetivos.
- Excecoes aplicacionais devem herdar de `Shared/Exceptions/BaseException.cs`; `Infrastructure/Middleware/ExceptionHandlerMiddleware.cs` transforma essas excecoes em `ApiResponse<T>` JSON e mapeia os status codes de forma padronizada.

## Diretriz de Produto

Quando houver ambiguidade entre criar uma funcionalidade "social" generica e preservar o proposito do Ebenezer App, preserve o proposito. Este produto nao e uma rede social comum: interacoes, notificacoes, retrospectivas e mensagens devem apontar para gratidao, cuidado mutuo, memoria das oracoes e encorajamento cristao responsavel.

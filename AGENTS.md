# AGENTS.md

Instrucoes para agentes e colaboradores que forem trabalhar neste repositorio.

## Contexto do Projeto

Este repositorio contem o backend do Ebenezer App, uma API em .NET para um produto real e tambem para um TCC. O estudo compara abordagens de persistencia. A comparacao acontece em branches diferentes, cada uma implementando os mesmos casos de uso com uma estrategia de persistencia distinta:

- Abordagem ArangoDB: baseline multimodelo, usando um unico banco para documentos e grafo.
- Abordagem poliglota heterogenea (MongoDB + Neo4j): documentos em MongoDB e grafo social em Neo4j, com possibilidade futura de Redis para cache, sessoes e contadores.

> Identifique em qual branch/abordagem voce esta antes de implementar. Use a secao de diretrizes correspondente ("Diretrizes para a Abordagem ArangoDB" ou "Diretrizes para a Abordagem MongoDB + Neo4j"). Nao misture tecnologias de persistencia de abordagens diferentes na mesma branch sem pedido explicito.

Em qualquer abordagem, mantenha as fronteiras de dados em repositories e services para que cada branch consiga trocar ou dividir persistencias sem reescrever regras de negocio. As interfaces de repository sao modeladas por caso de uso, nao por tecnologia, o que permite trocar a implementacao de persistencia sem afetar o dominio.

## Proposito do App

O Ebenezer App existe para ajudar usuarios a lembrarem do amor, da presenca e das respostas de Deus aos pedidos de oracao. O produto deve incentivar gratidao, memoria espiritual e reflexao sobre a soberania de Deus.

O app nao deve incentivar teologia da prosperidade, triunfalismo materialista, manipulacao emocional, toxicidade ou qualquer comportamento abusivo. Qualquer funcionalidade social deve ser desenhada com tolerancia zero a toxicidade.

## Stack Atual

- Backend: ASP.NET Core / .NET 10.
- Persistencia: depende da abordagem/branch atual.
  - Abordagem ArangoDB: ArangoDB via `ArangoDBNetStandard`.
  - Abordagem MongoDB + Neo4j: MongoDB via `MongoDB.Driver` (documentos) e Neo4j via `Neo4j.Driver` (grafo social).
- Autenticacao: Identity/JWT conforme configuracao existente.
- Documentacao local em desenvolvimento: OpenAPI e Scalar.
- Containerizacao: Docker e `docker-compose.yaml`.
- A composicao da aplicacao fica em `Program.cs`, que registra a persistencia da abordagem atual, Identity, JWT, servicos compartilhados e modulos por feature via `Infrastructure/Extensions/ServiceCollection/ModulesExtensions.cs`.
  - Na abordagem ArangoDB, a inicializacao do banco roda antes do pipeline em `UseArangoDbInitialization()`.
  - Na abordagem MongoDB + Neo4j, os clientes sao registrados em `AddMongoDb()` e `AddNeo4JDb()` (ver `Infrastructure/Extensions/ServiceCollection/`).

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

## Diretrizes para a Abordagem ArangoDB

> Aplicaveis apenas na branch da abordagem ArangoDB.

Nesta abordagem, use ArangoDB como fonte unica de persistencia.

- Use document collections para entidades principais como `Users`, `Prayers`, `Comments` e `Categories`.
- Use edge collections para relacionamentos como `PostedBy`, `CreatedCategory`, `CategorizedAs`, `Friendships` e `InteractsWith`.
- Prefira AQL com bind variables. Nunca concatene entrada do usuario diretamente em queries.
- Garanta que o shape retornado pela AQL corresponda exatamente ao DTO usado em `PostCursorAsync<T>()`.
- Models persistidos precisam ter membros publicos serializaveis. Use `[CollectionName("...")]` nos models e `Shared/BaseRepository<TDataModel>` quando a collection precisar ser resolvida a partir do atributo; propriedades privadas nao serao enviadas corretamente ao ArangoDB.
- Use `[JsonProperty("_key")]` ou `Infrastructure/Data/ArangoDbBaseModel.cs` quando for necessario mapear `_key`.
- Prefira datas em UTC para novos fluxos. Se alterar codigo existente que usa `DateTime.Now`, avalie impacto de compatibilidade.
- Em AQL, prefira sintaxe explicita e compativel com ArangoDB 3.12, por exemplo `INSERT ... INTO Collection`.
- Quando criar documentos e arestas no mesmo fluxo, retorne dados suficientes para o service montar a resposta sem nova query desnecessaria.

## Diretrizes para a Abordagem MongoDB + Neo4j

> Aplicaveis apenas na branch da abordagem poliglota heterogenea (MongoDB + Neo4j).

Nesta abordagem, os dados sao divididos por natureza: documentos e agregados de leitura ficam no MongoDB, enquanto o grafo social (amizades, conexoes, visibilidade baseada em relacionamentos, recomendacoes) fica no Neo4j.

### Divisao de responsabilidades

- MongoDB: entidades documentais principais como `Users`, `Categories` e `Prayers`, incluindo dados embutidos no proprio documento (por exemplo, ids de categorias e reacoes de uma oracao).
- Neo4j: grafo social com nos `User` e arestas `FRIENDSHIP`. Usado para amizades, sugestoes, amigos em comum e para resolver visibilidade de conteudo privado.
- Um mesmo caso de uso pode tocar os dois bancos. Exemplo: o repositorio de oracoes le/escreve documentos no MongoDB e consulta amizades aceitas no Neo4j para decidir visibilidade de posts privados.

### MongoDB

- Injete `IMongoDatabase` no repositorio e obtenha as collections com `database.GetCollection<TModel>(...)`.
- Models persistidos usam `[CollectionName(DbCollections.X)]` e implementam `Infrastructure/Data/IBaseModel<TModel, TEntity>` com `ToEntity()` e `FromEntity(...)`. Resolva o nome da collection via `Shared/Data/BaseRepository<TModel>` (propriedade `CollectionName`).
- Use os atributos do driver oficial: `[BsonId]`, `[BsonRepresentation(BsonType.ObjectId)]` para chaves, `[BsonIgnoreExtraElements]` para tolerar campos extras.
- Use o `Builders<TModel>.Filter`, `.Update` e `.Sort` com expressoes tipadas; nunca concatene entrada do usuario em filtros. Para busca textual, escape o termo com `Regex.Escape(...)` antes de montar `BsonRegularExpression`.
- Prefira `FindOneAndUpdateAsync` com `ReturnDocument.After` quando precisar do documento atualizado. Para operacoes escopadas por dono, inclua o identificador do dono no filtro.
- Para evitar N+1, carregue dados relacionados em lote (por exemplo, perfis e categorias) e monte mapas em memoria antes de projetar os DTOs.

### Neo4j

- Injete `IDriver` e abra sessoes com `await using var session = driver.AsyncSession();`.
- Escreva Cypher com bind parameters (`$param`). Nunca interpole entrada do usuario na query.
- Models de aresta usam `[CollectionName(DbEdges.X)]` e implementam `Infrastructure/Data/INeo4JModel<TModel>` com `FromRecord(IRecord)`.
- Garanta que os alias retornados pela query (`RETURN x AS alias`) correspondam exatamente ao que o mapeamento de model/DTO espera.
- Converta datas com `Infrastructure/Data/Neo4JValueConverter` (`ToIso8601`, `ToDateTime`, `ToNullableDateTime`) para manter consistencia entre `string`/`LocalDateTime`/`ZonedDateTime`.
- Amizade so vale quando aceita (`f.acceptedAt IS NOT NULL`). Visibilidade de conteudo privado deve considerar amizades aceitas.

### Regras transversais

- Mantenha o acesso a banco dentro dos repositories; o dominio nao deve conhecer MongoDB nem Neo4j.
- Evite espalhar nomes de collections, edges ou labels pelo dominio ou presentation; use `Shared/Data/DbCollections.cs` e `Shared/Data/DbEdges.cs`.
- Prefira datas em UTC.
- Registre decisoes que afetem a comparacao do TCC, como modelagem de grafo, custo de queries cross-store, consistencia eventual entre os dois bancos, complexidade operacional e latencia.

## Comparacao entre as Abordagens (TCC)

Para que a comparacao seja justa, mantenha as mesmas regras de negocio e os mesmos contratos de API/DTO entre as branches. As diferencas devem se concentrar na camada de persistencia (repositories e models).

Possivel distribuicao da abordagem poliglota:

- MongoDB: documentos principais e agregados de leitura.
- Neo4j: grafo social, amizades, conexoes, recomendacoes e relacionamentos complexos.
- Redis (futuro, se necessario): cache, sessoes, rate limiting, filas simples ou contadores temporarios.
- ArangoDB (outra branch): baseline multimodelo para comparacao com a abordagem heterogenea.

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
- Ao corrigir bugs, procure a causa raiz, especialmente em serializacao, mapeamento DTO/model/entity e shape das queries (AQL no ArangoDB; aggregation/filtros do MongoDB e Cypher no Neo4j).
- Prefira nomes claros em portugues ou ingles conforme o padrao da area existente; nao misture sem necessidade dentro do mesmo contexto.
- Mantenha comentarios de codigo raros e objetivos.
- Excecoes aplicacionais devem herdar de `Shared/Exceptions/BaseException.cs`; `Infrastructure/Middleware/ExceptionHandlerMiddleware.cs` transforma essas excecoes em `ApiResponse<T>` JSON e mapeia os status codes de forma padronizada.

## Diretriz de Produto

Quando houver ambiguidade entre criar uma funcionalidade "social" generica e preservar o proposito do Ebenezer App, preserve o proposito. Este produto nao e uma rede social comum: interacoes, notificacoes, retrospectivas e mensagens devem apontar para gratidao, cuidado mutuo, memoria das oracoes e encorajamento cristao responsavel.

# NewLife.AI

<p align="center">
  <a href="https://www.nuget.org/packages/NewLife.AI"><img src="https://img.shields.io/nuget/v/NewLife.AI.svg" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/NewLife.AI"><img src="https://img.shields.io/nuget/dt/NewLife.AI.svg" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/.NET-netstandard2.1%20%7C%20net8.0%20%7C%20net10.0-blue" alt=".NET">
  <a href="https://github.com/NewLifeX/NewLife.AI/blob/main/LICENSE"><img src="https://img.shields.io/github/license/NewLifeX/NewLife.AI.svg" alt="License"></a>
</p>

<p align="center">
  <a href="README.en.md">English</a> |
  <a href="README.md">简体中文</a> |
  <a href="README.ja.md">日本語</a> |
  <a href="README.ko.md">한국어</a> |
  <a href="README.es.md">Español</a> |
  <a href="README.fr.md">Français</a> |
  <a href="README.de.md">Deutsch</a> |
  <a href="README.ru.md">Русский</a> |
  <b>Português</b>
</p>

## Introdução

**NewLife.AI** é uma **biblioteca de IA de código aberto** para o ecossistema .NET, fornecendo uma interface unificada `IChatClient` que encapsula **46 provedores principais de LLM**. Inclui chamadas de função integradas, protocolo MCP, saída em streaming, suporte multimodal, capacidades multiagente, e pode ser incorporada como pacote NuGet em qualquer projeto .NET (`net45 / netstandard2.1`).

**NewLife.ChatAI** é uma **aplicação web de chat completa** construída sobre NewLife.AI (ASP.NET Core), oferecendo um frontend de chat multimodelo pronto para uso, um gateway de IA unificado e evolução automática de memória. Pode ser implantada independentemente ou incorporada em projetos ASP.NET Core existentes.

---

## Funcionalidades Principais

- **46 Provedores de IA, 6 Protocolos**: OpenAI / Anthropic / Gemini / Tongyi DashScope / Ollama / AWS Bedrock — alterne com uma linha de código
- **Interface unificada `IChatClient`**: Alinhada com a especificação MEAI — turno único, streaming, chamadas de função, multimodal, tudo com uma única API
- **Chamadas de função (Ferramentas)**: Atributo `[ToolDescription]` gera automaticamente JSON Schema; loop multi-turno `ToolChatClient`; ferramentas integradas de busca / clima / tradução / web scraping / geolocalização IP
- **Suporte MCP bidirecional**: Cliente conecta-se a servidores MCP externos (stdio / HTTP SSE); Servidor expõe ferramentas locais como serviços MCP padrão
- **Kernel de chat completo**: Pipeline de 3 estágios `IChatHandler` (OnBefore → Execute → OnAfter); manipuladores integrados (ativação de habilidades / injeção de memória / persistência / estatísticas de uso / geração de títulos); `IChatFilter` plugável
- **Evolução da memória do usuário**: Extrai automaticamente 10 categorias de memórias estruturadas das conversas — quanto mais você conversa, melhor ele entende você
- **Gateway de IA unificado**: Compatível com protocolos OpenAI / Anthropic / Gemini; adaptação automática snake_case/camelCase; multilocação AppKey; retentativa com backoff exponencial 429
- **Sistema de habilidades**: Reutilização de prompts Markdown, referências recursivas `@`, ativação automática por palavras-chave
- **Multiagente**: `ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner`
- **Frontend Web React 19**: Streaming SSE + predefinições de chat + visualização Artifact em tempo real (HTML/SVG/Mermaid) + ramificação de conversa + visualização de chamadas de ferramentas + cronometragem de raciocínio + multimodal
- **Camada de evolução do conhecimento**: Extrai automaticamente conhecimento das conversas, constrói uma base de conhecimento pesquisável com navegação TOC e busca semântica vetorial
- **Síntese de voz TTS**: Suporte a DashScope TTS e CosyVoice V3.5; síntese de voz em streaming; API TTS dedicada para frontend
- **Incorporação / Busca vetorial**: HashTextEmbedder v2 integrado e armazenamento vetorial; vetorização de documentos e busca semântica
- **Aprimoramento multiagente**: Novos ReflectionAgent e ReviewAgent; decomposição de tarefas complexas com agregação paralela de subagentes
- **Pontos de verificação com intervenção humana**: Seleção humana em tempo real entre múltiplos caminhos de IA; pontos de decisão para grupos de múltiplas perguntas
- **Chamadas de ferramentas aprimoradas**: Propagação de contexto ToolCallContext; disjuntor de provedor de ferramentas; filtragem de visibilidade em nível de sessão; erros estruturados com sistema de permissões de três níveis
- **Evolução da memória**: Serviço de consolidação de memória com deduplicação/fusão/expiração; memória de aprendizado extrai automaticamente conhecimento estruturado das conversas

---

## Provedores de IA Suportados

46 provedores: **9 clientes de protocolo independente + 37 adaptadores compatíveis com OpenAI**.

### Implementações de protocolo independente (9)

| Provedor | Protocolo | Características |
|----------|----------|-----------------|
| OpenAI | ChatCompletions / Responses | Visão / Chamadas de função / Geração de imagens / Raciocínio o3 |
| DeepSeek | DeepSeek API | reasoning_content / Parâmetros exclusivos DeepSeek v4 |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| Alibaba Cloud | DashScope | qwen-plus / qwen-max / Omni multimodal / Files API |
| Azure AI | Azure OpenAI | URL de nome de implantação + api-key |
| Ollama | Ollama API | llama / deepseek / qwen local |
| AWS Bedrock | Assinatura SigV4 | Claude / Llama / Titan / Mistral |
| NewLifeAI | Proxy em cascata | Agregação multiprovedor |
| (Outros) | Compatível OpenAI | 37 plataformas compatíveis |

### Família compatível com OpenAI (37)

Doubao (Volcano Engine), Zhipu Qingyan (GLM), Wenxin Yiyan, Moonshot (Kimi), MiniMax, StepFun, Baichuan, iFlytek Spark, 01.AI, Mistral, Perplexity, Cohere, Together AI, Fireworks, OpenRouter, SiliconCloud, DeepInfra, Groq, Cerebras, Hyperbolic, Nebius, Novita, Lepton, 302.AI, xAI (Grok)… e outras plataformas compatíveis com OpenAI.

Todos os provedores são declarados via atributo `[AiClient]`; `AiClientRegistry` escaneia e registra automaticamente na inicialização — configuração zero para novos provedores.

---

## Início Rápido

### 1. Apenas SDK de IA

```bash
dotnet add package NewLife.AI
```

```csharp
using NewLife.AI.Clients;

// Chat de turno único
using var client = new DashScopeChatClient("your-api-key", "qwen-plus");
var reply = await client.ChatAsync("Explique modelos de linguagem grandes em três frases");
Console.WriteLine(reply);

// Mensagens multi-função (array de tuplas, sem construção manual de ChatMessage)
var reply2 = await client.ChatAsync([
    ("system", "Você é um assistente profissional de desenvolvimento C#"),
    ("user", "Explique a diferença entre ValueTask e Task"),
]);

// Saída em streaming
await foreach (var chunk in client.GetStreamingResponseAsync([
    new ChatMessage { Role = "user", Content = "Escreva um poema curto sobre código" }
], new ChatOptions()))
{
    Console.Write(chunk.Text);
}
```

### 2. Injeção de dependência ASP.NET Core

```bash
dotnet add package NewLife.AI.Extensions
```

```csharp
// Registrar serviço
builder.Services.AddDashScope("your-api-key", "qwen-plus");

// Coexistência multiprovedor com Keyed
builder.Services.AddOpenAI("openai-key", serviceKey: "openai");
builder.Services.AddAnthropic("anthropic-key", serviceKey: "anthropic");

// Injetar e usar
public class MyService(IChatClient chatClient)
{
    public Task<String> ChatAsync(String question)
        => chatClient.ChatAsync(question);
}
```

### 3. Chamadas de função (Ferramentas)

```csharp
public class MyTools
{
    /// <summary>Obter clima de uma cidade especificada</summary>
    [ToolDescription("get_weather")]
    public async Task<String> GetWeatherAsync(
        [Description("Nome da cidade")] String city)
        => $"{city}: Ensolarado, 22°C hoje";
}

// Registrar ferramentas
var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// Adicionar ao pipeline — loop automático de chamadas de ferramentas multi-turno
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// Modelo chama automaticamente get_weather("Pequim"), retorna resposta final
var reply = await client.ChatAsync("Como está o tempo em Pequim hoje?");
```

### 4. Raciocínio restrito (Padrão ReAct)

Quando a IA tende a adivinhar ou tomar atalhos em problemas complexos, use o **formato ReAct** no System Prompt para forçar o raciocínio passo a passo. `ToolChatClient` tem um loop `while` integrado (`MaxIterations = 10`) — não é necessário escrever um Agent manualmente.

```csharp
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

var reply = await client.ChatAsync([
    ("system",
        "Você deve raciocinar passo a passo no seguinte formato. Não pule etapas nem adivinhe diretamente:\n" +
        "Thought: <analise o que precisa ser feito>\n" +
        "Action: <qual ferramenta chamar e parâmetros>\n" +
        "Observation: <analise o resultado da ferramenta>\n" +
        "(Repita Thought/Action/Observation até ter informações suficientes)\n" +
        "Answer: <conclusão final baseada nas observações acima>\n\n" +
        "Regra: Não dê uma Answer sem antes chamar uma ferramenta."),
    ("user", "O clima em Pequim hoje está adequado para esportes ao ar livre?"),
]);
```

### 5. Executar a aplicação web de chat completa

```bash
git clone https://github.com/NewLifeX/NewLife.AI.git
cd NewLife.AI

# Construir frontend (requer Node.js + pnpm)
cd Web && pnpm install && pnpm build && cd ..

# Iniciar
cd NewLife.ChatAI
dotnet run --framework net8.0
```

Abra `http://localhost:5000` no navegador — SQLite por padrão, pronto para uso. Na primeira execução, configure as chaves API dos provedores via `/Admin`.

Você também pode incorporar `NewLife.ChatAI` em um projeto existente via NuGet:

```bash
dotnet add package NewLife.ChatAI
```

```csharp
using NewLife.ChatAI;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddChatAI();

var app = builder.Build();
app.UseChatAI(redirectToChat: true);
app.Run();
```

---

## Gateway de API

NewLife.ChatAI inclui um gateway de IA multiprotocolo integrado. Sistemas de terceiros podem conectar-se sem modificações — todos os caminhos passam por injeção de memória e aprimoramento de habilidades.

| Protocolo | Rota | Descrição |
|-----------|------|-----------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | Streaming / não streaming / chamadas de função / visão |
| OpenAI Responses | `POST /v1/responses` | Modelos de raciocínio o3 / gpt-5 |
| Anthropic Messages | `POST /v1/messages` | Série Claude |
| Google Gemini | `POST /v1/gemini/...` | Série Gemini |
| Geração de imagens | `POST /v1/images/generations` | Text-to-Image |
| Edição de imagens | `POST /v1/images/edits` | Inpainting (multipart/form-data) |
| Descoberta de modelos | `GET /v1/models` | Lista de modelos disponíveis |

**Autenticação**: `Authorization: Bearer sk-xxxx` (AppKey)

**Características**: Retentativa com backoff exponencial 429 (jitter aleatório, máximo 5 tentativas); registro automático de uso de tokens; estatísticas bidimensionais por AppKey + Usuário

---

## Desenvolvimento de Extensões

### Adicionar um novo provedor de IA

Herde de `OpenAIChatClient`, adicione o atributo `[AiClient]` — `AiClientRegistry` escaneia automaticamente na inicialização:

```csharp
[AiClient("MyAI", "Meu Serviço", "https://api.myai.com/v1",
    Description = "Serviço de IA personalizado")]
[AiClientModel("myai-latest", "MyAI Latest", Code = "MyAI",
    FunctionCalling = true, Vision = true)]
public class MyAiChatClient : OpenAIChatClient
{
    public MyAiChatClient() { }
    public MyAiChatClient(String apiKey, String? model = null, String? endpoint = null)
        : base(apiKey, model, endpoint) { }
}
```

### Adicionar uma nova ferramenta

```csharp
public class MyTools
{
    /// <summary>Consultar hora atual</summary>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());
```

### Adicionar um novo IChatHandler

```csharp
[ChatHandlerOrder(150)]
public class CurrentTimeHandler : ChatHandlerBase
{
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        context.ContextMessages.Insert(0,
            new ChatMessage { Role = "system", Content = $"Hora atual: {DateTime.Now:yyyy-MM-dd HH:mm}" });
        return Task.CompletedTask;
    }
}

services.AddSingleton<IChatHandler, CurrentTimeHandler>();
```

---

## Documentação

| Documento | Descrição |
|-----------|-----------|
| [Especificação de requisitos](Doc/需求规格说明.md) | Objetivos do produto, lista de funcionalidades, requisitos não funcionais |
| [Design de arquitetura](Doc/架构设计.md) | Arquitetura de 4 camadas, detalhes de design dos módulos |
| [Framework de orquestração de IA](Doc/AI编排框架需求.md) | Design de ferramentas / agentes / planejador |
| [Requisitos do API Gateway](Doc/API网关需求.md) | Detalhes de adaptação de protocolos do gateway |
| [Arquitetura MCP](Doc/MCP架构.md) | Design de cliente e servidor MCP |
| [Gerenciamento de habilidades](Doc/技能管理需求.md) | Design detalhado do sistema de habilidades |
| [Sistema de autoaprendizagem](Doc/自学习系统需求.md) | Análise de conversas + extração de memória |
| [Fluxo de persistência de dados](Doc/对话数据保存流程.md) | Processo detalhado do MessageFlow |
| [Lista de módulos funcionais](Doc/功能模块清单.md) | Inventário completo de funcionalidades |

---

## Mapa do ecossistema

NewLife é um ecossistema em camadas de projetos open source versionados de forma independente. O mapa abaixo é compartilhado literalmente por todos os repositórios NewLife; cada projeto tem links para o seu repositório e para o seu pacote principal no NuGet.

| Projeto | O que faz | Downloads |
| --- | --- | --- |
| **Base** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Log, configuração, cache, rede, serialização, agendamento e rastreamento APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Middleware de dados: tabelas únicas na escala de dezenas de bilhões de linhas, particionamento automático, separação de leitura e escrita, mais de 10 bancos de dados. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Biblioteca de rede de alto desempenho: 1,77M pps / 14,5 Gbps em uma máquina (TCP 1KB, 2026-10), 4M de conexões TCP persistentes. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Framework de comunicação por protocolo e RPC para aplicações cliente/servidor e dispositivos IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Instala aplicações .NET como daemons do sistema operacional: serviços do Windows e systemd do Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Modelos de projeto `dotnet new` para console, web, API, serviço, dados e IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Dados e mensagens** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Cliente Redis com latência em microssegundos, milhões de operações por segundo e filas de mensagens integradas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Cliente RocketMQ totalmente gerenciado para Apache RocketMQ e Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Cliente e servidor MQTT, incluindo conectividade com o Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Banco de dados híbrido em C#: embarcado ou em rede, relacional, séries temporais, fila de mensagens e KV em um só. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Driver MySql e camada auxiliar construídos sobre a pilha de dados NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, IA e distribuído** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Framework de plataforma administrativa rápida: permissões, SSO, servidor OAuth e scaffolding CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Plataforma de serviços distribuídos: gestão de nós, APM, centro de configuração, registro e centro de publicação. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plataforma distribuída de tarefas e agendamento de dados em tempo real, com particionamento, repetição e reprocessamento. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Cliente unificado para 46 provedores de LLM, além do ChatAI, uma aplicação web de chat pronta para uso. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrial e IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Biblioteca padrão de IoT que define contratos de protocolo e abstrações de dispositivos. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Implementação Modbus TCP/RTU/ASCII sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Protocolo Siemens PLC sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Protocolo Omron PLC sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Protocolo Mitsubishi Melsec PLC sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Cliente OPC para acesso a dados industriais. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Pilha do protocolo de automação predial BACnet. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Plataforma IoT de código aberto leve, com gestão de gateways e dispositivos. | — |
| **Telemática veicular** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Protocolo de posicionamento veicular JT/T 808: mais de 70 corpos de mensagem, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Protocolo plataforma-a-plataforma JT/T 809 para autoridades de transporte. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Vídeo veicular JT/T 1078: análise RTP, remux para FLV/fMP4 e transmissão ao vivo. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Componentes de domínio** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Um pacote para Excel, Word, PPT, PDF e mais de 15 formatos de documento. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Áudio multiplataforma: codecs, E/S de dispositivos, DSP, contêineres e streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Codecs de vídeo, formatos de contêiner, processamento de frames e streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Interface unificada sobre Baidu, AMap, Tencent e Tianditu, com geocodificação e conversão de coordenadas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Geolocalização IPv4 com banco de dados offline mapeado em memória e API online como alternativa. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Ferramentas e produtos** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Caixa de ferramentas do desenvolvedor: rede, porta serial, criptografia, regex, Modbus e MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Sistema auto-hospedado de gerenciamento de código-fonte Git. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Instala automaticamente o runtime .NET correspondente quando iniciado em um host mais antigo. | — |

> Os badges mostram o pacote principal de cada projeto; pacotes de tema, extensão e satélites estão listados nas respectivas páginas do NuGet.Os mais de 30 pacotes não listados aqui (FTP, LoRa, extensões do Office, drivers de protocolo e outros) estão disponíveis na [organização NewLifeX](https://github.com/NewLifeX) e em [newlifex.com](https://newlifex.com).

## Equipe

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Fundada em 2002, a equipe NewLife dedica-se há mais de duas décadas ao ecossistema .NET e de IoT, mantém cerca de 100 projetos de código aberto (34 deles principais) e oferece soluções integradas de software e hardware para os setores de energia, logística, saúde, controle industrial e internet. Seguimos o lema "não há hierarquia no aprendizado — o domínio conquista o respeito".

Os componentes de código aberto já foram baixados <!--nuget-total:start-->6.48M<!--nuget-total:end--> vezes no NuGet (todo o ecossistema, até 2026-10).

Website: https://newlifex.com · Organização de código aberto: https://github.com/NewLifeX

<details>
<summary>Conta oficial no WeChat: 智能大石头 (escaneie para seguir)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Licença

[Licença MIT](LICENSE)

Issues e Pull Requests são bem-vindos.

- GitHub: https://github.com/NewLifeX/NewLife.AI
- Site: https://newlifex.com
- Grupo QQ: 1600800

## Projetos Relacionados

- [NewLife.Core](https://github.com/NewLifeX/X) — Biblioteca base .NET
- [XCode](https://github.com/NewLifeX/X) — Framework ORM de dados
- [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) — Plataforma de desenvolvimento rápido

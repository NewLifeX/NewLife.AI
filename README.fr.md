# NewLife.AI

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.AI?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.AI?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.AI?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.AI?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.AI/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.AI/actions/workflows/test.yml)
[![NativeAOT](https://github.com/NewLifeX/NewLife.AI/actions/workflows/aot.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.AI/actions/workflows/aot.yml)
<!-- metrics:start -->
[![AOT smoke](https://img.shields.io/badge/AOT%20smoke-35%2F35-brightgreen?logo=dotnet)](Doc/AOT兼容性分析.md)
[![TFM](https://img.shields.io/badge/TFM-5%20%28net45%20%E2%86%92%20net10.0%29-512BD4?logo=dotnet)](https://www.nuget.org/packages/NewLife.AI)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
<!-- metrics:end -->
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20x86%20%7C%20arm64%20%7C%20arm%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.AI?logo=github)](https://github.com/NewLifeX/NewLife.AI/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ Cette traduction peut être en retard ; la référence est le [English README](README.md).


## Introduction

**NewLife.AI** est une **bibliothèque d'IA open source** pour l'écosystème .NET, fournissant une interface unifiée `IChatClient` qui encapsule **46 fournisseurs majeurs de LLM**. Elle intègre les appels de fonctions, le protocole MCP, la sortie en streaming, le support multimodal, les capacités multi-agents, et peut être intégrée en tant que package NuGet dans tout projet .NET (`net45 → net10.0`).

**NewLife.ChatAI** est une **application web de chat complète** construite sur NewLife.AI (ASP.NET Core), offrant un frontend de chat multi-modèle prêt à l'emploi, une passerelle AI unifiée et une évolution automatique de la mémoire. Elle peut être déployée indépendamment ou intégrée dans des projets ASP.NET Core existants.

---

## Fonctionnalités Principales

- **46 fournisseurs d'IA, 6 protocoles** : OpenAI / Anthropic / Gemini / Tongyi DashScope / Ollama / AWS Bedrock — changez avec une ligne de code
- **Interface unifiée `IChatClient`** : Alignée sur la spécification MEAI — tour unique, streaming, appels de fonctions, multimodal, le tout avec une seule API
- **Appels de fonctions (Outils)** : L'attribut `[ToolDescription]` génère automatiquement le JSON Schema ; boucle multi-tour `ToolChatClient` ; outils intégrés de recherche / météo / traduction / web scraping / géolocalisation IP
- **Support MCP bidirectionnel** : Le client se connecte aux serveurs MCP externes (stdio / HTTP SSE) ; le serveur expose les outils locaux en tant que services MCP standard
- **Noyau de chat complet** : Pipeline en 3 étapes `IChatHandler` (OnBefore → Execute → OnAfter) ; gestionnaires intégrés (activation de compétences / injection de mémoire / persistance / statistiques d'utilisation / génération de titres) ; `IChatFilter` enfichable
- **Évolution de la mémoire utilisateur** : Extrait automatiquement 10 catégories de souvenirs structurés des conversations — plus vous discutez, mieux il vous comprend
- **Passerelle AI unifiée** : Compatible avec les protocoles OpenAI / Anthropic / Gemini ; adaptation automatique snake_case/camelCase ; multi-location AppKey ; nouvelle tentative avec backoff exponentiel 429
- **Système de compétences** : Réutilisation de prompts Markdown, références récursives `@`, activation automatique par mots-clés
- **Multi-agent** : `ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner`
- **Frontend Web React 19** : Streaming SSE + préréglages de chat + aperçu Artifact en temps réel (HTML/SVG/Mermaid) + bifurcation de conversation + visualisation des appels d'outils + chronométrage du raisonnement + multimodal
- **Couche d'évolution des connaissances** : Extrait automatiquement les connaissances des conversations, construit une base de connaissances consultable avec navigation TOC et recherche sémantique vectorielle
- **Synthèse vocale TTS** : Support DashScope TTS et CosyVoice V3.5 ; synthèse vocale en streaming ; API TTS dédiée pour le frontend
- **Embedding / Recherche vectorielle** : HashTextEmbedder v2 intégré et stockage vectoriel ; vectorisation de documents et recherche sémantique
- **Amélioration multi-agent** : Nouveaux ReflectionAgent et ReviewAgent ; décomposition de tâches complexes avec agrégation parallèle de sous-agents
- **Points de contrôle avec intervention humaine** : Sélection humaine en temps réel parmi les chemins multiples de l'IA ; points de décision pour groupes de questions multiples
- **Appels d'outils améliorés** : Propagation de contexte ToolCallContext ; disjoncteur de fournisseur d'outils ; filtrage de visibilité au niveau session ; erreurs structurées avec système de permissions à trois niveaux
- **Évolution de la mémoire** : Service de consolidation de mémoire avec déduplication/fusion/expiration ; la mémoire d'apprentissage extrait automatiquement les connaissances structurées

---

## Fournisseurs d'IA Supportés

46 fournisseurs : **9 clients de protocole indépendant + 37 adaptateurs compatibles OpenAI**.

### Implémentations de protocole indépendant (9)

| Fournisseur | Protocole | Caractéristiques |
|-------------|-----------|------------------|
| OpenAI | ChatCompletions / Responses | Vision / Appels de fonctions / Génération d'images / Raisonnement o3 |
| DeepSeek | DeepSeek API | reasoning_content / Paramètres exclusifs DeepSeek v4 |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| Alibaba Cloud | DashScope | qwen-plus / qwen-max / Omni multimodal / Files API |
| Azure AI | Azure OpenAI | URL de nom de déploiement + api-key |
| Ollama | Ollama API | llama / deepseek / qwen local |
| AWS Bedrock | Signature SigV4 | Claude / Llama / Titan / Mistral |
| NewLifeAI | Proxy en cascade | Agrégation multi-fournisseur |
| (Autres) | Compatible OpenAI | 37 plateformes compatibles |

### Famille compatible OpenAI (37)

Doubao (Volcano Engine), Zhipu Qingyan (GLM), Wenxin Yiyan, Moonshot (Kimi), MiniMax, StepFun, Baichuan, iFlytek Spark, 01.AI, Mistral, Perplexity, Cohere, Together AI, Fireworks, OpenRouter, SiliconCloud, DeepInfra, Groq, Cerebras, Hyperbolic, Nebius, Novita, Lepton, 302.AI, xAI (Grok)… et autres plateformes compatibles OpenAI.

Tous les fournisseurs sont déclarés via l'attribut `[AiClient]` ; `AiClientRegistry` scanne et enregistre automatiquement au démarrage — configuration zéro pour les nouveaux fournisseurs.

---

## Démarrage Rapide

### 1. SDK AI uniquement

```bash
dotnet add package NewLife.AI
```

```csharp
using NewLife.AI.Clients;

// Chat à tour unique
using var client = new DashScopeChatClient("your-api-key", "qwen-plus");
var reply = await client.ChatAsync("Expliquez les grands modèles de langage en trois phrases");
Console.WriteLine(reply);

// Messages multi-rôles (tableau de tuples, sans construction manuelle de ChatMessage)
var reply2 = await client.ChatAsync([
    ("system", "Vous êtes un assistant de développement C# professionnel"),
    ("user", "Expliquez la différence entre ValueTask et Task"),
]);

// Sortie en streaming
await foreach (var chunk in client.GetStreamingResponseAsync([
    new ChatMessage { Role = "user", Content = "Écrivez un court poème sur le code" }
], new ChatOptions()))
{
    Console.Write(chunk.Text);
}
```

### 2. Injection de dépendances ASP.NET Core

```bash
dotnet add package NewLife.AI.Extensions
```

```csharp
// Enregistrer le service
builder.Services.AddDashScope("your-api-key", "qwen-plus");

// Coexistence multi-fournisseur avec Keyed
builder.Services.AddOpenAI("openai-key", serviceKey: "openai");
builder.Services.AddAnthropic("anthropic-key", serviceKey: "anthropic");

// Injecter et utiliser
public class MyService(IChatClient chatClient)
{
    public Task<String> ChatAsync(String question)
        => chatClient.ChatAsync(question);
}
```

### 3. Appels de fonctions (Outils)

```csharp
public class MyTools
{
    /// <summary>Obtenir la météo d'une ville spécifiée</summary>
    [ToolDescription("get_weather")]
    public async Task<String> GetWeatherAsync(
        [Description("Nom de la ville")] String city)
        => $"{city} : Ensoleillé, 22°C aujourd'hui";
}

// Enregistrer les outils
var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// Ajouter au pipeline — boucle automatique d'appels d'outils multi-tour
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// Le modèle appelle automatiquement get_weather("北京"), renvoie la réponse finale
var reply = await client.ChatAsync("Quel temps fait-il à Pékin aujourd'hui ?");
```

### 4. Raisonnement contraint (Pattern ReAct)

Lorsque l'IA a tendance à deviner ou à prendre des raccourcis sur des problèmes complexes, utilisez le **format ReAct** dans le System Prompt pour forcer le raisonnement étape par étape. `ToolChatClient` a une boucle `while` intégrée (`MaxIterations = 10`), pas besoin d'écrire un Agent manuellement.

```csharp
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

var reply = await client.ChatAsync([
    ("system",
        "Vous devez raisonner étape par étape dans le format suivant. Ne sautez pas d'étapes et ne devinez pas directement :\n" +
        "Thought: <analysez ce que vous devez faire>\n" +
        "Action: <quel outil appeler et paramètres>\n" +
        "Observation: <analysez le résultat de l'outil>\n" +
        "(Répétez Thought/Action/Observation jusqu'à avoir suffisamment d'informations)\n" +
        "Answer: <conclusion finale basée sur les observations ci-dessus>\n\n" +
        "Règle : Ne donnez pas d'Answer sans d'abord appeler un outil."),
    ("user", "Le temps à Pékin aujourd'hui est-il adapté aux sports de plein air ?"),
]);
```

### 5. Exécuter l'application web de chat complète

```bash
git clone https://github.com/NewLifeX/NewLife.AI.git
cd NewLife.AI

# Construire le frontend (nécessite Node.js + pnpm)
cd Web && pnpm install && pnpm build && cd ..

# Démarrer
cd NewLife.ChatAI
dotnet run --framework net8.0
```

Ouvrez `http://localhost:5000` dans votre navigateur — SQLite par défaut, prêt à l'emploi. Au premier démarrage, configurez les clés API des fournisseurs via `/Admin`.

Vous pouvez également intégrer `NewLife.ChatAI` dans un projet existant via NuGet :

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

## Passerelle API

NewLife.ChatAI inclut une passerelle AI multi-protocole intégrée. Les systèmes tiers peuvent se connecter sans modification — tous les chemins passent par l'injection de mémoire et l'amélioration des compétences.

| Protocole | Route | Description |
|-----------|-------|-------------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | Streaming / non-streaming / appels de fonctions / vision |
| OpenAI Responses | `POST /v1/responses` | Modèles de raisonnement o3 / gpt-5 |
| Anthropic Messages | `POST /v1/messages` | Série Claude |
| Google Gemini | `POST /v1/gemini/...` | Série Gemini |
| Génération d'images | `POST /v1/images/generations` | Text-to-Image |
| Édition d'images | `POST /v1/images/edits` | Inpainting (multipart/form-data) |
| Découverte de modèles | `GET /v1/models` | Liste des modèles disponibles |

**Authentification** : `Authorization: Bearer sk-xxxx` (AppKey)

**Caractéristiques** : Nouvelle tentative avec backoff exponentiel 429 (jitter aléatoire, max 5 tentatives) ; enregistrement automatique de l'utilisation des tokens ; statistiques bidimensionnelles par AppKey + Utilisateur

---

## Développement d'Extensions

### Ajouter un nouveau fournisseur d'IA

Héritez de `OpenAIChatClient`, ajoutez l'attribut `[AiClient]` — `AiClientRegistry` scanne automatiquement au démarrage :

```csharp
[AiClient("MyAI", "Mon Service", "https://api.myai.com/v1",
    Description = "Service d'IA personnalisé")]
[AiClientModel("myai-latest", "MyAI Latest", Code = "MyAI",
    FunctionCalling = true, Vision = true)]
public class MyAiChatClient : OpenAIChatClient
{
    public MyAiChatClient() { }
    public MyAiChatClient(String apiKey, String? model = null, String? endpoint = null)
        : base(apiKey, model, endpoint) { }
}
```

### Ajouter un nouvel outil

```csharp
public class MyTools
{
    /// <summary>Obtenir l'heure actuelle</summary>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());
```

### Ajouter un nouveau IChatHandler

```csharp
[ChatHandlerOrder(150)]
public class CurrentTimeHandler : ChatHandlerBase
{
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        context.ContextMessages.Insert(0,
            new ChatMessage { Role = "system", Content = $"Heure actuelle : {DateTime.Now:yyyy-MM-dd HH:mm}" });
        return Task.CompletedTask;
    }
}

services.AddSingleton<IChatHandler, CurrentTimeHandler>();
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [Spécification des exigences](Doc/需求规格说明.md) | Objectifs du produit, liste des fonctionnalités, exigences non fonctionnelles |
| [Conception de l'architecture](Doc/架构设计.md) | Architecture 4 couches, détails de conception des modules |
| [Framework d'orchestration AI](Doc/AI编排框架需求.md) | Conception des outils / agents / planificateur |
| [Exigences de la passerelle API](Doc/API网关需求.md) | Détails d'adaptation des protocoles |
| [Architecture MCP](Doc/MCP架构.md) | Conception du client et du serveur MCP |
| [Gestion des compétences](Doc/技能管理需求.md) | Conception détaillée du système de compétences |
| [Système d'auto-apprentissage](Doc/自学习系统需求.md) | Analyse de conversation + extraction de mémoire |
| [Flux de persistance des données](Doc/对话数据保存流程.md) | Processus détaillé de MessageFlow |
| [Liste des modules fonctionnels](Doc/功能模块清单.md) | Inventaire complet des fonctionnalités |

---

## Carte de l'écosystème

NewLife est un écosystème en couches de projets open source versionnés indépendamment. La carte ci-dessous est reprise à l'identique par tous les dépôts NewLife ; chaque projet renvoie à son dépôt et à son paquet NuGet principal.

| Projet | Rôle | Téléchargements |
| --- | --- | --- |
| **Fondation** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Journalisation, configuration, cache, réseau, sérialisation, ordonnancement et traçage APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Intergiciel de données : tables uniques à l'échelle de dizaines de milliards, partitionnement automatique, séparation lecture/écriture et plus de 10 bases de données. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Bibliothèque réseau hautes performances : 1,77 M pps / 14,5 Gbps sur une machine (TCP 1KB, 2026-10), 4 M connexions TCP persistantes. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Communication protocolaire et framework RPC pour applications client/serveur et appareils IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Installe des applications .NET comme démons système : services Windows et systemd Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Modèles de projet `dotnet new` pour console, web, API, service, données et IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Données et messagerie** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Client Redis avec une latence en microsecondes, des millions d'opérations/s et des files de messages intégrées. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Client RocketMQ entièrement managé pour Apache RocketMQ et Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Client et serveur MQTT, y compris la connectivité Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Base de données hybride en C# : embarquée ou en réseau, relationnelle, séries temporelles, file de messages et KV réunis. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Pilote et couche d'assistance MySql construits sur la pile de données NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, IA et distribué** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Framework rapide de plateforme d'administration : permissions, SSO, serveur OAuth et échafaudage CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Plateforme de services distribués : gestion des nœuds, APM, centre de configuration, registre et centre de publication. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plateforme de tâches distribuées et d'ordonnancement de données en temps réel avec partitionnement, reprises et backfill. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Client unifié pour 46 fournisseurs de LLM, plus ChatAI, une application web de chat prête à l'emploi. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industriel et IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Bibliothèque standard IoT définissant les contrats de protocole et les abstractions d'appareils. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Implémentation Modbus TCP/RTU/ASCII au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Protocole pour automate Siemens au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Protocole pour automate Omron au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Protocole pour automate Mitsubishi Melsec au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Client OPC pour l'accès aux données industrielles. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Pile de protocoles BACnet pour l'automatisation des bâtiments. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Plateforme IoT open source légère avec gestion des passerelles et des appareils. | — |
| **Télématique embarquée** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Protocole de positionnement de véhicules JT/T 808 : plus de 70 corps de message, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Protocole plateforme à plateforme JT/T 809 pour les autorités de transport. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Vidéo embarquée JT/T 1078 : analyse RTP, remuxage FLV/fMP4 et diffusion en direct. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Composants métier** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Un seul paquet pour Excel, Word, PPT, PDF et plus de 15 formats de documents. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Audio multiplateforme : codecs, E/S d'appareils, DSP, conteneurs et streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Codecs vidéo, formats de conteneurs, traitement des images et streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Interface unifiée pour Baidu, AMap, Tencent et Tianditu avec géocodage et conversion de coordonnées. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Géolocalisation IPv4 avec une base de données hors ligne mappée en mémoire et une API en ligne en secours. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Outils et produits** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Boîte à outils du développeur : réseau, port série, cryptographie, regex, Modbus et MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Système de gestion de code source Git auto-hébergé. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Installe automatiquement le runtime .NET correspondant lorsqu'il est lancé depuis un hôte plus ancien. | — |

> Les badges indiquent le paquet principal de chaque projet ; les paquets de thèmes, d'extensions et satellites sont répertoriés sur leurs pages NuGet.Les plus de 30 paquets non listés ici (FTP, LoRa, extensions Office, pilotes de protocoles, etc.) sont disponibles dans l'[organisation NewLifeX](https://github.com/NewLifeX) et sur [newlifex.com](https://newlifex.com).

## Équipe

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

NewLife a été fondée en 2002 et œuvre depuis plus de deux décennies dans l'écosystème .NET et IoT, maintient près de 100 projets open source (dont 34 principaux) et fournit des solutions matérielles et logicielles intégrées aux secteurs de l'énergie, de la logistique, de la santé, du contrôle industriel et d'Internet. Notre devise : « l'apprentissage ne connaît pas l'ancienneté — celui qui maîtrise devient maître ».

Les composants open source ont été téléchargés <!--nuget-total:start-->6.48M<!--nuget-total:end--> fois depuis NuGet (à l'échelle de l'écosystème, en date de 2026-10).

Site web : https://newlifex.com · Organisation open source : https://github.com/NewLifeX

<details>
<summary>Compte officiel WeChat : 智能大石头 (scannez pour suivre)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Licence

[Licence MIT](LICENSE)

Les Issues et Pull Requests sont les bienvenues.

- GitHub : https://github.com/NewLifeX/NewLife.AI
- Site web : https://newlifex.com
- Groupe QQ : 1600800

## Projets Connexes

- [NewLife.Core](https://github.com/NewLifeX/X) — Bibliothèque de base .NET
- [XCode](https://github.com/NewLifeX/X) — Framework ORM de données
- [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) — Plateforme de développement rapide

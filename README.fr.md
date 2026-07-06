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
  <b>Français</b> |
  <a href="README.de.md">Deutsch</a> |
  <a href="README.ru.md">Русский</a> |
  <a href="README.pt.md">Português</a>
</p>

## Introduction

**NewLife.AI** est une **bibliothèque d'IA open source** pour l'écosystème .NET, fournissant une interface unifiée `IChatClient` qui encapsule **46 fournisseurs majeurs de LLM**. Elle intègre les appels de fonctions, le protocole MCP, la sortie en streaming, le support multimodal, les capacités multi-agents, et peut être intégrée en tant que package NuGet dans tout projet .NET (`net45 / netstandard2.1`).

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

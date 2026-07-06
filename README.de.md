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
  <b>Deutsch</b> |
  <a href="README.ru.md">Русский</a> |
  <a href="README.pt.md">Português</a>
</p>

## Einführung

**NewLife.AI** ist eine **Open-Source-KI-Basisbibliothek** für das .NET-Ökosystem, die eine einheitliche `IChatClient`-Schnittstelle bereitstellt, die **46 führende LLM-Anbieter** kapselt. Sie bietet integrierte Funktionsaufrufe, MCP-Protokoll, Streaming-Ausgabe, multimodale Unterstützung, Multi-Agent-Fähigkeiten und kann als NuGet-Paket in jedes .NET-Projekt (`net45 / netstandard2.1`) eingebettet werden.

**NewLife.ChatAI** ist eine auf NewLife.AI aufgebaute **vollständige Web-Chat-Anwendung** (ASP.NET Core), die ein sofort einsatzbereites Multi-Modell-Chat-Frontend, ein einheitliches AI-Gateway und automatische Gedächtnisentwicklung bietet. Sie kann eigenständig bereitgestellt oder in bestehende ASP.NET Core-Projekte eingebettet werden.

---

## Kernfunktionen

- **46 KI-Anbieter, 6 Protokolle**: OpenAI / Anthropic / Gemini / Tongyi DashScope / Ollama / AWS Bedrock — Wechsel mit einer Codezeile
- **Einheitliche `IChatClient`-Schnittstelle**: Ausgerichtet an der MEAI-Spezifikation — Einzelrunde, Streaming, Funktionsaufrufe, multimodal, alles über eine einzige API
- **Funktionsaufrufe (Werkzeuge)**: `[ToolDescription]`-Attribut generiert automatisch JSON Schema; `ToolChatClient`-Mehrrunden-Schleife; integrierte Werkzeuge für Suche / Wetter / Übersetzung / Web Scraping / IP-Geolokalisierung
- **Bidirektionale MCP-Unterstützung**: Client verbindet sich mit externen MCP-Servern (stdio / HTTP SSE); Server stellt lokale Werkzeuge als Standard-MCP-Dienste bereit
- **Vollständiger Chat-Kernel**: `IChatHandler`-Dreistufen-Pipeline (OnBefore → Execute → OnAfter); integrierte Handler (Fähigkeitsaktivierung / Gedächtnisinjektion / Persistenz / Nutzungsstatistik / Titelgenerierung); austauschbarer `IChatFilter`
- **Benutzergedächtnis-Entwicklung**: Extrahiert automatisch 10 Kategorien strukturierter Erinnerungen aus Gesprächen — je mehr Sie chatten, desto besser versteht es Sie
- **Einheitliches AI-Gateway**: Kompatibel mit OpenAI / Anthropic / Gemini-Protokollen; automatische snake_case/camelCase-Anpassung; AppKey-Mandantenfähigkeit; exponentielles Backoff-Retry bei Upstream-429
- **Fähigkeitssystem**: Wiederverwendung von Markdown-Prompts, rekursive `@`-Referenzen, automatische Aktivierung durch Trigger-Wörter
- **Multi-Agent**: `ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner`
- **React 19 Web-Frontend**: SSE-Streaming + Chat-Voreinstellungen + Artifact-Echtzeitvorschau (HTML/SVG/Mermaid) + Gesprächsverzweigung + Werkzeugaufruf-Visualisierung + Reasoning-Zeitmessung + multimodal
- **Wissensevolutionsschicht**: Extrahiert automatisch Wissen aus Gesprächen und baut eine durchsuchbare Wissensdatenbank mit TOC-Navigation und vektorbasierter semantischer Suche auf
- **TTS-Sprachsynthese**: Unterstützt DashScope TTS und CosyVoice V3.5; Streaming-Sprachsynthese; dedizierte Frontend-TTS-API
- **Embedding / Vektorsuche**: Integrierter HashTextEmbedder v2 und Vektorspeicher; Vektorisierung von Wissensdatenbank-Dokumenten und semantische Suche
- **Multi-Agent-Erweiterung**: Neue ReflectionAgent und ReviewAgent; Zerlegung komplexer Aufgaben mit paralleler Sub-Agent-Aggregation
- **Human-in-the-Loop-Checkpoints**: Menschliche Echtzeit-Auswahl zwischen KI-Mehrfachpfaden; Entscheidungs-Checkpoints für Mehrfragengruppen
- **Werkzeugaufruf-Erweiterung**: ToolCallContext-Kontextweitergabe; Werkzeug-Provider-Circuit-Breaker; sitzungsbasierte Werkzeugsichtbarkeitsfilterung; strukturierte Fehlerrückgabe mit dreistufigem Berechtigungssystem
- **Gedächtnisentwicklung**: Gedächtniskonsolidierungsdienst mit Deduplizierung/Zusammenführung/Ablauf; Lerngedächtnis extrahiert automatisch strukturiertes Wissen aus Gesprächen

---

## Unterstützte KI-Anbieter

46 Anbieter: **9 unabhängige Protokollclients + 37 OpenAI-kompatible Adapter**.

### Unabhängige Protokollimplementierungen (9)

| Anbieter | Protokoll | Merkmale |
|----------|----------|----------|
| OpenAI | ChatCompletions / Responses | Vision / Funktionsaufrufe / Bildgenerierung / o3-Reasoning |
| DeepSeek | DeepSeek API | reasoning_content / DeepSeek v4 exklusive Parameter |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| Alibaba Cloud | DashScope | qwen-plus / qwen-max / Omni multimodal / Files API |
| Azure AI | Azure OpenAI | Bereitstellungsname-URL + api-key |
| Ollama | Ollama API | Lokales llama / deepseek / qwen |
| AWS Bedrock | SigV4-Signatur | Claude / Llama / Titan / Mistral |
| NewLifeAI | Kaskaden-Proxy | Multi-Anbieter-Aggregation |
| (Weitere) | OpenAI-kompatibel | 37 kompatible Plattformen |

### OpenAI-kompatible Familie (37)

Doubao (Volcano Engine), Zhipu Qingyan (GLM), Wenxin Yiyan, Moonshot (Kimi), MiniMax, StepFun, Baichuan, iFlytek Spark, 01.AI, Mistral, Perplexity, Cohere, Together AI, Fireworks, OpenRouter, SiliconCloud, DeepInfra, Groq, Cerebras, Hyperbolic, Nebius, Novita, Lepton, 302.AI, xAI (Grok)… und andere OpenAI-kompatible Plattformen.

Alle Anbieter werden über das `[AiClient]`-Attribut deklariert; `AiClientRegistry` scannt und registriert automatisch beim Start — Nullkonfiguration für neue Anbieter.

---

## Schnellstart

### 1. Nur AI SDK

```bash
dotnet add package NewLife.AI
```

```csharp
using NewLife.AI.Clients;

// Einzelrunden-Chat
using var client = new DashScopeChatClient("your-api-key", "qwen-plus");
var reply = await client.ChatAsync("Erklären Sie große Sprachmodelle in drei Sätzen");
Console.WriteLine(reply);

// Mehrrollen-Nachrichten (Tupel-Array, keine manuelle ChatMessage-Konstruktion)
var reply2 = await client.ChatAsync([
    ("system", "Sie sind ein professioneller C#-Entwicklungsassistent"),
    ("user", "Erklären Sie den Unterschied zwischen ValueTask und Task"),
]);

// Streaming-Ausgabe
await foreach (var chunk in client.GetStreamingResponseAsync([
    new ChatMessage { Role = "user", Content = "Schreiben Sie ein kurzes Gedicht über Code" }
], new ChatOptions()))
{
    Console.Write(chunk.Text);
}
```

### 2. ASP.NET Core Dependency Injection

```bash
dotnet add package NewLife.AI.Extensions
```

```csharp
// Dienst registrieren
builder.Services.AddDashScope("your-api-key", "qwen-plus");

// Keyed-Mehrfachanbieter-Koexistenz
builder.Services.AddOpenAI("openai-key", serviceKey: "openai");
builder.Services.AddAnthropic("anthropic-key", serviceKey: "anthropic");

// Injizieren und verwenden
public class MyService(IChatClient chatClient)
{
    public Task<String> ChatAsync(String question)
        => chatClient.ChatAsync(question);
}
```

### 3. Funktionsaufrufe (Werkzeuge)

```csharp
public class MyTools
{
    /// <summary>Wetter für eine bestimmte Stadt abrufen</summary>
    [ToolDescription("get_weather")]
    public async Task<String> GetWeatherAsync(
        [Description("Stadtname")] String city)
        => $"{city}: Sonnig, 22°C heute";
}

// Werkzeuge registrieren
var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// In Pipeline einfügen — automatische Mehrrunden-Werkzeugaufruf-Schleife
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// Modell ruft automatisch get_weather("北京") auf, gibt endgültige Textantwort zurück
var reply = await client.ChatAsync("Wie ist das Wetter heute in Peking?");
```

### 4. Eingeschränktes Reasoning (ReAct-Muster)

Wenn die KI bei komplexen Problemen zu Raten oder Abkürzungen neigt, verwenden Sie das **ReAct-Format** im System Prompt, um schrittweises Reasoning zu erzwingen. `ToolChatClient` hat eine integrierte `while`-Schleife (`MaxIterations = 10`) — kein manuelles Schreiben eines Agenten erforderlich.

```csharp
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

var reply = await client.ChatAsync([
    ("system",
        "Sie müssen Schritt für Schritt im folgenden Format argumentieren. Überspringen Sie keine Schritte und raten Sie nicht direkt:\n" +
        "Thought: <analysieren Sie, was zu tun ist>\n" +
        "Action: <welches Werkzeug aufrufen und Parameter>\n" +
        "Observation: <analysieren Sie das Werkzeugergebnis>\n" +
        "(Wiederholen Sie Thought/Action/Observation, bis ausreichend Informationen vorliegen)\n" +
        "Answer: <endgültige Schlussfolgerung basierend auf den obigen Beobachtungen>\n\n" +
        "Regel: Geben Sie keine Answer, ohne zuvor ein Werkzeug aufzurufen."),
    ("user", "Ist das Wetter in Peking heute für Outdoor-Sport geeignet?"),
]);
```

### 5. Vollständige Web-Chat-Anwendung ausführen

```bash
git clone https://github.com/NewLifeX/NewLife.AI.git
cd NewLife.AI

# Frontend bauen (benötigt Node.js + pnpm)
cd Web && pnpm install && pnpm build && cd ..

# Starten
cd NewLife.ChatAI
dotnet run --framework net8.0
```

Öffnen Sie `http://localhost:5000` im Browser — Standardmäßig SQLite, sofort einsatzbereit. Konfigurieren Sie beim ersten Start die Anbieter-API-Schlüssel über `/Admin`.

Sie können `NewLife.ChatAI` auch über NuGet in ein bestehendes Projekt einbetten:

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

## API-Gateway

NewLife.ChatAI enthält ein integriertes Multi-Protokoll-AI-Gateway. Drittsysteme können ohne Änderungen angebunden werden — alle Pfade durchlaufen Gedächtnisinjektion und Fähigkeitsverbesserung.

| Protokoll | Route | Beschreibung |
|-----------|-------|--------------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | Streaming / Nicht-Streaming / Funktionsaufrufe / Vision |
| OpenAI Responses | `POST /v1/responses` | o3 / gpt-5 Reasoning-Modelle |
| Anthropic Messages | `POST /v1/messages` | Claude-Serie |
| Google Gemini | `POST /v1/gemini/...` | Gemini-Serie |
| Bildgenerierung | `POST /v1/images/generations` | Text-to-Image |
| Bildbearbeitung | `POST /v1/images/edits` | Inpainting (multipart/form-data) |
| Modellerkennung | `GET /v1/models` | Liste verfügbarer Modelle |

**Authentifizierung**: `Authorization: Bearer sk-xxxx` (AppKey)

**Merkmale**: Upstream-429 exponentielles Backoff-Retry (zufälliger Jitter, max. 5 Versuche); automatische Token-Nutzungsaufzeichnung; zweidimensionale Statistik nach AppKey + Benutzer

---

## Erweiterungsentwicklung

### Neuen KI-Anbieter hinzufügen

Von `OpenAIChatClient` erben, `[AiClient]`-Attribut hinzufügen — `AiClientRegistry` scannt automatisch beim Start:

```csharp
[AiClient("MyAI", "Mein Dienst", "https://api.myai.com/v1",
    Description = "Benutzerdefinierter KI-Dienst")]
[AiClientModel("myai-latest", "MyAI Latest", Code = "MyAI",
    FunctionCalling = true, Vision = true)]
public class MyAiChatClient : OpenAIChatClient
{
    public MyAiChatClient() { }
    public MyAiChatClient(String apiKey, String? model = null, String? endpoint = null)
        : base(apiKey, model, endpoint) { }
}
```

### Neues Werkzeug hinzufügen

```csharp
public class MyTools
{
    /// <summary>Aktuelle Uhrzeit abfragen</summary>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());
```

### Neuen IChatHandler hinzufügen

```csharp
[ChatHandlerOrder(150)]
public class CurrentTimeHandler : ChatHandlerBase
{
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        context.ContextMessages.Insert(0,
            new ChatMessage { Role = "system", Content = $"Aktuelle Zeit: {DateTime.Now:yyyy-MM-dd HH:mm}" });
        return Task.CompletedTask;
    }
}

services.AddSingleton<IChatHandler, CurrentTimeHandler>();
```

---

## Dokumentation

| Dokument | Beschreibung |
|----------|--------------|
| [Anforderungsspezifikation](Doc/需求规格说明.md) | Produktziele, Funktionsliste, nicht-funktionale Anforderungen |
| [Architekturdesign](Doc/架构设计.md) | 4-Schichten-Architektur, Moduldesigndetails |
| [KI-Orchestrierungsframework](Doc/AI编排框架需求.md) | Werkzeuge / Agenten / Planer-Design |
| [API-Gateway-Anforderungen](Doc/API网关需求.md) | Gateway-Protokollanpassungsdetails |
| [MCP-Architektur](Doc/MCP架构.md) | MCP-Client- und Server-Design |
| [Fähigkeitsverwaltung](Doc/技能管理需求.md) | Detailliertes Design des Fähigkeitssystems |
| [Selbstlernsystem](Doc/自学习系统需求.md) | Gesprächsanalyse + Gedächtnisextraktion |
| [Chat-Datenpersistenzfluss](Doc/对话数据保存流程.md) | Detaillierter MessageFlow-Prozess |
| [Funktionsmodulliste](Doc/功能模块清单.md) | Vollständiges Funktionsinventar |

---

## Lizenz

[MIT-Lizenz](LICENSE)

Issues und Pull Requests sind willkommen.

- GitHub: https://github.com/NewLifeX/NewLife.AI
- Website: https://newlifex.com
- QQ-Gruppe: 1600800

## Verwandte Projekte

- [NewLife.Core](https://github.com/NewLifeX/X) — .NET-Basisbibliothek
- [XCode](https://github.com/NewLifeX/X) — ORM-Datenframework
- [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) — Schnellentwicklungsplattform

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ Diese Übersetzung kann hinterherhinken; maßgeblich ist das [English README](README.md).


## Einführung

**NewLife.AI** ist eine **Open-Source-KI-Basisbibliothek** für das .NET-Ökosystem, die eine einheitliche `IChatClient`-Schnittstelle bereitstellt, die **46 führende LLM-Anbieter** kapselt. Sie bietet integrierte Funktionsaufrufe, MCP-Protokoll, Streaming-Ausgabe, multimodale Unterstützung, Multi-Agent-Fähigkeiten und kann als NuGet-Paket in jedes .NET-Projekt (`net45 → net10.0`) eingebettet werden.

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

## Ökosystem-Karte

NewLife ist ein geschichtetes Ökosystem unabhängig versionierter Open-Source-Projekte. Die folgende Karte wird wortgleich in allen NewLife-Repositories verwendet; jedes Projekt verlinkt auf sein Repository und sein primäres NuGet-Paket.

| Projekt | Funktion | Downloads |
| --- | --- | --- |
| **Grundlage** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Protokollierung, Konfiguration, Caching, Netzwerk, Serialisierung, Zeitplanung und APM-Tracing. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Daten-Middleware: einzelne Tabellen in der Größenordnung von zig Milliarden Zeilen, automatisches Sharding, Lese-/Schreibtrennung, über 10 Datenbanken. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Hochperformante Netzwerkbibliothek: 1,77 Mio. pps / 14,5 Gbps auf einer Maschine (TCP 1KB, 2026-10), 4 Mio. langlebige TCP-Verbindungen. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Protokollkommunikation und RPC-Framework für Client/Server-Anwendungen und IoT-Geräte. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Installiert .NET-Anwendungen als Betriebssystem-Dienste: Windows-Dienste und Linux-systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new`-Projektvorlagen für Konsole, Web, API, Dienst, Daten und IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Daten & Messaging** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis-Client mit Mikrosekunden-Latenz, Millionen Operationen pro Sekunde und integrierten Nachrichtenwarteschlangen. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Vollständig verwalteter RocketMQ-Client für Apache RocketMQ und Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT-Client und -Server, einschließlich Alibaba Cloud IoT-Anbindung. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Hybriddatenbank in C#: eingebettet oder im Netzwerk, relational, Zeitreihen, Nachrichtenwarteschlange und KV in einem. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql-Treiber und Hilfsschicht auf Basis des NewLife-Datenstacks. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, KI & verteilt** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Schnelles Framework für Administrationsplattformen: Berechtigungen, SSO, OAuth-Server und CRUD-Gerüst. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Verteilte Dienstplattform: Knotenverwaltung, APM, Konfigurationszentrum, Registry und Release-Center. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plattform für verteilte Aufgaben und Echtzeit-Datenplanung mit Sharding, Wiederholungen und Backfill. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Einheitlicher Client für 46 LLM-Anbieter plus ChatAI, eine sofort einsatzbereite Web-Chat-Anwendung. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrie & IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT-Standardbibliothek mit Protokollverträgen und Geräteabstraktionen. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus-TCP/RTU/ASCII-Implementierung auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Siemens-SPS-Protokoll auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Omron-SPS-Protokoll auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Mitsubishi-Melsec-SPS-Protokoll auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC-Client für industriellen Datenzugriff. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet-Protokollstack für die Gebäudeautomation. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Leichtgewichtige Open-Source-IoT-Plattform mit Gateway- und Geräteverwaltung. | — |
| **Fahrzeugtelematik** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T-808-Fahrzeugortungsprotokoll: über 70 Nachrichtenkörper, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T-809-Plattform-zu-Plattform-Protokoll für Verkehrsbehörden. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T-1078-Fahrzeugvideo: RTP-Parsing, FLV/fMP4-Remuxing und Live-Streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Domänenkomponenten** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Ein Paket für Excel, Word, PPT, PDF und über 15 Dokumentformate. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Plattformübergreifendes Audio: Codecs, Geräte-I/O, DSP, Container und Streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Videocodecs, Containerformate, Frame-Verarbeitung und Streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Einheitliche Schnittstelle für Baidu, AMap, Tencent und Tianditu mit Geokodierung und Koordinatenumrechnung. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4-Geolokalisierung mit einer offline speichergemappten Datenbank und einer Online-API als Fallback. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Werkzeuge & Produkte** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Entwickler-Toolbox: Netzwerk, serielle Schnittstelle, Krypto, Regex, Modbus und MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Selbst gehostetes Git-Quellcode-Verwaltungssystem. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Installiert automatisch die passende .NET-Laufzeit, wenn es von einem älteren Host gestartet wird. | — |

> Die Badges zeigen das primäre Paket jedes Projekts; Theme-, Erweiterungs- und Satellitenpakete sind auf den jeweiligen NuGet-Seiten aufgeführt.Die hier nicht aufgeführten über 30 Pakete (FTP, LoRa, Office-Erweiterungen, Protokolltreiber und mehr) sind in der [NewLifeX-Organisation](https://github.com/NewLifeX) und auf [newlifex.com](https://newlifex.com) verfügbar.

## Team

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

NewLife wurde 2002 gegründet und widmet sich seit mehr als zwei Jahrzehnten dem .NET- und IoT-Ökosystem, betreut fast 100 Open-Source-Projekte (34 davon Hauptprojekte) und liefert integrierte Hard- und Softwarelösungen für die Bereiche Energie, Logistik, Gesundheitswesen, Industrieautomation und Internet. Unser Leitspruch lautet „Lernen kennt kein Dienstalter – wer es beherrscht, wird zum Lehrmeister“.

Die Open-Source-Komponenten wurden <!--nuget-total:start-->6.48M<!--nuget-total:end--> Mal von NuGet heruntergeladen (ökosystemweit, Stand 2026-10).

Website: https://newlifex.com · Open-Source-Organisation: https://github.com/NewLifeX

<details>
<summary>WeChat-Offizieller Account: 智能大石头 (zum Folgen scannen)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

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

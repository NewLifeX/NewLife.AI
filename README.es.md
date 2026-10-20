# NewLife.AI

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.AI?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.AI?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.AI?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.AI?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.AI/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.AI/actions/workflows/test.yml)
[![NativeAOT](https://github.com/NewLifeX/NewLife.AI/actions/workflows/aot.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.AI/actions/workflows/aot.yml)
<!-- metrics:start -->
[![AOT smoke](https://img.shields.io/badge/AOT%20smoke-37%2F37-brightgreen?logo=dotnet)](Doc/AOT兼容性分析.md)
[![TFM](https://img.shields.io/badge/TFM-5%20%28net45%20%E2%86%92%20net10.0%29-512BD4?logo=dotnet)](https://www.nuget.org/packages/NewLife.AI)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
<!-- metrics:end -->
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20x86%20%7C%20arm64%20%7C%20arm%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.AI?logo=github)](https://github.com/NewLifeX/NewLife.AI/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ Esta traducción puede estar desactualizada; la versión autorizada es el [English README](README.md).


## Introducción

**NewLife.AI** es una **biblioteca de IA de código abierto** para el ecosistema .NET, que proporciona una interfaz unificada `IChatClient` que encapsula **46 proveedores principales de LLM**. Incluye llamadas a funciones, protocolo MCP, salida en streaming, soporte multimodal, capacidades multi-agente, y puede integrarse como paquete NuGet en cualquier proyecto .NET (`net45 → net10.0`).

**NewLife.ChatAI** es una **aplicación web de chat completa** construida sobre NewLife.AI (ASP.NET Core), que ofrece un frontend de chat multi-modelo listo para usar, una puerta de enlace AI unificada y evolución automática de memoria. Puede desplegarse de forma independiente o integrarse en proyectos ASP.NET Core existentes.

---

## Características Principales

- **Listo para NativeAOT**: `IsAotCompatible` en net8.0/net10.0, matriz de humo NativeAOT de 37 escenarios en Windows / Linux / macOS y x64 / arm64 / loongarch64
- **31 idiomas en tiempo de ejecución**: texto fuente en inglés + tablas de idioma en tiempo de compilación, las traducciones faltantes vuelven al inglés y añadir un idioma no requiere código
- **46 Proveedores de IA, 6 Protocolos**: OpenAI / Anthropic / Gemini / Tongyi DashScope / Ollama / AWS Bedrock — cambia con una línea de código
- **Interfaz unificada `IChatClient`**: Alineada con la especificación MEAI — turno único, streaming, llamadas a funciones, multimodal, todo con una sola API
- **Llamadas a funciones (Herramientas)**: El atributo `[ToolDescription]` genera automáticamente JSON Schema; bucle multi-turno `ToolChatClient`; herramientas integradas de búsqueda / clima / traducción / web scraping / geolocalización IP
- **Soporte MCP bidireccional**: El cliente se conecta a servidores MCP externos (stdio / HTTP SSE); el servidor expone herramientas locales como servicios MCP estándar
- **Kernel de chat completo**: Pipeline de 3 etapas `IChatHandler` (OnBefore → Execute → OnAfter); manejadores integrados (activación de habilidades / inyección de memoria / persistencia / estadísticas de uso / generación de títulos); `IChatFilter` conectable
- **Evolución de memoria del usuario**: Extrae automáticamente 10 categorías de recuerdos estructurados de las conversaciones — cuanto más chateas, mejor te entiende
- **Puerta de enlace AI unificada**: Compatible con protocolos OpenAI / Anthropic / Gemini; adaptación automática snake_case/camelCase; multi-tenencia AppKey; reintento con retroceso exponencial 429
- **Sistema de habilidades**: Reutilización de prompts Markdown, referencias recursivas `@`, activación automática por palabras clave
- **Multi-agente**: `ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner`
- **Frontend Web React 19**: Streaming SSE + presets de chat + vista previa Artifact en tiempo real (HTML/SVG/Mermaid) + bifurcación de conversación + visualización de llamadas a herramientas + temporización de razonamiento + multimodal
- **Capa de evolución del conocimiento**: Extrae automáticamente conocimiento de las conversaciones, construye una base de conocimiento consultable con navegación TOC y búsqueda semántica vectorial
- **Síntesis de voz TTS**: Soporte para DashScope TTS y CosyVoice V3.5; síntesis de voz en streaming; API TTS dedicada para frontend
- **Incrustación / Búsqueda vectorial**: HashTextEmbedder v2 integrado y almacenamiento vectorial; vectorización de documentos y búsqueda semántica
- **Mejora multi-agente**: Nuevos ReflectionAgent y ReviewAgent; descomposición de tareas complejas con agregación paralela de sub-agentes
- **Puntos de control con intervención humana**: Selección humana en tiempo real entre múltiples caminos de IA; puntos de decisión para grupos de preguntas múltiples
- **Llamadas a herramientas mejoradas**: Propagación de contexto ToolCallContext; disyuntor de proveedor de herramientas; filtrado de visibilidad a nivel de sesión; errores estructurados con sistema de permisos de tres niveles
- **Evolución de memoria**: Servicio de consolidación de memoria con deduplicación/fusión/expiración; la memoria de aprendizaje extrae automáticamente conocimiento estructurado

---

## Proveedores de IA Soportados

46 proveedores: **9 clientes de protocolo independiente + 37 adaptadores compatibles con OpenAI**.

### Implementaciones de protocolo independiente (9)

| Proveedor | Protocolo | Características |
|-----------|-----------|-----------------|
| OpenAI | ChatCompletions / Responses | Visión / Llamadas a funciones / Generación de imágenes / Razonamiento o3 |
| DeepSeek | DeepSeek API | reasoning_content / Parámetros exclusivos DeepSeek v4 |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| Alibaba Cloud | DashScope | qwen-plus / qwen-max / Omni multimodal / Files API |
| Azure AI | Azure OpenAI | URL de nombre de despliegue + api-key |
| Ollama | Ollama API | llama / deepseek / qwen local |
| AWS Bedrock | Firma SigV4 | Claude / Llama / Titan / Mistral |
| NewLifeAI | Proxy en cascada | Agregación multi-proveedor |
| (Otros) | Compatible OpenAI | 37 plataformas compatibles |

### Familia compatible con OpenAI (37)

Doubao (Volcano Engine), Zhipu Qingyan (GLM), Wenxin Yiyan, Moonshot (Kimi), MiniMax, StepFun, Baichuan, iFlytek Spark, 01.AI, Mistral, Perplexity, Cohere, Together AI, Fireworks, OpenRouter, SiliconCloud, DeepInfra, Groq, Cerebras, Hyperbolic, Nebius, Novita, Lepton, 302.AI, xAI (Grok)… y otras plataformas compatibles con OpenAI.

Todos los proveedores se declaran mediante el atributo `[AiClient]`; `AiClientRegistry` escanea y registra automáticamente al inicio — configuración cero para nuevos proveedores.

---

## Inicio Rápido

### 1. Solo SDK de IA

```bash
dotnet add package NewLife.AI
```

```csharp
using NewLife.AI.Clients;

// Chat de un solo turno
using var client = new DashScopeChatClient("your-api-key", "qwen-plus");
var reply = await client.ChatAsync("Explica los modelos de lenguaje grandes en tres frases");
Console.WriteLine(reply);

// Mensajes multi-rol (array de tuplas, sin construcción manual de ChatMessage)
var reply2 = await client.ChatAsync([
    ("system", "Eres un asistente profesional de desarrollo C#"),
    ("user", "Explica la diferencia entre ValueTask y Task"),
]);

// Salida en streaming
await foreach (var chunk in client.GetStreamingResponseAsync([
    new ChatMessage { Role = "user", Content = "Escribe un poema corto sobre código" }
], new ChatOptions()))
{
    Console.Write(chunk.Text);
}
```

### 2. Inyección de dependencias ASP.NET Core

```bash
dotnet add package NewLife.AI.Extensions
```

```csharp
// Registrar servicio
builder.Services.AddDashScope("your-api-key", "qwen-plus");

// Coexistencia multi-proveedor con Keyed
builder.Services.AddOpenAI("openai-key", serviceKey: "openai");
builder.Services.AddAnthropic("anthropic-key", serviceKey: "anthropic");

// Inyectar y usar
public class MyService(IChatClient chatClient)
{
    public Task<String> ChatAsync(String question)
        => chatClient.ChatAsync(question);
}
```

### 3. Llamadas a funciones (Herramientas)

```csharp
public class MyTools
{
    /// <summary>Obtener el clima de una ciudad especificada</summary>
    [ToolDescription("get_weather")]
    public async Task<String> GetWeatherAsync(
        [Description("Nombre de la ciudad")] String city)
        => $"{city}: Soleado, 22°C hoy";
}

// Registrar herramientas
var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// Añadir al pipeline — bucle automático de llamadas a herramientas multi-turno
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// El modelo llama automáticamente a get_weather("北京"), devuelve la respuesta final
var reply = await client.ChatAsync("¿Qué tiempo hace hoy en Pekín?");
```

### 4. Razonamiento restringido (Patrón ReAct)

Cuando la IA tiende a adivinar o tomar atajos en problemas complejos, usa el **formato ReAct** en el System Prompt para forzar el razonamiento paso a paso. `ToolChatClient` tiene un bucle `while` integrado (`MaxIterations = 10`), no es necesario escribir un Agent manualmente.

```csharp
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

var reply = await client.ChatAsync([
    ("system",
        "Debes razonar paso a paso en el siguiente formato. No omitas pasos ni adivines directamente:\n" +
        "Thought: <analiza qué necesitas hacer>\n" +
        "Action: <qué herramienta llamar y parámetros>\n" +
        "Observation: <analiza el resultado de la herramienta>\n" +
        "(Repite Thought/Action/Observation hasta tener suficiente información)\n" +
        "Answer: <conclusión final basada en las observaciones anteriores>\n\n" +
        "Regla: No des una Answer sin antes llamar a una herramienta."),
    ("user", "¿Es adecuado el clima de Pekín hoy para deportes al aire libre?"),
]);
```

### 5. Ejecutar la aplicación web de chat completa

```bash
git clone https://github.com/NewLifeX/NewLife.AI.git
cd NewLife.AI

# Construir frontend (requiere Node.js + pnpm)
cd Web && pnpm install && pnpm build && cd ..

# Iniciar
cd NewLife.ChatAI
dotnet run --framework net8.0
```

Abre `http://localhost:5000` en tu navegador — SQLite por defecto, listo para usar. En el primer inicio, configura las claves API de los proveedores en `/Admin`.

También puedes integrar `NewLife.ChatAI` en un proyecto existente mediante NuGet:

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

## Puerta de Enlace API

NewLife.ChatAI incluye una puerta de enlace AI multi-protocolo integrada. Los sistemas de terceros pueden conectarse sin modificaciones — todas las rutas pasan por inyección de memoria y mejora de habilidades.

| Protocolo | Ruta | Descripción |
|-----------|------|-------------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | Streaming / no streaming / llamadas a funciones / visión |
| OpenAI Responses | `POST /v1/responses` | Modelos de razonamiento o3 / gpt-5 |
| Anthropic Messages | `POST /v1/messages` | Serie Claude |
| Google Gemini | `POST /v1/gemini/...` | Serie Gemini |
| Generación de imágenes | `POST /v1/images/generations` | Text-to-Image |
| Edición de imágenes | `POST /v1/images/edits` | Inpainting (multipart/form-data) |
| Descubrimiento de modelos | `GET /v1/models` | Lista de modelos disponibles |

**Autenticación**: `Authorization: Bearer sk-xxxx` (AppKey)

**Características**: Reintento con retroceso exponencial 429 (jitter aleatorio, máximo 5 intentos); registro automático de uso de tokens; estadísticas bidimensionales por AppKey + Usuario

---

## Desarrollo de Extensiones

### Añadir un nuevo proveedor de IA

Hereda de `OpenAIChatClient`, añade el atributo `[AiClient]` — `AiClientRegistry` escanea automáticamente al inicio:

```csharp
[AiClient("MyAI", "Mi Servicio", "https://api.myai.com/v1",
    Description = "Servicio de IA personalizado")]
[AiClientModel("myai-latest", "MyAI Latest", Code = "MyAI",
    FunctionCalling = true, Vision = true)]
public class MyAiChatClient : OpenAIChatClient
{
    public MyAiChatClient() { }
    public MyAiChatClient(String apiKey, String? model = null, String? endpoint = null)
        : base(apiKey, model, endpoint) { }
}
```

### Añadir una nueva herramienta

```csharp
public class MyTools
{
    /// <summary>Consultar la hora actual</summary>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());
```

### Añadir un nuevo IChatHandler

```csharp
[ChatHandlerOrder(150)]
public class CurrentTimeHandler : ChatHandlerBase
{
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        context.ContextMessages.Insert(0,
            new ChatMessage { Role = "system", Content = $"Hora actual: {DateTime.Now:yyyy-MM-dd HH:mm}" });
        return Task.CompletedTask;
    }
}

services.AddSingleton<IChatHandler, CurrentTimeHandler>();
```

---

## Documentación

| Documento | Descripción |
|-----------|-------------|
| [Especificación de requisitos](Doc/需求规格说明.md) | Objetivos del producto, lista de funciones, requisitos no funcionales |
| [Diseño de arquitectura](Doc/架构设计.md) | Arquitectura de 4 capas, detalles de diseño de módulos |
| [Framework de orquestación AI](Doc/AI编排框架需求.md) | Diseño de herramientas / agentes / planificador |
| [Requisitos de API Gateway](Doc/API网关需求.md) | Detalles de adaptación de protocolos |
| [Arquitectura MCP](Doc/MCP架构.md) | Diseño de cliente y servidor MCP |
| [Gestión de habilidades](Doc/技能管理需求.md) | Diseño detallado del sistema de habilidades |
| [Sistema de autoaprendizaje](Doc/自学习系统需求.md) | Análisis de conversación + extracción de memoria |
| [Flujo de persistencia de datos](Doc/对话数据保存流程.md) | Proceso detallado de MessageFlow |
| [Lista de módulos funcionales](Doc/功能模块清单.md) | Inventario completo de funciones |

---

## Mapa del ecosistema

NewLife es un ecosistema por capas de proyectos de código abierto con versionado independiente. El mapa siguiente se comparte literalmente en todos los repositorios de NewLife; cada proyecto enlaza a su repositorio y a su paquete principal de NuGet.

| Proyecto | Qué hace | Descargas |
| --- | --- | --- |
| **Fundación** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Registro de logs, configuración, caché, red, serialización, programación de tareas y seguimiento APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Middleware de datos: tablas únicas a escala de decenas de miles de millones, particionado automático, separación lectura/escritura y más de 10 bases de datos. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Biblioteca de red de alto rendimiento: 1,77 M pps / 14,5 Gbps en una máquina (TCP 1KB, 2026-10), 4 M conexiones TCP persistentes. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Framework de comunicación por protocolo y RPC para aplicaciones cliente/servidor y dispositivos IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Instala aplicaciones .NET como demonios del sistema operativo: servicios de Windows y systemd de Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Plantillas de proyecto `dotnet new` para consola, web, API, servicio, datos e IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Datos y mensajería** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Cliente Redis con latencia de microsegundos, millones de operaciones por segundo y colas de mensajes integradas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Cliente RocketMQ totalmente administrado para Apache RocketMQ y Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Cliente y servidor MQTT, incluida la conectividad con Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Base de datos híbrida en C#: embebida o en red, relacional, series temporales, cola de mensajes y KV en una sola. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Controlador y capa de ayuda de MySql construidos sobre la pila de datos de NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, IA y distribuido** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Framework rápido para plataformas de administración: permisos, SSO, servidor OAuth y andamiaje CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Plataforma distribuida de servicios: gestión de nodos, APM, centro de configuración, registro y centro de despliegues. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plataforma de tareas distribuidas y programación de datos en tiempo real con particionado, reintentos y relleno histórico. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Cliente unificado para 46 proveedores de LLM más ChatAI, una aplicación web de chat lista para usar. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrial e IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Biblioteca estándar de IoT que define contratos de protocolo y abstracciones de dispositivos. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Implementación de Modbus TCP/RTU/ASCII sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Protocolo para PLC Siemens sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Protocolo para PLC Omron sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Protocolo para PLC Mitsubishi Melsec sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Cliente OPC para el acceso a datos industriales. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Pila de protocolo BACnet para la automatización de edificios. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Plataforma IoT de código abierto y ligera con gestión de pasarelas y dispositivos. | — |
| **Telemática vehicular** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Protocolo de posicionamiento vehicular JT/T 808: más de 70 cuerpos de mensaje, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Protocolo plataforma a plataforma JT/T 809 para autoridades de transporte. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Vídeo a bordo JT/T 1078: análisis RTP, remultiplexado FLV/fMP4 y transmisión en directo. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Componentes de dominio** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Un solo paquete para Excel, Word, PPT, PDF y más de 15 formatos de documento. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Audio multiplataforma: códecs, E/S de dispositivos, DSP, contenedores y streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Códecs de vídeo, formatos de contenedor, procesamiento de fotogramas y streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Interfaz unificada sobre Baidu, AMap, Tencent y Tianditu con geocodificación y conversión de coordenadas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Geolocalización IPv4 con una base de datos offline mapeada en memoria y una API en línea de respaldo. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Herramientas y productos** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Caja de herramientas del desarrollador: red, puerto serie, criptografía, expresiones regulares, Modbus y MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Sistema de gestión de código fuente Git autoalojado. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Instala automáticamente el runtime de .NET correspondiente cuando se ejecuta desde un host más antiguo. | — |

> Las insignias muestran el paquete principal de cada proyecto; los paquetes de temas, extensiones y satélites figuran en sus páginas de NuGet.Los más de 30 paquetes no listados aquí (FTP, LoRa, extensiones de Office, controladores de protocolo, etc.) están disponibles en la [organización NewLifeX](https://github.com/NewLifeX) y en [newlifex.com](https://newlifex.com).

## Equipo

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

NewLife se fundó en 2002 y lleva más de dos décadas dedicado al ecosistema .NET e IoT, mantiene cerca de 100 proyectos de código abierto (34 de ellos principales) y ofrece soluciones integradas de software y hardware para los sectores de energía, logística, salud, control industrial e Internet. Nos guía el lema «en el aprendizaje no hay antigüedad: quien domina es maestro».

Los componentes de código abierto acumulan <!--nuget-total:start-->6.48M<!--nuget-total:end--> descargas en NuGet (todo el ecosistema, a fecha de 2026-10).

Sitio web: https://newlifex.com · Organización de código abierto: https://github.com/NewLifeX

<details>
<summary>Cuenta oficial de WeChat: 智能大石头 (escanea para seguir)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Licencia

[Licencia MIT](LICENSE)

Se aceptan Issues y Pull Requests.

- GitHub: https://github.com/NewLifeX/NewLife.AI
- Sitio web: https://newlifex.com
- Grupo QQ: 1600800

## Proyectos Relacionados

- [NewLife.Core](https://github.com/NewLifeX/X) — Biblioteca base .NET
- [XCode](https://github.com/NewLifeX/X) — Framework ORM de datos
- [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) — Plataforma de desarrollo rápido

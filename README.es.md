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
  <b>Español</b> |
  <a href="README.fr.md">Français</a> |
  <a href="README.de.md">Deutsch</a> |
  <a href="README.ru.md">Русский</a> |
  <a href="README.pt.md">Português</a>
</p>

## Introducción

**NewLife.AI** es una **biblioteca de IA de código abierto** para el ecosistema .NET, que proporciona una interfaz unificada `IChatClient` que encapsula **46 proveedores principales de LLM**. Incluye llamadas a funciones, protocolo MCP, salida en streaming, soporte multimodal, capacidades multi-agente, y puede integrarse como paquete NuGet en cualquier proyecto .NET (`net45 / netstandard2.1`).

**NewLife.ChatAI** es una **aplicación web de chat completa** construida sobre NewLife.AI (ASP.NET Core), que ofrece un frontend de chat multi-modelo listo para usar, una puerta de enlace AI unificada y evolución automática de memoria. Puede desplegarse de forma independiente o integrarse en proyectos ASP.NET Core existentes.

---

## Características Principales

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

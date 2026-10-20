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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ Этот перевод может отставать от актуальной версии; основным является [English README](README.md).


## Введение

**NewLife.AI** — это **открытая библиотека ИИ** для экосистемы .NET, предоставляющая единый интерфейс `IChatClient`, который объединяет **46 ведущих провайдеров LLM**. Включает встроенные вызовы функций, протокол MCP, потоковый вывод, мультимодальную поддержку, мультиагентные возможности и может быть встроена как NuGet-пакет в любой проект .NET (`net45 → net10.0`).

**NewLife.ChatAI** — это **полноценное веб-приложение для чата**, построенное на NewLife.AI (ASP.NET Core), предлагающее готовый к использованию мультимодельный фронтенд чата, единый AI-шлюз и автоматическую эволюцию памяти. Может быть развернуто самостоятельно или встроено в существующие проекты ASP.NET Core.

---

## Ключевые возможности

- **Готовность к NativeAOT**: `IsAotCompatible` для net8.0/net10.0, матрица smoke-тестов NativeAOT из 37 сценариев на Windows / Linux / macOS и x64 / arm64 / loongarch64
- **31 язык во время выполнения**: английский исходный текст + таблицы языков на этапе компиляции, отсутствующие переводы откатываются на английский, добавление языка не требует изменений кода
- **46 ИИ-провайдеров, 6 протоколов**: OpenAI / Anthropic / Gemini / Tongyi DashScope / Ollama / AWS Bedrock — переключение одной строкой кода
- **Единый интерфейс `IChatClient`**: Соответствует спецификации MEAI — одиночный раунд, потоковая передача, вызовы функций, мультимодальность — всё через единый API
- **Вызовы функций (Инструменты)**: Атрибут `[ToolDescription]` автоматически генерирует JSON Schema; многораундовый цикл `ToolChatClient`; встроенные инструменты поиска / погоды / перевода / веб-скрапинга / IP-геолокации
- **Двунаправленная поддержка MCP**: Клиент подключается к внешним MCP-серверам (stdio / HTTP SSE); сервер предоставляет локальные инструменты как стандартные MCP-сервисы
- **Полноценное ядро чата**: Трёхэтапный конвейер `IChatHandler` (OnBefore → Execute → OnAfter); встроенные обработчики (активация навыков / внедрение памяти / сохранение / статистика использования / генерация заголовков); сменный `IChatFilter`
- **Эволюция памяти пользователя**: Автоматически извлекает 10 категорий структурированных воспоминаний из разговоров — чем больше вы общаетесь, тем лучше оно вас понимает
- **Единый AI-шлюз**: Совместимость с протоколами OpenAI / Anthropic / Gemini; автоматическая адаптация snake_case/camelCase; мультитенантность AppKey; экспоненциальная повторная попытка при ошибке 429
- **Система навыков**: Повторное использование Markdown-промптов, рекурсивные ссылки `@`, автоматическая активация по триггерным словам
- **Мультиагентность**: `ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner`
- **Веб-фронтенд React 19**: SSE-потоковая передача + пресеты чата + предпросмотр Artifact в реальном времени (HTML/SVG/Mermaid) + ветвление диалогов + визуализация вызовов инструментов + замер времени рассуждения + мультимодальность
- **Слой эволюции знаний**: Автоматически извлекает знания из разговоров, строит поисковую базу знаний с навигацией по содержанию и векторным семантическим поиском
- **Синтез речи TTS**: Поддержка DashScope TTS и CosyVoice V3.5; потоковый синтез речи; выделенный TTS API для фронтенда
- **Эмбеддинги / Векторный поиск**: Встроенный HashTextEmbedder v2 и векторное хранилище; векторизация документов базы знаний и семантический поиск
- **Улучшенная мультиагентность**: Новые ReflectionAgent и ReviewAgent; декомпозиция сложных задач с параллельной агрегацией подагентов
- **Контрольные точки с участием человека**: Выбор человеком в реальном времени среди множества путей ИИ; контрольные точки принятия решений для групп вопросов
- **Улучшенные вызовы инструментов**: Передача контекста ToolCallContext; автоматический выключатель провайдера инструментов; фильтрация видимости инструментов на уровне сессии; структурированные ошибки с трёхуровневой системой разрешений
- **Эволюция памяти**: Сервис консолидации памяти с дедупликацией/слиянием/истечением срока; обучающая память автоматически извлекает структурированные знания из разговоров

---

## Поддерживаемые ИИ-провайдеры

46 провайдеров: **9 независимых протокольных клиентов + 37 OpenAI-совместимых адаптеров**.

### Независимые реализации протоколов (9)

| Провайдер | Протокол | Особенности |
|-----------|----------|-------------|
| OpenAI | ChatCompletions / Responses | Зрение / Вызовы функций / Генерация изображений / Рассуждение o3 |
| DeepSeek | DeepSeek API | reasoning_content / Эксклюзивные параметры DeepSeek v4 |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| Alibaba Cloud | DashScope | qwen-plus / qwen-max / Omni мультимодальный / Files API |
| Azure AI | Azure OpenAI | URL имени развертывания + api-key |
| Ollama | Ollama API | Локальные llama / deepseek / qwen |
| AWS Bedrock | Подпись SigV4 | Claude / Llama / Titan / Mistral |
| NewLifeAI | Каскадный прокси | Агрегация нескольких провайдеров |
| (Прочие) | OpenAI-совместимые | 37 совместимых платформ |

### OpenAI-совместимое семейство (37)

Doubao (Volcano Engine), Zhipu Qingyan (GLM), Wenxin Yiyan, Moonshot (Kimi), MiniMax, StepFun, Baichuan, iFlytek Spark, 01.AI, Mistral, Perplexity, Cohere, Together AI, Fireworks, OpenRouter, SiliconCloud, DeepInfra, Groq, Cerebras, Hyperbolic, Nebius, Novita, Lepton, 302.AI, xAI (Grok)… и другие OpenAI-совместимые платформы.

Все провайдеры объявляются через атрибут `[AiClient]`; `AiClientRegistry` автоматически сканирует и регистрирует при запуске — нулевая конфигурация для новых провайдеров.

---

## Быстрый старт

### 1. Только AI SDK

```bash
dotnet add package NewLife.AI
```

```csharp
using NewLife.AI.Clients;

// Одиночный раунд чата
using var client = new DashScopeChatClient("your-api-key", "qwen-plus");
var reply = await client.ChatAsync("Объясните большие языковые модели в трёх предложениях");
Console.WriteLine(reply);

// Многоролевые сообщения (массив кортежей, без ручного создания ChatMessage)
var reply2 = await client.ChatAsync([
    ("system", "Вы — профессиональный ассистент по разработке на C#"),
    ("user", "Объясните разницу между ValueTask и Task"),
]);

// Потоковый вывод
await foreach (var chunk in client.GetStreamingResponseAsync([
    new ChatMessage { Role = "user", Content = "Напишите короткое стихотворение о коде" }
], new ChatOptions()))
{
    Console.Write(chunk.Text);
}
```

### 2. Внедрение зависимостей ASP.NET Core

```bash
dotnet add package NewLife.AI.Extensions
```

```csharp
// Регистрация сервиса
builder.Services.AddDashScope("your-api-key", "qwen-plus");

// Сосуществование нескольких провайдеров через Keyed
builder.Services.AddOpenAI("openai-key", serviceKey: "openai");
builder.Services.AddAnthropic("anthropic-key", serviceKey: "anthropic");

// Внедрение и использование
public class MyService(IChatClient chatClient)
{
    public Task<String> ChatAsync(String question)
        => chatClient.ChatAsync(question);
}
```

### 3. Вызовы функций (Инструменты)

```csharp
public class MyTools
{
    /// <summary>Получить погоду для указанного города</summary>
    [ToolDescription("get_weather")]
    public async Task<String> GetWeatherAsync(
        [Description("Название города")] String city)
        => $"{city}: Солнечно, 22°C сегодня";
}

// Регистрация инструментов
var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// Добавление в конвейер — автоматический многораундовый цикл вызовов инструментов
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// Модель автоматически вызывает get_weather("Пекин"), возвращает итоговый текстовый ответ
var reply = await client.ChatAsync("Какая сегодня погода в Пекине?");
```

### 4. Ограниченное рассуждение (паттерн ReAct)

Когда ИИ склонен угадывать или срезать углы на сложных задачах, используйте **формат ReAct** в System Prompt для принудительного пошагового рассуждения. `ToolChatClient` имеет встроенный цикл `while` (`MaxIterations = 10`) — не нужно писать Агента вручную.

```csharp
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

var reply = await client.ChatAsync([
    ("system",
        "Вы должны рассуждать пошагово в следующем формате. Не пропускайте шаги и не угадывайте напрямую:\n" +
        "Thought: <проанализируйте, что нужно сделать>\n" +
        "Action: <какой инструмент вызвать и параметры>\n" +
        "Observation: <проанализируйте результат инструмента>\n" +
        "(Повторяйте Thought/Action/Observation, пока не соберёте достаточно информации)\n" +
        "Answer: <окончательный вывод на основе наблюдений>\n\n" +
        "Правило: Не давайте Answer без предварительного вызова инструмента."),
    ("user", "Подходит ли погода в Пекине сегодня для занятий спортом на улице?"),
]);
```

### 5. Запуск полного веб-приложения чата

```bash
git clone https://github.com/NewLifeX/NewLife.AI.git
cd NewLife.AI

# Сборка фронтенда (требуется Node.js + pnpm)
cd Web && pnpm install && pnpm build && cd ..

# Запуск
cd NewLife.ChatAI
dotnet run --framework net8.0
```

Откройте `http://localhost:5000` в браузере — по умолчанию SQLite, готово к использованию. При первом запуске настройте API-ключи провайдеров через `/Admin`.

Вы также можете встроить `NewLife.ChatAI` в существующий проект через NuGet:

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

## API-шлюз

NewLife.ChatAI включает встроенный мультипротокольный AI-шлюз. Сторонние системы могут подключаться без модификаций — все пути проходят через внедрение памяти и улучшение навыков.

| Протокол | Маршрут | Описание |
|----------|---------|----------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | Потоковый / не потоковый / вызовы функций / зрение |
| OpenAI Responses | `POST /v1/responses` | Модели рассуждения o3 / gpt-5 |
| Anthropic Messages | `POST /v1/messages` | Серия Claude |
| Google Gemini | `POST /v1/gemini/...` | Серия Gemini |
| Генерация изображений | `POST /v1/images/generations` | Text-to-Image |
| Редактирование изображений | `POST /v1/images/edits` | Inpainting (multipart/form-data) |
| Обнаружение моделей | `GET /v1/models` | Список доступных моделей |

**Аутентификация**: `Authorization: Bearer sk-xxxx` (AppKey)

**Особенности**: Экспоненциальная повторная попытка при ошибке 429 (случайный джиттер, до 5 попыток); автоматическая запись использования токенов; двумерная статистика по AppKey + Пользователь

---

## Разработка расширений

### Добавление нового ИИ-провайдера

Унаследуйте от `OpenAIChatClient`, добавьте атрибут `[AiClient]` — `AiClientRegistry` автоматически сканирует при запуске:

```csharp
[AiClient("MyAI", "Мой сервис", "https://api.myai.com/v1",
    Description = "Пользовательский ИИ-сервис")]
[AiClientModel("myai-latest", "MyAI Latest", Code = "MyAI",
    FunctionCalling = true, Vision = true)]
public class MyAiChatClient : OpenAIChatClient
{
    public MyAiChatClient() { }
    public MyAiChatClient(String apiKey, String? model = null, String? endpoint = null)
        : base(apiKey, model, endpoint) { }
}
```

### Добавление нового инструмента

```csharp
public class MyTools
{
    /// <summary>Получить текущее время</summary>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());
```

### Добавление нового IChatHandler

```csharp
[ChatHandlerOrder(150)]
public class CurrentTimeHandler : ChatHandlerBase
{
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        context.ContextMessages.Insert(0,
            new ChatMessage { Role = "system", Content = $"Текущее время: {DateTime.Now:yyyy-MM-dd HH:mm}" });
        return Task.CompletedTask;
    }
}

services.AddSingleton<IChatHandler, CurrentTimeHandler>();
```

---

## Документация

| Документ | Описание |
|----------|----------|
| [Спецификация требований](Doc/需求规格说明.md) | Цели продукта, список функций, нефункциональные требования |
| [Архитектурный дизайн](Doc/架构设计.md) | 4-слойная архитектура, детали дизайна модулей |
| [Фреймворк оркестрации ИИ](Doc/AI编排框架需求.md) | Дизайн инструментов / агентов / планировщика |
| [Требования к API-шлюзу](Doc/API网关需求.md) | Детали адаптации протоколов шлюза |
| [Архитектура MCP](Doc/MCP架构.md) | Дизайн клиента и сервера MCP |
| [Управление навыками](Doc/技能管理需求.md) | Детальный дизайн системы навыков |
| [Система самообучения](Doc/自学习系统需求.md) | Анализ разговоров + извлечение памяти |
| [Поток сохранения данных чата](Doc/对话数据保存流程.md) | Детальный процесс MessageFlow |
| [Список функциональных модулей](Doc/功能模块清单.md) | Полный перечень функций |

---

## Карта экосистемы

NewLife — это многоуровневая экосистема независимо версионируемых open-source проектов. Карта ниже дословно используется во всех репозиториях NewLife; каждый проект ссылается на свой репозиторий и свой основной пакет NuGet.

| Проект | Что делает | Загрузки |
| --- | --- | --- |
| **Базовый** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Логирование, конфигурация, кэширование, сеть, сериализация, планирование и APM-трассировка. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Промежуточное ПО данных: отдельные таблицы масштаба десятков миллиардов строк, автоматическое секционирование, разделение чтения и записи, более 10 баз данных. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Высокопроизводительная сетевая библиотека: 1,77M pps / 14,5 Гбит/с на машину (TCP 1KB, 2026-10), 4M долгоживущих TCP-соединений. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Протокольная связь и RPC-фреймворк для клиент-серверных приложений и IoT-устройств. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Устанавливает .NET-приложения как демоны ОС: службы Windows и systemd в Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Шаблоны проектов `dotnet new` для консоли, web, API, службы, данных и IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Данные и обмен сообщениями** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Клиент Redis с микросекундной задержкой, миллионами операций в секунду и встроенными очередями сообщений. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Полностью управляемый клиент RocketMQ для Apache RocketMQ и Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Клиент и сервер MQTT, включая подключение к Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Гибридная база данных на C#: встроенная или сетевая, реляционная, временных рядов, очередь сообщений и KV в одном. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Драйвер MySql и вспомогательный слой на основе стека данных NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, AI и распределённые системы** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Фреймворк быстрой разработки админ-платформ: права, SSO, сервер OAuth и CRUD-скаффолдинг. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Распределённая сервисная платформа: управление узлами, APM, центр конфигурации, реестр и центр публикации. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Распределённая платформа задач и планирования данных в реальном времени с секционированием, повторами и дозаполнением. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Единый клиент для 46 поставщиков LLM плюс ChatAI — готовое веб-приложение для чата. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Промышленность и IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Стандартная библиотека IoT, определяющая контракты протоколов и абстракции устройств. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Реализация Modbus TCP/RTU/ASCII поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Протокол Siemens PLC поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Протокол Omron PLC поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Протокол Mitsubishi Melsec PLC поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Клиент OPC для доступа к промышленным данным. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Стек протокола автоматизации зданий BACnet. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Лёгкая open-source платформа IoT с управлением шлюзами и устройствами. | — |
| **Телематика транспорта** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Протокол позиционирования транспорта JT/T 808: более 70 типов сообщений, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Межплатформенный протокол JT/T 809 для органов управления транспортом. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Автомобильное видео JT/T 1078: разбор RTP, ремультиплексирование в FLV/fMP4 и прямая трансляция. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Прикладные компоненты** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Один пакет для Excel, Word, PPT, PDF и более 15 форматов документов. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Кроссплатформенное аудио: кодеки, ввод-вывод устройств, DSP, контейнеры и потоковая передача. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Видеокодеки, форматы контейнеров, обработка кадров и потоковая передача. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Единый интерфейс к Baidu, AMap, Tencent и Tianditu с геокодированием и преобразованием координат. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Геолокация IPv4 с офлайн-базой, отображаемой в память, и онлайн-API как резервом. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Инструменты и продукты** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Инструменты разработчика: сеть, последовательный порт, шифрование, регулярные выражения, Modbus и MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Самостоятельно размещаемая система управления исходным кодом Git. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Автоматически устанавливает подходящую среду выполнения .NET при запуске на старом хосте. | — |

> Бейджи показывают основной пакет каждого проекта; пакеты тем, расширений и сателлитов перечислены на соответствующих страницах NuGet.Более 30 пакетов, не перечисленных здесь (FTP, LoRa, расширения Office, драйверы протоколов и другие), доступны в [организации NewLifeX](https://github.com/NewLifeX) и на [newlifex.com](https://newlifex.com).

## Команда

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Команда NewLife основана в 2002 году и более двух десятилетий работает в экосистеме .NET и IoT, поддерживает около 100 проектов с открытым кодом (34 из них — основные) и предлагает интегрированные программно-аппаратные решения для энергетики, логистики, здравоохранения, промышленной автоматизации и интернет-отрасли. Мы следуем девизу «в учении нет старших — мастерство приносит уважение».

Open-source компоненты скачали <!--nuget-total:start-->6.48M<!--nuget-total:end--> раз с NuGet (по всей экосистеме, по состоянию на 2026-10).

Сайт: https://newlifex.com · Организация open source: https://github.com/NewLifeX

<details>
<summary>Официальный аккаунт WeChat: 智能大石头 (отсканируйте, чтобы подписаться)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Лицензия

[Лицензия MIT](LICENSE)

Приветствуются Issues и Pull Requests.

- GitHub: https://github.com/NewLifeX/NewLife.AI
- Сайт: https://newlifex.com
- QQ группа: 1600800

## Связанные проекты

- [NewLife.Core](https://github.com/NewLifeX/X) — Базовая библиотека .NET
- [XCode](https://github.com/NewLifeX/X) — ORM-фреймворк для работы с данными
- [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) — Платформа быстрой разработки

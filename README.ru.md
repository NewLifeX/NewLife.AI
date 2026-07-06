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
  <b>Русский</b> |
  <a href="README.pt.md">Português</a>
</p>

## Введение

**NewLife.AI** — это **открытая библиотека ИИ** для экосистемы .NET, предоставляющая единый интерфейс `IChatClient`, который объединяет **46 ведущих провайдеров LLM**. Включает встроенные вызовы функций, протокол MCP, потоковый вывод, мультимодальную поддержку, мультиагентные возможности и может быть встроена как NuGet-пакет в любой проект .NET (`net45 / netstandard2.1`).

**NewLife.ChatAI** — это **полноценное веб-приложение для чата**, построенное на NewLife.AI (ASP.NET Core), предлагающее готовый к использованию мультимодельный фронтенд чата, единый AI-шлюз и автоматическую эволюцию памяти. Может быть развернуто самостоятельно или встроено в существующие проекты ASP.NET Core.

---

## Ключевые возможности

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

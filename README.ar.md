# NewLife.AI

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.AI?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.AI?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.AI?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.AI?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.AI/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.AI/actions/workflows/test.yml)
[![NativeAOT](https://github.com/NewLifeX/NewLife.AI/actions/workflows/aot.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.AI/actions/workflows/aot.yml)
<!-- metrics:start -->
[![AOT smoke](https://img.shields.io/badge/AOT%20smoke-33%2F33-brightgreen?logo=dotnet)](Doc/AOT兼容性分析.md)
[![TFM](https://img.shields.io/badge/TFM-5%20%28net45%20%E2%86%92%20net10.0%29-512BD4?logo=dotnet)](https://www.nuget.org/packages/NewLife.AI)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
<!-- metrics:end -->
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20x86%20%7C%20arm64%20%7C%20arm%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.AI?logo=github)](https://github.com/NewLifeX/NewLife.AI/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md)
<!-- lang:end -->

> ⚠️ قد تتأخر هذه الترجمة عن أحدث نسخة؛ المحتوى المعتمد هو [English README](README.md).


## مقدمة

**NewLife.AI** مكتبة أساس مفتوحة المصدر للذكاء الاصطناعي في منظومة .NET، توفّر واجهة موحّدة `IChatClient` تغلّف **46 من مزوّدي نماذج اللغة الكبيرة (LLM) السائدين**. وتتضمّن استدعاء الدوال المدمج، وبروتوكول MCP، والإخراج المتدفّق، ودعم الوسائط المتعددة، وقدرات الوكلاء المتعددين، ويمكن تضمينها كحزمة NuGet في أي مشروع .NET (`net45 → net10.0`، وجاهزة لـ NativeAOT).

توفّر المكتبة **31 لغة وقت تشغيل** (المصدر بالإنجليزية مع تراجع تلقائي)، وتستهدف Windows وLinux وmacOS وAndroid وiOS عبر معماريات x64 / x86 / arm64 / arm / loongarch64 / riscv64 / s390x.

**NewLife.ChatAI** تطبيق دردشة ويب متكامل مبني على NewLife.AI (ASP.NET Core)، يقدّم واجهة أمامية للدردشة متعددة النماذج جاهزة للاستخدام، وبوابة ذكاء اصطناعي موحّدة، وتطوّرًا تلقائيًا للذاكرة. ويمكن نشره بشكل مستقل أو تضمينه في مشاريع ASP.NET Core القائمة عبر NuGet.

---

## الميزات الأساسية

- **جاهزية NativeAOT**: تفعيل `IsAotCompatible` على net8.0/net10.0، ومصفوفة فحص دخاني لـ NativeAOT من 22 سيناريو عبر Windows / Linux / macOS وx64 / arm64 / loongarch64
- **31 لغة وقت تشغيل**: رسائل المصدر بالإنجليزية مع جداول لغات مُجمّعة؛ وتعود الترجمات المفقودة إلى الإنجليزية، دون الحاجة إلى تغييرات في الشيفرة لإضافة لغات جديدة
- **46 مزوّد ذكاء اصطناعي، 6 بروتوكولات**: OpenAI / Anthropic / Gemini / Tongyi DashScope / Ollama / AWS Bedrock — التبديل بسطر واحد من الشيفرة
- **واجهة `IChatClient` موحّدة**: متوافقة مع مواصفات MEAI — الدور الواحد والبث المتدفّق واستدعاء الدوال والوسائط المتعددة كلها عبر واجهة برمجية واحدة
- **استدعاء الدوال (الأدوات)**: السمة `[ToolDescription]` تولّد JSON Schema تلقائيًا؛ وحلقة متعددة الأدوار في `ToolChatClient`؛ وأدوات مدمجة للبحث / الطقس / الترجمة / كشط الويب / تحديد موقع IP
- **دعم MCP ثنائي الاتجاه**: يتصل العميل بخوادم MCP خارجية (stdio / HTTP SSE)؛ ويكشف الخادم الأدوات المحلية كخدمات MCP قياسية
- **نواة دردشة متكاملة**: خط أنابيب ثلاثي المراحل `IChatHandler` (OnBefore → Execute → OnAfter)، ومعالجات مدمجة (تفعيل المهارات / حقن الذاكرة / الاستمرارية / تتبّع الاستخدام / توليد العنوان)، و`IChatFilter` قابل للتوصيل
- **تطوّر ذاكرة المستخدم**: يستخرج تلقائيًا 10 فئات من الذكريات المهيكلة من المحادثات — كلما تحدثت أكثر، فهمك أفضل
- **بوابة ذكاء اصطناعي موحّدة**: متوافقة مع بروتوكولات OpenAI / Anthropic / Gemini؛ وتكيّف تلقائي بين snake_case/camelCase؛ وتعدد مستأجرين عبر AppKey؛ وإعادة محاولة بتراجع أُسّي عند استجابة 429 من المنبع
- **نظام المهارات**: إعادة استخدام أوامر Markdown، ومراجع `@` التكرارية، وتفعيل تلقائي بكلمات التفعيل
- **وكلاء متعددون**: `ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner`
- **واجهة ويب React 19**: بث SSE + إعدادات دردشة مسبقة + معاينة فورية لـ Artifact (HTML/SVG/Mermaid) + تفرّع المحادثة + تصوّر استدعاءات الأدوات + توقيت الاستدلال + الوسائط المتعددة
- **طبقة تطوّر المعرفة**: تقطير المعرفة تلقائيًا من المحادثات، وبناء قاعدة معرفة قابلة للبحث مع تصفح جدول المحتويات والبحث الدلالي المتجهي
- **تركيب الكلام TTS**: دعم DashScope TTS وCosyVoice V3.5؛ وتركيب كلام متدفّق؛ وواجهة TTS مخصصة للواجهة الأمامية
- **التضمين / الاسترجاع المتجهي**: HashTextEmbedder v2 وتخزين متجهي مدمجان؛ وترميز مستندات قاعدة المعرفة والبحث الدلالي
- **تعزيز الوكلاء المتعددين**: وكيلا ReflectionAgent وReviewAgent الجديدان؛ وتفكيك المهام المعقدة مع تجميع متوازٍ للوكلاء الفرعيين
- **نقاط تحقق بمشاركة بشرية (Human-in-the-Loop)**: اختيار بشري فوري بين خيارات متعددة المسارات من الذكاء الاصطناعي؛ ونقاط تحقق لقرارات جماعية متعددة الأسئلة
- **تعزيز استدعاء الأدوات**: نشر سياق ToolCallContext؛ وقاطع دائرة لمزوّد الأدوات؛ وتصفية ظهور الأدوات على مستوى الجلسة؛ وإرجاع أخطاء مهيكل مع نظام أذونات ثلاثي المستويات
- **تطوّر الذاكرة**: خدمة توحيد الذاكرة مع إزالة التكرار/الدمج/الانتهاء؛ وذاكرة تعلّم تستخرج تلقائيًا معرفة مهيكلة من المحادثات

---

## مزوّدو الذكاء الاصطناعي المدعومون

46 مزوّدًا: **9 عملاء بروتوكولات مستقلة + 37 محوّلًا متوافقًا مع OpenAI**.

### تطبيقات البروتوكولات المستقلة (9)

| المزوّد | البروتوكول | الميزات |
|----------|----------|----------|
| OpenAI | ChatCompletions / Responses | الرؤية / استدعاء الدوال / توليد الصور / استدلال o3 |
| DeepSeek | DeepSeek API | reasoning_content / معاملات DeepSeek v4 الحصرية |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| Alibaba Cloud | DashScope | qwen-plus / qwen-max / Omni متعدد الوسائط / Files API |
| Azure AI | Azure OpenAI | عنوان URL لاسم النشر + api-key |
| Ollama | Ollama API | llama / deepseek / qwen محلية |
| AWS Bedrock | SigV4 Signature | Claude / Llama / Titan / Mistral |
| NewLifeAI | Cascade Proxy | تجميع متعدد المزوّدين |
| (أخرى) | OpenAI Compatible | 37 منصة متوافقة |

### عائلة المتوافقين مع OpenAI (37)

Doubao (Volcano Engine)، Zhipu Qingyan (GLM)، Wenxin Yiyan، Moonshot (Kimi)، MiniMax، StepFun، Baichuan، iFlytek Spark، 01.AI، Mistral، Perplexity، Cohere، Together AI، Fireworks، OpenRouter، SiliconCloud، DeepInfra، Groq، Cerebras، Hyperbolic، Nebius، Novita، Lepton، 302.AI، xAI (Grok)… ومنصات أخرى متوافقة مع OpenAI.

يُعلن عن جميع المزوّدين عبر السمة `[AiClient]`؛ ويفحص `AiClientRegistry` ويسجّل تلقائيًا عند بدء التشغيل — دون أي إعداد للمزوّدين الجدد.

---

## البدء السريع

### 1. حزمة AI SDK فقط

```bash
dotnet add package NewLife.AI
```

```csharp
using NewLife.AI.Clients;

// Single-turn chat
using var client = new DashScopeChatClient("your-api-key", "qwen-plus");
var reply = await client.ChatAsync("Explain large language models in three sentences");
Console.WriteLine(reply);

// Multi-role messages (tuple array, no manual ChatMessage construction)
var reply2 = await client.ChatAsync([
    ("system", "You are a professional C# development assistant"),
    ("user", "Explain the difference between ValueTask and Task"),
]);

// Streaming output
await foreach (var chunk in client.GetStreamingResponseAsync([
    new ChatMessage { Role = "user", Content = "Write a short poem about code" }
], new ChatOptions()))
{
    Console.Write(chunk.Text);
}
```

### 2. حقن التبعيات في ASP.NET Core

```bash
dotnet add package NewLife.AI.Extensions
```

```csharp
// Register service
builder.Services.AddDashScope("your-api-key", "qwen-plus");

// Keyed multi-provider coexistence
builder.Services.AddOpenAI("openai-key", serviceKey: "openai");
builder.Services.AddAnthropic("anthropic-key", serviceKey: "anthropic");

// Inject and use
public class MyService(IChatClient chatClient)
{
    public Task<String> ChatAsync(String question)
        => chatClient.ChatAsync(question);
}
```

### 3. استدعاء الدوال (الأدوات)

```csharp
public class MyTools
{
    /// <summary>Get weather for a specified city</summary>
    [ToolDescription("get_weather")]
    public async Task<String> GetWeatherAsync(
        [Description("City name")] String city)
        => $"{city}: Sunny, 22°C today";
}

// Register tools
var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// Add to pipeline — automatic multi-turn tool calling loop
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// Model auto-calls get_weather("Beijing"), returns final text answer
var reply = await client.ChatAsync("What's the weather like in Beijing today?");
```

### 4. الاستدلال المقيّد (نمط ReAct)

عندما يميل الذكاء الاصطناعي إلى التخمين أو اتباع اختصارات في المشكلات المعقدة، استخدم **نمط ReAct** في System Prompt لفرض الاستدلال خطوة بخطوة. يتضمّن `ToolChatClient` حلقة `while` مدمجة (`MaxIterations = 10`) — دون حاجة إلى كتابة Agent يدويًا. ادمجه مع ReAct System Prompt لتقييد اتجاه الاستدلال في كل خطوة بقدر أكبر.

```csharp
// UseTools pipeline: ToolChatClient loops automatically until the model stops calling tools
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// ReAct System Prompt: Thought → Action → Observation → Answer enforces step-by-step reasoning
var reply = await client.ChatAsync([
    ("system",
        "You must reason step by step in the following format. Do not skip steps or guess directly:\n" +
        "Thought: <analyze what needs to be done>\n" +
        "Action: <which tool to call and parameters>\n" +
        "Observation: <analyze the tool result>\n" +
        "(Repeat Thought/Action/Observation until sufficient information is gathered)\n" +
        "Answer: <final conclusion based on the above observations>\n\n" +
        "Rule: Do not give an Answer without first calling a tool."),
    ("user", "Is the weather in Beijing suitable for outdoor sports today?"),
]);
```

### 5. تشغيل تطبيق الدردشة الويب الكامل

```bash
git clone https://github.com/NewLifeX/NewLife.AI.git
cd NewLife.AI

# Build frontend (requires Node.js + pnpm)
cd Web && pnpm install && pnpm build && cd ..

# Start
cd NewLife.ChatAI
dotnet run --framework net8.0
```

افتح `http://localhost:5000` في متصفحك — SQLite افتراضيًا، وجاهز للاستخدام مباشرة. عند أول تشغيل، اضبط مفاتيح API للمزوّدين عبر `/Admin`.

يمكنك أيضًا تضمين `NewLife.ChatAI` في مشروع قائم عبر NuGet:

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

## بوابة API

يتضمّن NewLife.ChatAI بوابة ذكاء اصطناعي مدمجة متعددة البروتوكولات. يمكن للأنظمة الخارجية التكامل دون تعديل — إذ تمر جميع المسارات عبر حقن الذاكرة وتعزيز المهارات.

| البروتوكول | المسار | الوصف |
|----------|-------|-------------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | متدفّق / غير متدفّق / استدعاء الدوال / الرؤية |
| OpenAI Responses | `POST /v1/responses` | نماذج الاستدلال o3 / gpt-5 |
| Anthropic Messages | `POST /v1/messages` | سلسلة Claude |
| Google Gemini | `POST /v1/gemini/...` | سلسلة Gemini |
| Image Generation | `POST /v1/images/generations` | نص إلى صورة |
| Image Edit | `POST /v1/images/edits` | الترميم الداخلي (multipart/form-data) |
| Model Discovery | `GET /v1/models` | قائمة النماذج المتاحة |

**المصادقة**: `Authorization: Bearer sk-xxxx` (AppKey)

**الميزات**: إعادة محاولة بتراجع أُسّي عند استجابة 429 من المنبع (اهتزاز عشوائي، حتى 5 محاولات)؛ وتسجيل تلقائي لاستخدام Token؛ وإحصاءات ثنائية الأبعاد حسب AppKey + User

---

## تطوير الإضافات

### إضافة مزوّد ذكاء اصطناعي جديد

ورّث من `OpenAIChatClient` وأضف السمة `[AiClient]` — يفحص `AiClientRegistry` تلقائيًا عند بدء التشغيل:

```csharp
[AiClient("MyAI", "My Service", "https://api.myai.com/v1",
    Description = "Custom AI Service")]
[AiClientModel("myai-latest", "MyAI Latest", Code = "MyAI",
    FunctionCalling = true, Vision = true)]
public class MyAiChatClient : OpenAIChatClient
{
    public MyAiChatClient() { }
    public MyAiChatClient(String apiKey, String? model = null, String? endpoint = null)
        : base(apiKey, model, endpoint) { }
}
```

### إضافة أداة جديدة

```csharp
public class MyTools
{
    /// <summary>Query current time</summary>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// DI scenario
services.AddSingleton<IToolProvider>(_ =>
{
    var r = new ToolRegistry();
    r.AddTools<MyTools>(new MyTools());
    return r;
});
```

### إضافة IChatHandler جديد

احقن منطقًا مخصصًا قبل الدردشة (OnBefore) أو بعدها (OnAfter) — مثل حقن السياق أو التدقيق أو التسجيل:

```csharp
[ChatHandlerOrder(150)]
public class CurrentTimeHandler : ChatHandlerBase
{
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        context.ContextMessages.Insert(0,
            new ChatMessage { Role = "system", Content = $"Current time: {DateTime.Now:yyyy-MM-dd HH:mm}" });
        return Task.CompletedTask;
    }
}

// DI registration — MessageFlow auto-sorts by ChatHandlerOrderAttribute
services.AddSingleton<IChatHandler, CurrentTimeHandler>();
```

### إضافة مرشّح (Filter) جديد

نموذج الحلقات المتداخلة (onion-ring) لحقن التسجيل والتدقيق ومراجعة المحتوى قبل الدردشة وبعدها:

```csharp
public class AuditFilter : IChatFilter
{
    public async Task OnChatAsync(
        ChatFilterContext ctx,
        Func<ChatFilterContext, CancellationToken, Task> next,
        CancellationToken ct)
    {
        // before: log input / filter sensitive content
        await next(ctx, ct);
        // after: log output / write audit log
    }

    public Task OnStreamCompletedAsync(ChatFilterContext ctx, CancellationToken ct)
        => Task.CompletedTask;
}
```

---

## التوثيق

| المستند | الوصف |
|----------|-------------|
| [مواصفات المتطلبات](Doc/需求规格说明.md) | أهداف المنتج، وقائمة الميزات، والمتطلبات غير الوظيفية |
| [تصميم البنية المعمارية](Doc/架构设计.md) | بنية من أربع طبقات، وتفاصيل تصميم الوحدات |
| [إطار تنسيق الذكاء الاصطناعي](Doc/AI编排框架需求.md) | تصميم الأدوات / الوكلاء / المخطِّط |
| [متطلبات بوابة API](Doc/API网关需求.md) | تفاصيل تكييف بروتوكولات البوابة |
| [بنية MCP](Doc/MCP架构.md) | تصميم عميل وخادم MCP |
| [إدارة المهارات](Doc/技能管理需求.md) | التصميم التفصيلي لنظام المهارات |
| [نظام التعلّم الذاتي](Doc/自学习系统需求.md) | تحليل المحادثات + استخراج الذاكرة |
| [تدفق حفظ بيانات الدردشة](Doc/对话数据保存流程.md) | العملية التفصيلية لـ MessageFlow |
| [توافق AOT](Doc/AOT兼容性分析.md) | حدود دعم NativeAOT، ومصفوفة الفحص الدخاني، وتغطية المنصات/المعماريات |
| [التعريب وقت التشغيل](Doc/多语言本地化.md) | 31 لغة وقت تشغيل، وجداول اللغات، وسير عمل المساهمة |
| [قائمة وحدات الميزات](Doc/功能模块清单.md) | جرد كامل للميزات |

---

## خريطة المنظومة

NewLife منظومة متعددة الطبقات من مشاريع مفتوحة المصدر تُصدَر بإصدارات مستقلة. الخريطة أدناه مشتركة حرفيًا بين جميع مستودعات NewLife؛ ويرتبط كل مشروع بمستودعه وحزمة NuGet الأساسية الخاصة به.

| المشروع | الوظيفة | التنزيلات |
| --- | --- | --- |
| **الأساس** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | التسجيل، والإعدادات، والتخزين المؤقت، والشبكات، والتسلسل، والجدولة، وتتبّع APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | وسيط بيانات: جداول مفردة بمقياس عشرات المليارات، وتقسيم تلقائي، وفصل القراءة/الكتابة، وأكثر من 10 قواعد بيانات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | مكتبة شبكات عالية الأداء: 1.77M pps / 14.5 Gbps على جهاز واحد (TCP 1KB، 2026-10)، و4M اتصال TCP طويل الأمد. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | إطار اتصال بروتوكولي وRPC لتطبيقات العميل/الخادم وأجهزة إنترنت الأشياء. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | تثبيت تطبيقات .NET كخدمات خلفية في نظام التشغيل: خدمات Windows وsystemd في Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | قوالب مشاريع `dotnet new` لتطبيقات الطرفية والويب وAPI والخدمات والبيانات وإنترنت الأشياء. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **البيانات والرسائل** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | عميل Redis بزمن استجابة بالميكروثانية، وملايين العمليات/ثانية، وقوائم انتظار رسائل مدمجة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | عميل RocketMQ مُدار بالكامل لـ Apache RocketMQ وAlibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | عميل وخادم MQTT، بما في ذلك الاتصال بـ Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | قاعدة بيانات هجينة بلغة C#: مدمجة أو شبكية، وعلائقية وسلاسل زمنية وقائمة انتظار رسائل وKV في واحد. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | مشغّل MySql وطبقة مساعدة مبنية على حزمة بيانات NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **الويب والذكاء الاصطناعي والأنظمة الموزعة** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | إطار سريع لمنصات الإدارة: الأذونات، وSSO، وخادم OAuth، وتوليد CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | منصة خدمات موزعة: إدارة العقد، وAPM، ومركز الإعدادات، والسجل، ومركز الإصدارات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | منصة جدولة مهام موزعة وبيانات فورية مع التقسيم وإعادة المحاولة والتعبئة الرجعية. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | عميل موحّد لـ 46 مزوّد LLM بالإضافة إلى ChatAI، تطبيق دردشة ويب جاهز للتشغيل. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **الصناعة وإنترنت الأشياء** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | مكتبة إنترنت أشياء قياسية تحدد عقود البروتوكولات وتجريدات الأجهزة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | تطبيق Modbus TCP/RTU/ASCII فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | بروتوكول Siemens PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | بروتوكول Omron PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | بروتوكول Mitsubishi Melsec PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | عميل OPC للوصول إلى البيانات الصناعية. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | حزمة بروتوكول BACnet لأتمتة المباني. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | منصة إنترنت أشياء مفتوحة المصدر خفيفة مع إدارة البوابات والأجهزة. | — |
| **تليماتيك المركبات** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | بروتوكول تحديد مواقع المركبات JT/T 808: أكثر من 70 جسم رسالة، وإصدارات 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | بروتوكول JT/T 809 بين المنصات لهيئات النقل. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | فيديو داخل المركبة JT/T 1078: تحليل RTP، وإعادة تغليف FLV/fMP4، والبث المباشر. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **مكونات المجال** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | حزمة واحدة لـ Excel وWord وPPT وPDF وأكثر من 15 صيغة مستندات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | صوت عبر المنصات: المرمّزات، وإدخال/إخراج الأجهزة، وDSP، والحاويات، والبث. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | مرمّزات الفيديو، وصيغ الحاويات، ومعالجة الإطارات، والبث. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | واجهة موحّدة فوق Baidu وAMap وTencent وTianditu مع الترميز الجغرافي وتحويل الإحداثيات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | تحديد الموقع الجغرافي لـ IPv4 بقاعدة بيانات offline معيّنة في الذاكرة مع API عبر الإنترنت كبديل. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **الأدوات والمنتجات** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | صندوق أدوات المطوّر: الشبكة، والمنفذ التسلسلي، والتشفير، والتعبيرات النمطية، وModbus وMQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | نظام إدارة شيفرة Git مستضاف ذاتيًا. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | يثبّت وقت تشغيل .NET المطابق تلقائيًا عند التشغيل من مضيف أقدم. | — |

> تعرض الشارات الحزمة الأساسية لكل مشروع؛ أما حزم السمات والإضافات والحزم التابعة فمدرجة في صفحات NuGet الخاصة بها. والحزم التي يزيد عددها على 30 وغير المدرجة هنا (FTP، وLoRa، وإضافات Office، ومشغّلات البروتوكولات وغيرها) متاحة تحت [منظمة NewLifeX](https://github.com/NewLifeX) وعلى [newlifex.com](https://newlifex.com).

## الفريق

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

تأسّس فريق NewLife عام 2002، وأمضى أكثر من عقدين في منظومة .NET وإنترنت الأشياء، ويصون ما يقارب 100 مشروع مفتوح المصدر (34 منها أساسية في هذه الخريطة)، ويقدّم حلولًا متكاملة من البرمجيات والأجهزة لقطاعات الطاقة والخدمات اللوجستية والرعاية الصحية والتحكم الصناعي والإنترنت. ونعيش بشعار «التعلّم لا يعرف الأسبقية — الإتقان هو ما يفرض الاحترام».

حُمّلت المكونات مفتوحة المصدر <!--nuget-total:start-->6.48M<!--nuget-total:end--> مرة من NuGet (على مستوى المنظومة، حتى 2026-10).

الموقع: https://newlifex.com · منظمة المصادر المفتوحة: https://github.com/NewLifeX

<details>
<summary>حساب WeChat الرسمي: 智能大石头 (امسح الرمز للمتابعة)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## الترخيص

[رخصة MIT](LICENSE)

نرحّب بالمشكلات وطلبات السحب.

- GitHub: https://github.com/NewLifeX/NewLife.AI
- الموقع: https://newlifex.com
- مجموعة QQ: 1600800

## مشاريع ذات صلة

- [NewLife.Core](https://github.com/NewLifeX/X) — مكتبة أساس .NET
- [XCode](https://github.com/NewLifeX/X) — إطار بيانات ORM
- [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) — منصة تطوير سريع

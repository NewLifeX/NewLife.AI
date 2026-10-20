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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ 이 번역은 최신 버전보다 늦을 수 있습니다. 기준은 [English README](README.md)입니다.


## 프로젝트 소개

**NewLife.AI**는 .NET 생태계를 위한 **오픈소스 AI 기반 라이브러리**입니다. 통합된 `IChatClient` 인터페이스를 통해 **46개 주요 LLM 제공업체**를 래핑하며, 함수 호출, MCP 프로토콜, 스트리밍 출력, 멀티모달, 멀티에이전트 기능을 내장하고 있습니다. NuGet 패키지로 모든 .NET 프로젝트(`net45 → net10.0`)에 포함할 수 있습니다.

**NewLife.ChatAI**는 NewLife.AI 위에 구축된 **완전한 웹 채팅 애플리케이션**(ASP.NET Core)으로, 즉시 사용 가능한 멀티모델 채팅 프론트엔드, 통합 AI 게이트웨이, 자동 메모리 진화를 제공합니다. 독립 실행형 배포 또는 기존 ASP.NET Core 프로젝트에 NuGet으로 포함할 수 있습니다.

---

## 핵심 기능

- **NativeAOT 지원**: net8.0/net10.0에서 `IsAotCompatible` 사용, Windows / Linux / macOS와 x64 / arm64 / loongarch64를 아우르는 37 시나리오 NativeAOT 스모크 매트릭스
- **31개 런타임 언어**: 영어 원문 + 컴파일 타임 언어 테이블, 번역이 없으면 영어로 폴백하며 언어 추가에 코드 변경이 필요 없음
- **46개 AI 제공업체, 6개 프로토콜**: OpenAI / Anthropic / Gemini / Tongyi DashScope / Ollama / AWS Bedrock — 한 줄의 코드로 전환
- **통합 `IChatClient` 인터페이스**: MEAI 사양에 맞춰 단일 턴, 스트리밍, 함수 호출, 멀티모달을 단일 API로 제공
- **함수 호출(도구)**: `[ToolDescription]` 속성으로 JSON Schema 자동 생성; `ToolChatClient` 멀티턴 루프; 검색/날씨/번역/웹 스크래핑/IP 위치 등 내장 도구
- **양방향 MCP 지원**: 클라이언트는 외부 MCP Server(stdio / HTTP SSE)에 연결; 서버는 로컬 도구를 표준 MCP 서비스로 노출
- **완전한 채팅 커널**: `IChatHandler` 3단계 파이프라인(OnBefore → Execute → OnAfter); 내장 핸들러(스킬 활성화/메모리 주입/영속화/사용량 통계/제목 생성); 플러그 가능한 `IChatFilter`
- **사용자 메모리 진화**: 대화에서 10가지 범주의 구조화된 메모리를 자동 추출 — 사용할수록 사용자를 더 잘 이해
- **통합 AI 게이트웨이**: OpenAI / Anthropic / Gemini 프로토콜 호환; snake_case/camelCase 자동 적응; AppKey 멀티테넌시; 업스트림 429 지수 백오프 재시도
- **스킬 시스템**: Markdown 프롬프트 재사용, `@` 재귀 참조, 트리거 워드 자동 활성화
- **멀티에이전트**: `ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner`
- **React 19 웹 프론트엔드**: SSE 스트리밍 + 채팅 프리셋 + Artifact 실시간 미리보기(HTML/SVG/Mermaid) + 대화 분기 + 도구 호출 시각화 + 추론 타이밍 + 멀티모달
- **지식 진화 계층**: 대화에서 지식을 자동 추출, TOC 브라우징과 벡터 의미 검색이 가능한 지식 베이스 구축
- **TTS 음성 합성**: DashScope TTS 및 CosyVoice V3.5 지원; 스트리밍 음성 합성; 전용 프론트엔드 TTS API
- **임베딩/벡터 검색**: 내장 HashTextEmbedder v2 및 벡터 저장소; 지식 베이스 문서 벡터화 및 의미 검색
- **멀티에이전트 강화**: ReflectionAgent 및 ReviewAgent 추가; 복잡한 작업 분할 및 병렬 서브에이전트 집계
- **인간 개입 체크포인트**: AI 다중 경로에서 실시간 인간 선택; 다중 질문 그룹 결정 체크포인트
- **도구 호출 강화**: ToolCallContext 컨텍스트 전파; 도구 Provider 서킷 브레이커; 세션 수준 도구 가시성 필터링; 구조화된 오류 반환 및 3단계 권한 체계
- **메모리 진화**: 메모리 통합 서비스(중복 제거/병합/만료); 학습 메모리가 대화에서 구조화된 지식 자동 추출

---

## 지원 AI 제공업체

46개 제공업체: **9개 독립 프로토콜 클라이언트 + 37개 OpenAI 호환 어댑터**.

### 독립 프로토콜 구현 (9개)

| 제공업체 | 프로토콜 | 특징 |
|---------|---------|------|
| OpenAI | ChatCompletions / Responses | 비전 / 함수 호출 / 이미지 생성 / o3 추론 |
| DeepSeek | DeepSeek API | reasoning_content / DeepSeek v4 전용 매개변수 |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| Alibaba Cloud | DashScope | qwen-plus / qwen-max / Omni 전모달 / Files API |
| Azure AI | Azure OpenAI | 배포 이름 URL + api-key |
| Ollama | Ollama API | 로컬 llama / deepseek / qwen |
| AWS Bedrock | SigV4 서명 | Claude / Llama / Titan / Mistral |
| NewLifeAI | 캐스케이드 프록시 | 다중 제공업체 집계 |
| (기타) | OpenAI 호환 | 37개 호환 플랫폼 |

### OpenAI 호환 제품군 (37개)

Doubao(Volcano Engine), Zhipu Qingyan(GLM), Wenxin Yiyan, Moonshot(Kimi), MiniMax, StepFun, Baichuan, iFlytek Spark, 01.AI, Mistral, Perplexity, Cohere, Together AI, Fireworks, OpenRouter, SiliconCloud, DeepInfra, Groq, Cerebras, Hyperbolic, Nebius, Novita, Lepton, 302.AI, xAI(Grok)… 및 기타 OpenAI 호환 플랫폼.

모든 제공업체는 `[AiClient]` 속성으로 선언되며, `AiClientRegistry`가 시작 시 자동 스캔 및 등록 — 새로운 제공업체 추가는 제로 설정.

---

## 빠른 시작

### 1. AI SDK만 사용

```bash
dotnet add package NewLife.AI
```

```csharp
using NewLife.AI.Clients;

// 단일 턴 채팅
using var client = new DashScopeChatClient("your-api-key", "qwen-plus");
var reply = await client.ChatAsync("대규모 언어 모델에 대해 세 문장으로 설명해주세요");
Console.WriteLine(reply);

// 다중 역할 메시지 (튜플 배열, 수동 ChatMessage 구성 불필요)
var reply2 = await client.ChatAsync([
    ("system", "당신은 전문 C# 개발 어시스턴트입니다"),
    ("user", "ValueTask와 Task의 차이점을 설명해주세요"),
]);

// 스트리밍 출력
await foreach (var chunk in client.GetStreamingResponseAsync([
    new ChatMessage { Role = "user", Content = "코드에 관한 짧은 시를 써주세요" }
], new ChatOptions()))
{
    Console.Write(chunk.Text);
}
```

### 2. ASP.NET Core 의존성 주입

```bash
dotnet add package NewLife.AI.Extensions
```

```csharp
// 서비스 등록
builder.Services.AddDashScope("your-api-key", "qwen-plus");

// Keyed 다중 제공업체 공존
builder.Services.AddOpenAI("openai-key", serviceKey: "openai");
builder.Services.AddAnthropic("anthropic-key", serviceKey: "anthropic");

// 주입 및 사용
public class MyService(IChatClient chatClient)
{
    public Task<String> ChatAsync(String question)
        => chatClient.ChatAsync(question);
}
```

### 3. 함수 호출 (도구)

```csharp
public class MyTools
{
    /// <summary>지정된 도시의 날씨 가져오기</summary>
    [ToolDescription("get_weather")]
    public async Task<String> GetWeatherAsync(
        [Description("도시 이름")] String city)
        => $"{city}: 맑음, 22°C";
}

// 도구 등록
var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// 파이프라인에 추가 — 자동 멀티턴 도구 호출 루프
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// 모델이 자동으로 get_weather("베이징") 호출, 최종 텍스트 응답 반환
var reply = await client.ChatAsync("베이징 오늘 날씨 어때요?");
```

### 4. 제약 추론 (ReAct 패턴)

AI가 복잡한 문제에서 추측하거나 지름길을 택하는 경향이 있을 때, System Prompt에서 **ReAct 형식**을 사용하여 단계별 추론을 강제합니다. `ToolChatClient`에는 내장 `while` 루프(`MaxIterations = 10`)가 있어 수동으로 Agent를 작성할 필요가 없습니다.

```csharp
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

var reply = await client.ChatAsync([
    ("system",
        "다음 형식으로 단계별로 추론하세요. 단계를 건너뛰거나 직접 추측하지 마세요:\n" +
        "Thought: <무엇을 해야 하는지 분석>\n" +
        "Action: <어떤 도구를 호출할지, 매개변수>\n" +
        "Observation: <도구 결과 분석>\n" +
        "(충분한 정보가 수집될 때까지 Thought/Action/Observation 반복)\n" +
        "Answer: <위 관찰에 기반한 최종 결론>\n\n" +
        "규칙: 도구를 호출하지 않고 Answer를 제공하지 마세요."),
    ("user", "베이징 오늘 날씨가 야외 스포츠에 적합한가요?"),
]);
```

### 5. 전체 웹 채팅 앱 실행

```bash
git clone https://github.com/NewLifeX/NewLife.AI.git
cd NewLife.AI

# 프론트엔드 빌드 (Node.js + pnpm 필요)
cd Web && pnpm install && pnpm build && cd ..

# 시작
cd NewLife.ChatAI
dotnet run --framework net8.0
```

브라우저에서 `http://localhost:5000` 접속 — 기본 SQLite, 즉시 사용 가능. 최초 실행 시 `/Admin`에서 제공업체 API 키 설정.

NuGet을 통해 기존 프로젝트에 `NewLife.ChatAI` 포함 가능:

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

## API 게이트웨이

NewLife.ChatAI에는 다중 프로토콜 AI 게이트웨이가 내장되어 있습니다. 서드파티 시스템은 수정 없이 연결 가능 — 모든 경로가 메모리 주입과 스킬 강화를 통과합니다.

| 프로토콜 | 경로 | 설명 |
|---------|------|------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | 스트리밍 / 비스트리밍 / 함수 호출 / 비전 |
| OpenAI Responses | `POST /v1/responses` | o3 / gpt-5 추론 모델 |
| Anthropic Messages | `POST /v1/messages` | Claude 시리즈 |
| Google Gemini | `POST /v1/gemini/...` | Gemini 시리즈 |
| 이미지 생성 | `POST /v1/images/generations` | Text-to-Image |
| 이미지 편집 | `POST /v1/images/edits` | Inpainting (multipart/form-data) |
| 모델 발견 | `GET /v1/models` | 사용 가능한 모델 목록 |

**인증**: `Authorization: Bearer sk-xxxx` (AppKey)

**특징**: 업스트림 429 지수 백오프 재시도(무작위 지터, 최대 5회); Token 사용량 자동 기록; AppKey + 사용자 이중 차원 통계

---

## 확장 개발

### 새 AI 제공업체 추가

`OpenAIChatClient` 상속, `[AiClient]` 속성 추가 — `AiClientRegistry`가 시작 시 자동 스캔:

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

### 새 도구 추가

```csharp
public class MyTools
{
    /// <summary>현재 시간 조회</summary>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());
```

### 새 IChatHandler 추가

```csharp
[ChatHandlerOrder(150)]
public class CurrentTimeHandler : ChatHandlerBase
{
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        context.ContextMessages.Insert(0,
            new ChatMessage { Role = "system", Content = $"현재 시간: {DateTime.Now:yyyy-MM-dd HH:mm}" });
        return Task.CompletedTask;
    }
}

services.AddSingleton<IChatHandler, CurrentTimeHandler>();
```

---

## 문서

| 문서 | 설명 |
|------|------|
| [요구사항 명세서](Doc/需求规格说明.md) | 제품 목표, 기능 목록, 비기능 요구사항 |
| [아키텍처 설계](Doc/架构设计.md) | 4계층 아키텍처, 각 모듈 설계 세부사항 |
| [AI 오케스트레이션](Doc/AI编排框架需求.md) | 도구 / 에이전트 / 플래너 설계 |
| [API 게이트웨이 요구사항](Doc/API网关需求.md) | 게이트웨이 프로토콜 적응 세부사항 |
| [MCP 아키텍처](Doc/MCP架构.md) | MCP 클라이언트 및 서버 설계 |
| [스킬 관리](Doc/技能管理需求.md) | 스킬 시스템 상세 설계 |
| [자기 학습 시스템](Doc/自学习系统需求.md) | 대화 분석 + 메모리 추출 |
| [대화 데이터 저장 흐름](Doc/对话数据保存流程.md) | MessageFlow 상세 프로세스 |
| [기능 모듈 목록](Doc/功能模块清单.md) | 전체 기능 목록 |

---

## 생태계 지도

NewLife는 독립적으로 버전이 관리되는 오픈소스 프로젝트의 계층형 생태계입니다. 아래 지도는 모든 NewLife 리포지토리에서 그대로 동일하게 공유되며, 각 프로젝트는 코드 리포지토리와 기본 NuGet 패키지로 연결됩니다.

| 프로젝트 | 한 줄 소개 | 다운로드 |
| --- | --- | --- |
| **기반** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 로깅, 구성, 캐시, 네트워크, 직렬화, 스케줄링, APM 추적. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 데이터 미들웨어: 단일 테이블 수백억 건, 자동 분할 테이블, 읽기/쓰기 분리, 10여 종 데이터베이스 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 고성능 네트워크 라이브러리: 단일 노드 177만 pps, 14.5 Gbps(TCP 1KB, 2026-10 실측), 400만 Tcp 장기 연결. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | CS 애플리케이션과 IoT 장치를 위한 프로토콜 통신 및 RPC 프레임워크. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | .NET 애플리케이션을 운영체제 데몬으로 설치: Windows 서비스와 Linux systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | 콘솔, Web, API, 서비스, 데이터, IoT용 `dotnet new` 프로젝트 템플릿. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **데이터 및 메시징** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 클라이언트: 마이크로초 수준 지연, 백만 수준 처리량, 메시지 큐 내장. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Apache RocketMQ와 알리바바 클라우드 메시지 큐를 지원하는 순수 관리형 RocketMQ 클라이언트. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 클라이언트와 서버, 알리바바 클라우드 IoT 연결 포함. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 하이브리드 데이터베이스: 임베디드/네트워크 두 모드, 관계형, 시계열, 메시지 큐, KV를 하나로. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | NewLife 데이터 스택 위에 구축한 MySql 드라이버와 도우미 계층. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, AI 및 분산** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 빠른 관리 플랫폼 프레임워크: 권한, SSO, OAuth 서버, CRUD 스캐폴딩. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 분산 서비스 플랫폼: 노드 관리, APM, 구성 센터, 등록 센터, 배포 센터. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 샤딩, 재시도, 백필을 지원하는 분산 작업 및 실시간 데이터 스케줄링 플랫폼. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 46개 LLM 공급자를 통합 연결하는 클라이언트와 바로 사용할 수 있는 ChatAI 웹 채팅 애플리케이션. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **산업 및 IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 프로토콜 계약과 장치 추상을 정의하는 IoT 표준 라이브러리. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | NewLife.IoT 기반으로 구현한 Modbus TCP/RTU/ASCII. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | NewLife.IoT 기반으로 구현한 지멘스 PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | NewLife.IoT 기반으로 구현한 오므론 PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | NewLife.IoT 기반으로 구현한 미쓰비시 Melsec PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 산업 데이터 수집용 OPC 클라이언트. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 빌딩 자동화 프로토콜 스택. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 게이트웨이와 장치 관리를 갖춘 경량 오픈소스 IoT 플랫폼. | — |
| **차량 텔레매틱스** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 차량 위치 프로토콜: 70개 이상 메시지 본문, 2011/2013/2019 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | 운송 관리 부서를 위한 JT/T 809 플랫폼 간 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 차량용 비디오: RTP 파싱, FLV/fMP4 리먹싱, 라이브 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **도메인 컴포넌트** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Excel, Word, PPT, PDF 등 15개 이상 문서 형식을 하나의 패키지로. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 크로스 플랫폼 오디오: 코덱, 장치 IO, DSP, 컨테이너, 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 비디오 코덱, 컨테이너 형식, 프레임 처리, 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 바이두/고덕/텐센트/톈디투 지도 통합 인터페이스, 지오코딩과 좌표 변환 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 위치 정보: 오프라인 메모리 매핑 데이터베이스와 온라인 API 이중 소스. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **도구 및 제품** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 개발자 도구 모음: 네트워크, 시리얼 포트, 암호화, 정규식, Modbus, MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 셀프 호스팅 Git 소스 코드 관리 시스템. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 구버전 호스트에서 시작할 때 일치하는 .NET 런타임을 자동으로 설치합니다. | — |

> 배지는 각 프로젝트의 기본 패키지를 나타냅니다. 테마, 확장, 위성 패키지는 해당 NuGet 페이지에서 확인하세요.여기에 나열하지 않은 30여 개 패키지(FTP, LoRa, Office 확장, 각종 프로토콜 드라이버 등)는 [NewLifeX 조직](https://github.com/NewLifeX)과 [newlifex.com](https://newlifex.com)에서 확인할 수 있습니다.

## 뉴라이프 개발팀

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

뉴라이프 팀(NewLife)은 2002년에 설립되어 20여 년간 .NET과 IoT 기술 생태계에 집중하며 100여 개의 오픈소스 프로젝트를 주도하고(이 중 34개가 핵심 프로젝트), 전력, 물류, 의료, 산업 제어, 인터넷 등 산업에 소프트웨어와 하드웨어를 아우르는 통합 솔루션을 제공해 왔습니다. "학문에는 선후가 없으며, 이룬 사람이 스승이다"라는 신조를 따릅니다.

오픈소스 컴포넌트는 NuGet에서 누적 <!--nuget-total:start-->6.48M<!--nuget-total:end-->회 다운로드되었습니다(생태계 전체 기준, 2026-10).

웹사이트: https://newlifex.com · 오픈소스 조직: https://github.com/NewLifeX

<details>
<summary>위챗 공식 계정: 智能大石头(스캔하여 팔로우)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## 라이선스

[MIT License](LICENSE)

Issue와 Pull Request를 환영합니다.

- GitHub: https://github.com/NewLifeX/NewLife.AI
- 웹사이트: https://newlifex.com
- QQ 그룹: 1600800

## 관련 프로젝트

- [NewLife.Core](https://github.com/NewLifeX/X) — .NET 기반 라이브러리
- [XCode](https://github.com/NewLifeX/X) — ORM 데이터 프레임워크
- [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) — 신속 개발 플랫폼

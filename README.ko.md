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
  <b>한국어</b> |
  <a href="README.es.md">Español</a> |
  <a href="README.fr.md">Français</a> |
  <a href="README.de.md">Deutsch</a> |
  <a href="README.ru.md">Русский</a> |
  <a href="README.pt.md">Português</a>
</p>

## 프로젝트 소개

**NewLife.AI**는 .NET 생태계를 위한 **오픈소스 AI 기반 라이브러리**입니다. 통합된 `IChatClient` 인터페이스를 통해 **46개 주요 LLM 제공업체**를 래핑하며, 함수 호출, MCP 프로토콜, 스트리밍 출력, 멀티모달, 멀티에이전트 기능을 내장하고 있습니다. NuGet 패키지로 모든 .NET 프로젝트(`net45 / netstandard2.1`)에 포함할 수 있습니다.

**NewLife.ChatAI**는 NewLife.AI 위에 구축된 **완전한 웹 채팅 애플리케이션**(ASP.NET Core)으로, 즉시 사용 가능한 멀티모델 채팅 프론트엔드, 통합 AI 게이트웨이, 자동 메모리 진화를 제공합니다. 독립 실행형 배포 또는 기존 ASP.NET Core 프로젝트에 NuGet으로 포함할 수 있습니다.

---

## 핵심 기능

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

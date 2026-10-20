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
  <b>日本語</b> |
  <a href="README.ko.md">한국어</a> |
  <a href="README.es.md">Español</a> |
  <a href="README.fr.md">Français</a> |
  <a href="README.de.md">Deutsch</a> |
  <a href="README.ru.md">Русский</a> |
  <a href="README.pt.md">Português</a>
</p>

## プロジェクト概要

**NewLife.AI** は .NET エコシステム向けの**オープンソース AI 基盤ライブラリ**です。統一された `IChatClient` インターフェースを通じて **46 の主要 LLM プロバイダー**をラップし、関数呼び出し、MCP プロトコル、ストリーミング出力、マルチモーダル、マルチエージェント機能を内蔵しています。NuGet パッケージとして任意の .NET プロジェクト（`net45 / netstandard2.1`）に組み込めます。

**NewLife.ChatAI** は NewLife.AI 上に構築された**完全な Web チャットアプリケーション**（ASP.NET Core）で、すぐに使えるマルチモデルチャットフロントエンド、統合 AI ゲートウェイ、自動記憶進化を提供します。単独デプロイも、既存の ASP.NET Core プロジェクトへの NuGet 埋め込みも可能です。

---

## 主な機能

- **46 の AI プロバイダー、6 つのプロトコル**：OpenAI / Anthropic / Gemini / Tongyi DashScope / Ollama / AWS Bedrock — 1 行のコードで切り替え可能
- **統一 `IChatClient` インターフェース**：MEAI 仕様に準拠 — 単一ターン、ストリーミング、関数呼び出し、マルチモーダルを単一 API で
- **関数呼び出し（ツール）**：`[ToolDescription]` 属性で JSON Schema を自動生成；`ToolChatClient` によるマルチターンループ；検索 / 天気 / 翻訳 / Web スクレイピング / IP 位置情報など組み込みツール
- **双方向 MCP サポート**：クライアントは外部 MCP Server（stdio / HTTP SSE）に接続；サーバーはローカルツールを標準 MCP サービスとして公開
- **完全なチャットカーネル**：`IChatHandler` 3 段階パイプライン（OnBefore → Execute → OnAfter）；組み込みハンドラー（スキル活性化 / 記憶注入 / 永続化 / 使用量統計 / タイトル生成）；プラグ可能な `IChatFilter`
- **ユーザー記憶の進化**：会話から 10 カテゴリの構造化記憶を自動抽出 — 使えば使うほどユーザーを理解
- **統合 AI ゲートウェイ**：OpenAI / Anthropic / Gemini プロトコル互換；snake_case/camelCase 自動適応；AppKey マルチテナント；上流 429 指数バックオフリトライ
- **スキルシステム**：Markdown プロンプト再利用、`@` 再帰参照、トリガーワード自動活性化
- **マルチエージェント**：`ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner`
- **React 19 Web フロントエンド**：SSE ストリーミング + チャットプリセット + Artifact リアルタイムプレビュー（HTML/SVG/Mermaid）+ 会話分岐 + ツール呼び出し可視化 + 推論タイミング + マルチモーダル
- **知識進化層**：会話から知識を自動抽出し、TOC ブラウジングとベクトル意味検索を備えた検索可能な知識ベースを構築
- **TTS 音声合成**：DashScope TTS と CosyVoice V3.5 対応；ストリーミング音声合成；専用フロントエンド TTS API
- **埋め込み / ベクトル検索**：組み込み HashTextEmbedder v2 とベクトルストレージ；知識ベース文書のベクトル化と意味検索
- **マルチエージェント強化**：新規 ReflectionAgent と ReviewAgent；複雑タスクの分割と並列サブエージェント集約
- **人間参加型チェックポイント**：AI の複数パスからリアルタイムで人間が選択；多質問グループ決定チェックポイント
- **ツール呼び出し強化**：ToolCallContext コンテキスト伝播；ツール Provider サーキットブレーカー；セッションレベルのツール可視性フィルタリング；構造化エラー返却と 3 段階権限体系
- **記憶進化**：記憶統合サービス（重複排除/マージ/有効期限）；学習記憶が会話から構造化知識を自動抽出

---

## 対応 AI プロバイダー

46 プロバイダー：**9 つの独立プロトコルクライアント + 37 の OpenAI 互換アダプター**。

### 独立プロトコル実装（9 つ）

| プロバイダー | プロトコル | 特徴 |
|-------------|-----------|------|
| OpenAI | ChatCompletions / Responses | ビジョン / 関数呼び出し / 画像生成 / o3 推論 |
| DeepSeek | DeepSeek API | reasoning_content / DeepSeek v4 専用パラメータ |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| Alibaba Cloud | DashScope | qwen-plus / qwen-max / Omni 全モーダル / Files API |
| Azure AI | Azure OpenAI | デプロイ名 URL + api-key |
| Ollama | Ollama API | ローカル llama / deepseek / qwen |
| AWS Bedrock | SigV4 署名 | Claude / Llama / Titan / Mistral |
| NewLifeAI | カスケードプロキシ | マルチプロバイダー集約 |
| （その他） | OpenAI 互換 | 37 の互換プラットフォーム |

### OpenAI 互換ファミリー（37）

Doubao（Volcano Engine）、Zhipu Qingyan（GLM）、Wenxin Yiyan、Moonshot（Kimi）、MiniMax、StepFun、Baichuan、iFlytek Spark、01.AI、Mistral、Perplexity、Cohere、Together AI、Fireworks、OpenRouter、SiliconCloud、DeepInfra、Groq、Cerebras、Hyperbolic、Nebius、Novita、Lepton、302.AI、xAI（Grok）…その他 OpenAI 互換プラットフォーム。

全プロバイダーは `[AiClient]` 属性で宣言され、`AiClientRegistry` が起動時に自動スキャン・登録 — 新規プロバイダー追加はゼロ設定。

---

## クイックスタート

### 1. AI SDK のみ

```bash
dotnet add package NewLife.AI
```

```csharp
using NewLife.AI.Clients;

// 単一ターンチャット
using var client = new DashScopeChatClient("your-api-key", "qwen-plus");
var reply = await client.ChatAsync("大規模言語モデルについて3文で説明してください");
Console.WriteLine(reply);

// マルチロールメッセージ（タプル配列、手動 ChatMessage 構築不要）
var reply2 = await client.ChatAsync([
    ("system", "あなたはプロのC#開発アシスタントです"),
    ("user", "ValueTask と Task の違いを説明してください"),
]);

// ストリーミング出力
await foreach (var chunk in client.GetStreamingResponseAsync([
    new ChatMessage { Role = "user", Content = "コードについての短い詩を書いてください" }
], new ChatOptions()))
{
    Console.Write(chunk.Text);
}
```

### 2. ASP.NET Core DI

```bash
dotnet add package NewLife.AI.Extensions
```

```csharp
// サービス登録
builder.Services.AddDashScope("your-api-key", "qwen-plus");

// Keyed マルチプロバイダー共存
builder.Services.AddOpenAI("openai-key", serviceKey: "openai");
builder.Services.AddAnthropic("anthropic-key", serviceKey: "anthropic");

// 注入して使用
public class MyService(IChatClient chatClient)
{
    public Task<String> ChatAsync(String question)
        => chatClient.ChatAsync(question);
}
```

### 3. 関数呼び出し（ツール）

```csharp
public class MyTools
{
    /// <summary>指定された都市の天気を取得</summary>
    [ToolDescription("get_weather")]
    public async Task<String> GetWeatherAsync(
        [Description("都市名")] String city)
        => $"{city}：晴れ、22°C";
}

// ツール登録
var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// パイプラインに追加 — 自動マルチターンツール呼び出しループ
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// モデルが自動的に get_weather("北京") を呼び出し、最終テキスト回答を返す
var reply = await client.ChatAsync("北京の今日の天気は？");
```

### 4. 制約付き推論（ReAct パターン）

AI が複雑な問題で推測や近道をしがちな場合、System Prompt で **ReAct 形式**を使用して段階的推論を強制します。`ToolChatClient` には組み込みの `while` ループ（`MaxIterations = 10`）があり、手動で Agent を書く必要はありません。

```csharp
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

var reply = await client.ChatAsync([
    ("system",
        "以下の形式で段階的に推論してください。ステップをスキップしたり直接推測したりしないでください：\n" +
        "Thought: <何をする必要があるか分析>\n" +
        "Action: <どのツールを呼び出すか、パラメータ>\n" +
        "Observation: <ツール結果の分析>\n" +
        "（十分な情報が集まるまで Thought/Action/Observation を繰り返す）\n" +
        "Answer: <上記の観測に基づく最終結論>\n\n" +
        "ルール：ツールを呼び出さずに Answer を出さないこと。"),
    ("user", "北京の今日の天気はアウトドアスポーツに適していますか？"),
]);
```

### 5. 完全な Web チャットアプリの実行

```bash
git clone https://github.com/NewLifeX/NewLife.AI.git
cd NewLife.AI

# フロントエンドビルド（Node.js + pnpm が必要）
cd Web && pnpm install && pnpm build && cd ..

# 起動
cd NewLife.ChatAI
dotnet run --framework net8.0
```

ブラウザで `http://localhost:5000` にアクセス — デフォルト SQLite、すぐに使用可能。初回起動時に `/Admin` からプロバイダー API キーを設定。

NuGet 経由で既存プロジェクトに `NewLife.ChatAI` を埋め込むことも可能：

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

## API ゲートウェイ

NewLife.ChatAI にはマルチプロトコル AI ゲートウェイが内蔵されています。サードパーティシステムは改造なしで接続可能 — 全パスが記憶注入とスキル強化を通過します。

| プロトコル | ルート | 説明 |
|-----------|------|------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | ストリーミング / 非ストリーミング / 関数呼び出し / ビジョン |
| OpenAI Responses | `POST /v1/responses` | o3 / gpt-5 推論モデル |
| Anthropic Messages | `POST /v1/messages` | Claude シリーズ |
| Google Gemini | `POST /v1/gemini/...` | Gemini シリーズ |
| 画像生成 | `POST /v1/images/generations` | Text-to-Image |
| 画像編集 | `POST /v1/images/edits` | Inpainting（multipart/form-data）|
| モデル発見 | `GET /v1/models` | 利用可能なモデル一覧 |

**認証**：`Authorization: Bearer sk-xxxx`（AppKey）

**特徴**：上流 429 指数バックオフリトライ（ランダムジッター、最大 5 回）；Token 使用量自動記録；AppKey + ユーザーの二次元統計

---

## 拡張開発

### 新しい AI プロバイダーの追加

`OpenAIChatClient` を継承し、`[AiClient]` 属性を追加 — `AiClientRegistry` が起動時に自動スキャン：

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

### 新しいツールの追加

```csharp
public class MyTools
{
    /// <summary>現在時刻を取得</summary>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());
```

### 新しい IChatHandler の追加

```csharp
[ChatHandlerOrder(150)]
public class CurrentTimeHandler : ChatHandlerBase
{
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        context.ContextMessages.Insert(0,
            new ChatMessage { Role = "system", Content = $"現在時刻：{DateTime.Now:yyyy-MM-dd HH:mm}" });
        return Task.CompletedTask;
    }
}

services.AddSingleton<IChatHandler, CurrentTimeHandler>();
```

---

## ドキュメント

| ドキュメント | 説明 |
|-------------|------|
| [要件仕様書](Doc/需求规格说明.md) | 製品目標、機能一覧、非機能要件 |
| [アーキテクチャ設計](Doc/架构设计.md) | 4 層アーキテクチャ、各モジュール設計詳細 |
| [AI オーケストレーション](Doc/AI编排框架需求.md) | ツール / エージェント / プランナー設計 |
| [API ゲートウェイ要件](Doc/API网关需求.md) | ゲートウェイプロトコル適応詳細 |
| [MCP アーキテクチャ](Doc/MCP架构.md) | MCP クライアント・サーバー設計 |
| [スキル管理](Doc/技能管理需求.md) | スキルシステム詳細設計 |
| [自己学習システム](Doc/自学习系统需求.md) | 会話分析 + 記憶抽出 |
| [会話データ保存フロー](Doc/对话数据保存流程.md) | MessageFlow 詳細プロセス |
| [機能モジュール一覧](Doc/功能模块清单.md) | 完全な機能一覧 |

---

## エコシステムマップ

NewLife は、独立したバージョン管理を行うオープンソースプロジェクトの階層型エコシステムです。以下のマップはすべての NewLife リポジトリで逐語的に同一の内容を共有しており、各プロジェクトはコードリポジトリと主要な NuGet パッケージにリンクしています。

| プロジェクト | 概要 | ダウンロード数 |
| --- | --- | --- |
| **基盤** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | ログ、構成、キャッシュ、ネットワーク、シリアライズ、スケジューリング、APM トレーシング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | データミドルウェア：単一テーブルで数百億件級、自動シャーディング、読み書き分離、10 種類以上のデータベースに対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高性能ネットワークライブラリ：単一マシンで 177 万 pps、14.5 Gbps（TCP 1KB、2026-10 実測）、400 万 Tcp 長接続。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | CS アプリケーションと IoT デバイス向けのプロトコル通信と RPC フレームワーク。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | .NET アプリケーションを OS デーモンとしてインストール：Windows サービスと Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | コンソール、Web、API、サービス、データ、IoT 向けの `dotnet new` プロジェクトテンプレート。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **データとメッセージング** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | マイクロ秒級の遅延、数百万 ops/s のスループット、メッセージキュー内蔵の Redis クライアント。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Apache RocketMQ と Alibaba Cloud Message Queue に対応した純マネージド RocketMQ クライアント。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT クライアントとサーバー。Alibaba Cloud IoT への接続を含みます。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 製ハイブリッドデータベース：組み込み/ネットワークの両モード、リレーショナル、時系列、メッセージキュー、KV を 1 つに。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | NewLife データスタック上に構築された MySql ドライバーとヘルパー層。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI、分散** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 高速管理プラットフォームフレームワーク：権限、SSO、OAuth サーバー、CRUD スキャフォールディング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 分散サービスプラットフォーム：ノード管理、APM、構成センター、登録センター、リリースセンター。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | シャーディング、リトライ、バックフィルに対応した分散タスクとリアルタイムデータのスケジューリングプラットフォーム。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 46 社の LLM プロバイダーに統一接続するクライアントと、すぐに使える Web チャットアプリ ChatAI。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **産業と IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | プロトコル契約とデバイス抽象を定義する IoT 標準ライブラリ。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | NewLife.IoT 上に実装された Modbus TCP/RTU/ASCII。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | NewLife.IoT 上に実装されたシーメンス PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | NewLife.IoT 上に実装されたオムロン PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | NewLife.IoT 上に実装された三菱 Melsec PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 産業データアクセス向けの OPC クライアント。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet ビル管理プロトコルスタック。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | ゲートウェイとデバイス管理を備えた軽量なオープンソース IoT プラットフォーム。 | — |
| **車載テレマティクス** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 車両位置情報プロトコル：70 以上のメッセージボディ、2011/2013/2019 に対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | 運輸管理部門向けの JT/T 809 プラットフォーム間プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 車載ビデオ：RTP 解析、FLV/fMP4 リマックス、ライブ配信。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **ドメインコンポーネント** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Excel、Word、PPT、PDF など 15 以上のドキュメント形式を 1 パッケージで。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | クロスプラットフォーム音声：コーデック、デバイス IO、DSP、コンテナ、ストリーミング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | ビデオコーデック、コンテナ形式、フレーム処理、ストリーミング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度地図、高徳地図、テンセント地図、天地図を統一インターフェイス化。ジオコーディングと座標変換に対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 位置情報：オフラインのメモリマップデータベースとオンライン API のフォールバック。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **ツールと製品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 開発者向けツールボックス：ネットワーク、シリアルポート、暗号化、正規表現、Modbus、MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | セルフホスト型 Git ソースコード管理システム。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 古いホストから起動した際に対応する .NET ランタイムを自動インストールします。 | — |

> バッジは各プロジェクトの主要パッケージを示します。テーマ、拡張、サテライトパッケージは各 NuGet ページを参照してください。ここに記載していない 30 以上のパッケージ（FTP、LoRa、Office 拡張、各種プロトコルドライバーなど）は [NewLifeX 組織](https://github.com/NewLifeX) と [newlifex.com](https://newlifex.com) で公開しています。

## 新生命開発チーム

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命チームは 2002 年に設立され、20 年以上にわたり .NET と IoT の技術エコシステムに注力し、100 近いオープンソースプロジェクトを主導し（うち 34 が主要プロジェクト）、電力、物流、医療、産業制御、インターネットなどの業界にソフトウェアとハードウェアを一体化したソリューションを提供しています。「学問に先後なし、達する者が師である」を信条としています。

オープンソースコンポーネントは NuGet で累計 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 回ダウンロードされています（エコシステム全体、2026-10 時点）。

ウェブサイト：https://newlifex.com · オープンソース組織：https://github.com/NewLifeX

<details>
<summary>WeChat 公式アカウント：智能大石头（スキャンしてフォロー）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## ライセンス

[MIT License](LICENSE)

Issue と Pull Request を歓迎します。

- GitHub：https://github.com/NewLifeX/NewLife.AI
- 公式サイト：https://newlifex.com
- QQ グループ：1600800

## 関連プロジェクト

- [NewLife.Core](https://github.com/NewLifeX/X) — .NET 基盤ライブラリ
- [XCode](https://github.com/NewLifeX/X) — ORM データフレームワーク
- [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) — 迅速開発プラットフォーム

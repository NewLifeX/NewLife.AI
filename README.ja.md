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

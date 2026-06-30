# ChatAI Docker 部署指南

> 版本：v1.0 | 日期：2026-06-30

本文档说明如何通过 Docker 一键部署 ChatAI（开源社区版 Web 对话应用）。

## 前提

- 安装 [Docker](https://docs.docker.com/get-docker/) 和 Docker Compose
- （可选）准备好模型服务商的 API Key（OpenAI / DashScope 等）

## 快速开始

```bash
# 1. 进入 ChatAI 目录
cd NAI

# 2. 启动（首次启动会自动构建镜像，约 2-3 分钟）
docker compose up -d

# 3. 访问
# 打开浏览器 http://localhost:5080
```

首次启动后访问 `http://localhost:5080`，系统会在 `Data/` 目录下自动创建 SQLite 数据库并初始化表结构（由 XCode 自动迁移完成）。

## 默认配置

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| 端口 | 5080 | 通过 `docker-compose.yml` 修改 |
| 数据库 | SQLite（`Data/chatai.db`） | 可切换 PostgreSQL，见下方 |
| 数据持久化 | Docker Volume `chatai_data` | 映射到 `./Data/` 目录 |

## 配置模型服务商

启动后进入后台管理（`http://localhost:5080/admin`），在「提供商配置」中添加服务商信息（Endpoint + ApiKey）。也可以在 `docker-compose.yml` 中通过环境变量注入。

## 可选：切换到 PostgreSQL

编辑 `docker-compose.yml`，取消 `postgres` 服务的注释，并修改 ChatAI 的数据库连接串：

```yaml
environment:
  - ConnectionStrings__ChatAI=Server=postgres;Port=5432;Database=chatai;User Id=chatai;Password=chatai123;
```

## 查看日志

```bash
docker compose logs -f chatai
```

## 更新

```bash
docker compose down
docker compose build --no-cache
docker compose up -d
```

## StarChat 部署

StarChat（商用增强版）同样支持 Docker 部署：

```bash
cd NewLife.StarChat
docker compose up -d
```

打开浏览器访问 `http://localhost:5080`。首次启动自动创建 SQLite 数据库并初始化表结构。

> **生产环境建议**：企业客户也可选择 IIS / Windows 服务部署。Docker 方案主要用于快速试用和 Linux 服务器场景。

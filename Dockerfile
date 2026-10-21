# ChatAI 镜像：前端构建 → 后端发布 → 运行时
#
# 构建上下文为仓库根：docker build -t chatai .
# 前端产物由 Vite 直接输出到 NewLife.ChatAI/wwwroot，随后由后端作为嵌入资源打进 DLL。

# Stage 1: 构建前端
FROM node:22-alpine AS web
WORKDIR /src/Web
RUN corepack enable && corepack prepare pnpm@10.17.1 --activate
COPY Web/package.json Web/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY Web/ ./
RUN pnpm build

# Stage 2: 发布后端（net10.0，与运行时镜像保持一致）
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 只复制还原所需的项目文件，源码变更不破坏依赖缓存
COPY NewLife.AI/NewLife.AI.csproj NewLife.AI/
COPY NewLife.ChatAI/NewLife.ChatAI.csproj NewLife.ChatAI/
RUN dotnet restore NewLife.ChatAI/NewLife.ChatAI.csproj

COPY . .
# 用容器内构建的前端覆盖本地可能已过期的 wwwroot
COPY --from=web /src/NewLife.ChatAI/wwwroot NewLife.ChatAI/wwwroot

RUN dotnet publish NewLife.ChatAI/NewLife.ChatAI.csproj -c Release -f net10.0 -o /app --no-restore

# Stage 3: 运行时
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# SQLite 数据库目录，建议用卷挂载持久化
RUN mkdir -p /app/Data

# 环境变量（部署时通过 docker-compose 或 -e 覆盖）
ENV ASPNETCORE_URLS=http://+:5080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5080

ENTRYPOINT ["dotnet", "ChatAI.dll"]

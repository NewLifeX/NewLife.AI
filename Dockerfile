# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 复制项目文件并还原依赖
COPY NAI/NewLife.AI/*.csproj NAI/NewLife.AI/
COPY NAI/NewLife.AI.Extensions/*.csproj NAI/NewLife.AI.Extensions/
COPY NAI/NewLife.ChatAI/*.csproj NAI/NewLife.ChatAI/
RUN dotnet restore NAI/NewLife.ChatAI/NewLife.ChatAI.csproj

# 复制全部源码
COPY . .

# 发布
WORKDIR /src/NAI/NewLife.ChatAI
RUN dotnet publish NewLife.ChatAI.csproj -c Release -o /app --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# 环境变量（部署时通过 docker-compose 或 -e 覆盖）
ENV ASPNETCORE_URLS=http://+:5080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5080

ENTRYPOINT ["dotnet", "NewLife.ChatAI.dll"]

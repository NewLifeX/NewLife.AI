#!/bin/bash

# NewLife.AI 启动脚本
# 用法:
#   ./start.sh          启动前端 + 后端
#   ./start.sh front    仅启动前端
#   ./start.sh back     仅启动后端
#   ./start.sh stop     停止所有服务

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BACKEND_DIR="$SCRIPT_DIR/NewLife.ChatAI"
FRONTEND_DIR="$SCRIPT_DIR/Web"
PID_DIR="$SCRIPT_DIR/.pids"

mkdir -p "$PID_DIR"

start_backend() {
    if [ -f "$PID_DIR/backend.pid" ] && kill -0 "$(cat "$PID_DIR/backend.pid")" 2>/dev/null; then
        echo "[后端] 已在运行 (PID: $(cat "$PID_DIR/backend.pid"))"
        return
    fi
    echo "[后端] 还原依赖 ..."
    cd "$SCRIPT_DIR" && dotnet restore NewLife.AI/NewLife.AI.csproj > /dev/null 2>&1
    echo "[后端] 启动 NewLife.StarChat (http://localhost:5080) ..."
    cd "$BACKEND_DIR" && dotnet run --framework net8.0 -p:TargetFrameworks=net8.0 --no-restore --urls "http://localhost:5080" > "$PID_DIR/backend.log" 2>&1 &
    echo $! > "$PID_DIR/backend.pid"
    echo "[后端] 已启动 (PID: $!)"
}

start_frontend() {
    if [ -f "$PID_DIR/frontend.pid" ] && kill -0 "$(cat "$PID_DIR/frontend.pid")" 2>/dev/null; then
        echo "[前端] 已在运行 (PID: $(cat "$PID_DIR/frontend.pid"))"
        return
    fi
    echo "[前端] 启动 Vite Dev Server ..."
    cd "$FRONTEND_DIR" && npm run dev > "$PID_DIR/frontend.log" 2>&1 &
    echo $! > "$PID_DIR/frontend.pid"
    echo "[前端] 已启动 (PID: $!)"
}

stop_service() {
    local name=$1
    local pidfile="$PID_DIR/$name.pid"
    if [ -f "$pidfile" ]; then
        local pid
        pid=$(cat "$pidfile")
        if kill -0 "$pid" 2>/dev/null; then
            kill "$pid" 2>/dev/null
            # 等待进程退出，最多 5 秒
            for i in $(seq 1 10); do
                kill -0 "$pid" 2>/dev/null || break
                sleep 0.5
            done
            # 还没退出就强制杀
            kill -0 "$pid" 2>/dev/null && kill -9 "$pid" 2>/dev/null
            echo "[$name] 已停止 (PID: $pid)"
        else
            echo "[$name] 进程不存在"
        fi
        rm -f "$pidfile"
    else
        echo "[$name] 未在运行"
    fi
}

stop_all() {
    stop_service "backend"
    stop_service "frontend"
}

show_status() {
    for name in backend frontend; do
        local pidfile="$PID_DIR/$name.pid"
        if [ -f "$pidfile" ] && kill -0 "$(cat "$pidfile")" 2>/dev/null; then
            echo "[$name] 运行中 (PID: $(cat "$pidfile"))"
        else
            echo "[$name] 未运行"
        fi
    done
}

case "${1:-all}" in
    front|frontend|web)
        start_frontend
        ;;
    back|backend|api)
        start_backend
        ;;
    all)
        start_backend
        start_frontend
        echo ""
        echo "后端: http://localhost:5080"
        echo "前端: http://localhost:5010 (代理 -> 5080)"
        echo ""
        echo "查看日志: tail -f $PID_DIR/backend.log"
        echo "         tail -f $PID_DIR/frontend.log"
        echo "停止服务: $0 stop"
        ;;
    stop)
        stop_all
        ;;
    status)
        show_status
        ;;
    *)
        echo "用法: $0 {all|front|back|stop|status}"
        exit 1
        ;;
esac

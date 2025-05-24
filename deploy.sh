#!/bin/bash

# 部署脚本 (deploy.sh)
set -e  # 遇到错误立即退出

# 输出当前时间
echo "开始部署: $(date)"

# 切换到项目目录
cd "$(dirname "$0")"
echo "当前目录: $(pwd)"

# 获取当前分支
current_branch=$(git rev-parse --abbrev-ref HEAD)
echo "当前分支: $current_branch"

# 检查是否有本地变更
if [[ -n $(git status --porcelain) ]]; then
    echo "检测到本地变更，执行stash..."
    git stash -u
    echo "本地变更已暂存"
fi

# 拉取最新代码
echo "拉取最新代码..."
git fetch origin
git pull origin "$current_branch" --rebase

# 重启 Docker 服务
echo "重启 Docker 服务..."
docker-compose down
docker-compose up -d --build

# 显示容器状态
echo "容器状态:"
docker-compose ps

echo "部署完成: $(date)"
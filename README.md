# Trading Service Documentation

## Telegram 命令列表

### 基础命令

#### `/help`
显示帮助信息

#### `/strategy`
策略管理命令，支持以下操作：
- create：创建策略
- delete：删除策略
- pause：暂停策略
- resume：恢复策略

示例：

1. 创建策略：
```json
/strategy create {
    "Symbol": "BTCUSDT",
    "Amount": 1000,
    "Volatility": 0.2,
    "Leverage": 5,
    "AccountType": "Spot",
    "StrategyType": "BottomBuy"
}
```

2. 删除策略：
```
/strategy delete 12345
```

#### `/alert`
警报管理命令，支持以下操作：
- create：创建警报
- delete：删除警报
- empty：清空所有警报
- pause：暂停警报
- resume：恢复警报

示例：

1. 创建警报（支持间隔: 5m,15m,1h,4h,1d）：
```json
/alert create {
    "Symbol": "BTCUSDT",
    "Interval": "4h",
    "Expression": "Math.abs((close - open) / open) >= 0.02"
}
```

2. 删除警报：
```
/alert delete 12345
```

3. 清空所有警报：
```
/alert empty
```

## 部署说明

### Docker部署
1. 构建镜像:
```bash
cd Trading && docker build -t trading-api:latest -f "src/Trading.API/Dockerfile" .
```

2. 使用deploy.sh部署:
```bash
./deploy.sh
```

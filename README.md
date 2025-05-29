# Trading Service Documentation


## 目录
- [基础命令](#基础命令)
- [策略管理](#策略管理)
  - [策略类型说明](#策略类型说明)
  - [策略示例](#策略示例)
- [警报管理](#警报管理)
- [部署说明](#部署说明)

## 基础命令

| 命令 | 说明 | 子命令 |
|------|------|--------|
| `/help` | 显示帮助信息 | - |
| `/strategy` | 策略管理 | create, delete, pause, resume |
| `/alert` | 警报管理 | create, delete, empty, pause, resume |

## 策略管理

### 策略类型说明

#### RestClient 策略
- **BottomBuy** 和 **TopSell**: 基于当天开盘价格执行的策略
- 特点：不需要等待收盘，第二天自动管理
- 适用：日线级别交易

#### WebSocket 策略
- **CloseBuy** 和 **CloseSell**: 基于指定周期收盘价格执行的策略
- ⚠️ 注意：必须等待当前周期收盘后才会执行下单
- 适用：更灵活的周期选择

### 策略示例

#### 1. 现货做多策略 (BottomBuy)
```json
/strategy create {
    "Symbol": "BTCUSDT",
    "Amount": 1000,
    "Volatility": 0.2,
    "Interval": "1d",
    "Leverage": 5,
    "AccountType": "Spot",
    "StrategyType": "BottomBuy"
}
```

#### 2. 合约做空策略 (TopSell)
```json
/strategy create {
    "Symbol": "BTCUSDT",
    "Amount": 1000,
    "Volatility": 0.2,
    "Interval": "1d",
    "Leverage": 5,
    "AccountType": "Future",
    "StrategyType": "TopSell"
}
```

#### 3. WebSocket合约做空策略 (CloseSell)
```json
/strategy create {
    "Symbol": "BTCUSDT",
    "Amount": 1000,
    "Volatility": 0.002,
    "Interval": "4h",
    "AccountType": "Future",
    "StrategyType": "CloseSell"
}
```

#### 4. WebSocket合约做多策略 (CloseBuy)
```json
/strategy create {
    "Symbol": "BTCUSDT",
    "Amount": 1000,
    "Volatility": 0.002,
    "Interval": "4h",
    "AccountType": "Future",
    "StrategyType": "CloseBuy"
}
```

#### 删除策略
```
/strategy delete <Id>
```

## 警报管理

### 支持的时间间隔
- 5m (5分钟)
- 15m (15分钟)
- 1h (1小时)
- 4h (4小时)
- 1d (1天)
- 3d (3天)
- 1w (1周)

### 警报示例

#### 1. 价格波动警报
```json
/alert create {
    "Symbol": "BTCUSDT",
    "Interval": "4h",
    "Expression": "Math.abs((close - open) / open) >= 0.02"
}
```

#### 2. 价格阈值警报
```json
/alert create {
    "Symbol": "BTCUSDT",
    "Interval": "4h",
    "Expression": "close > 20000"
}
```

#### 删除指定警报
```
/alert delete <Id>
```

#### 清空所有警报
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
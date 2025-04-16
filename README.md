# Trading Service Documentation

## Telegram 命令列表

### 基础命令

#### `/help`
显示帮助信息

#### `/status`
查看所有策略和警报状态
- 输出内容包含:
  - 策略ID
  - 状态图标(🟢运行中, 🔴已暂停, ⚠️未知状态)
  - 交易对信息
  - 目标价格和跌幅
  - 交易金额和数量
  - 最后更新时间

#### `/create`
创建新的交易策略
```json
/create {
  "Symbol": "BTCUSDT",      // 交易对
  "Amount": 1000,           // 交易金额
  "PriceDropPercentage": 0.2, // 目标跌幅(%)
  "Leverage": 5,            // 杠杆倍数
  "AccountType": "Spot",    // 账户类型(Spot/Futures)
  "StrategyType": "BottomBuy" // 策略类型
}
```

#### `/delete`
删除指定策略
```
/delete <策略ID>
```

#### `/stop`
暂停所有策略运行

#### `/resume`
恢复所有策略运行

#### `/alarm`
价格警报管理

1. 创建警报:
```
/alarm <币种> <时间间隔> <条件表达式>
```
- 币种: 如 BTCUSDT
- 时间间隔: 支持 5m,15m,1h,4h,1d
- 条件表达式: JavaScript 表达式(4个参数可以使用： close open high low)

示例:
```
/alarm BTCUSDT 1h close > 50000
/alarm BTCUSDT 4h Math.abs((close - open) / open) >= 0.02
```

2. 清空所有警报:
```
/alarm empty
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
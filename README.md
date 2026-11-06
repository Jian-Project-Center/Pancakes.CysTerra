# PancakesAPI（CysTerra）

[CysTerra](https://docs.eew.cryville.world/) 通用版扩展：订阅 **PancakesAPI** 的 GlobalQuake（`/gq`）通道，将全球地震检知/更新接入 CysTerra 的事件摘要、事件详情与语音播报。

行为对齐官方 Cryville.EEW.GlobalQuake（官方旧 TCP 服务已停）；数据改由 PancakesAPI WebSocket 提供。对齐接口层 **Cryville.EEW 0.8.14**、Features / TTS **0.8.1**。

## 安装

扩展仅适用于 CysTerra **通用版**（Android 版不支持扩展）。以下三个包均需安装，版本 **0.4.7.3**：

| 组件 | 导入包 |
| --- | --- |
| 事件源 + 事件摘要 | `Pancakes.CysTerra-0.4.7.3.zip` |
| 事件详情 | `Pancakes.CysTerra.Features-0.4.7.3.zip` |
| 语音消息 | `Pancakes.CysTerra.TTS-0.4.7.3.zip` |

每个 zip 的主 dll 位于根目录，可直接导入。发布附件见本仓库 [Releases](https://github.com/Jian-Project-Center/Pancakes.CysTerra/releases)。

## 配置

| 项 | 说明 |
| --- | --- |
| WebSocket | 固定 `wss://api.aloys23.link/api/v1/alert/ws/quake/gq`，设置中不展示 |
| 认证 | 无需填写密钥 |

## 显示名

| 位置 | 文案 |
| --- | --- |
| 预警条 Source | `GlobalQuake` |
| 事件摘要 / 事件详情 / 语音消息 | `PancakesAPI（GlobalQuake）` |
| 事件源等其余文案 | `PancakesAPI` |
| 标题 / 语音 | 对齐官方：`检出` / `归档` / `取消`（`Detected` / `Archived` / `Canceled`） |

## 行为说明

- 字段语义对齐官方 GlobalQuake 报告（震级、深度、坐标、修订、归档、质量 S–F 等）；上游没有的字段不会补写。
- MMI：上游罗马数字经官方 `RomanNumerals.RomanToInteger` 转为数值后再写入 Severity；展示用 `CreateRomanIntensityProperty`。
- 终报（归档）后事件会按官方超时表从进行中状态退出。
- 地名解析使用与官方一致的 NameLocation 策略。

开发说明见 [CysTerra 扩展文档](https://docs.eew.cryville.world/) 与 [打包 / 版本](https://docs.eew.cryville.world/docs/packaging.html#versioning)。

## 许可证

版权所有 © 2026 纪安 / Jian Project Center。

联系邮箱：jianproject@163.com

本程序以 **GNU LGPL-3.0-or-later** 授权。LGPL 正文见 `LICENSE.txt`，其所引用的 GPL-3.0 正文见 `COPYING.txt`。对应源代码即本仓库；发布的扩展包内同时附带这两份许可证。

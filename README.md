# LLMTranslation — 轻量级 AI 划词翻译小工具

本仓库完全使用 AI 工具 vibe coding 生成（不包括本文档，本文档在 AI 编写大部分后，人工进行了审查）。

Models: Qwen 3.8 flash + Qwen 3.8 27B
Agents: Pi + QoderCLI

参考 Cherry Studio 的划词翻译流程，但只保留这一个功能，做到极致轻量（小于15 MB）：

- **可执行文件仅 ~750KB**
- 不捆绑任何运行时：基于 Windows 自带的 .NET Framework 4.8（Win10/11 均已内置）
- 可配置任意 **OpenAI 兼容 API**（地址 / 端口 / Key / 模型 / 提示词）
- 公式渲染：翻译结果在 WebView2 中用 marked + KaTeX 渲染 Markdown 与 LaTeX 公式（$…$ 行内、$$…$$ 独立公式，支持 \boldsymbol 等常用符号）；脚本与字体全部本地打包、离线可用不联网；WebView2 不可用时自动回退为纯文本显示
- **Cherry Studio 风格翻译窗**：圆角窗口；标题栏(置顶/最小化/关闭)可拖动；
  工具栏：源语言→目标语言选择(自动/简体中文/English)、显示/隐藏原文、复制(带“复制成功”提示)
- 窗口交互：**四条边/四角透明热区按住缩放**，悬停时显示对应双向箭头光标（最小 320×240）
- **流式实时渲染**：SSE 逐块推送，文本即时增量显示；MathJax 公式 400ms 防抖后排版，避免阻塞流式观感
- 翻译窗快捷键：**C 复制 / R 重译 / Esc 关闭**
- 默认携带 `"thinking":{"type":"disabled"}` 禁用 DeepSeek 思考（更快更省），设置里可重新开启
- 设置中可调**译文字体大小** (10–28, 默认 15)

## 使用流程

1. 双击 `dist\LLMTranslation.exe`，托盘区出现蓝色"译"字图标，常驻后台。
2. 首次运行自动弹出设置窗，填写：
   - **API 地址**：默认 `https://api.deepseek.com/v1`（模型 `deepseek-v4-flash`），任何 OpenAI 兼容接口均可，如本地代理 `http://127.0.0.1:7890/v1`
   - **端口（可选）**：单独覆盖地址中的端口（如一本地转发端口）
   - **API Key / 模型 / 系统提示词**
3. 在任意软件中**拖选**或**双击**文字 → 光标旁弹出 `文A 翻译` 浮动按钮（鼠标移开约 1 秒后自动消失）。
4. 点击按钮 → 弹出翻译小窗，流式（SSE）实时显示译文；支持 置顶 / 复制 / 拖边缩放 / 最小化到任务栏 / `Esc 关闭`。
5. 全局快捷键（默认 **Alt+Q**，设置里点击输入框按组合键即可改）：不弹浮动按钮，直接打开翻译窗翻译当前选中的文字。
6. 点击或右键托盘：开关划词、设置 / 使用说明、退出；双击托盘直接打开设置。

配置保存在 `%LOCALAPPDATA%\LLMTranslation\config.json`（纯文本 JSON，可手改）。

## 目录结构

```
build.bat            编译脚本 (系统自带 csc, 零依赖; 打包 web/ 与 libs/ 为内嵌资源)
src\                 全部源码 (由单文件 Program.cs 按模块自动拆分, 编译时一起喂给 csc)
  Program.cs         入口: 单实例 Mutex + 启动 AppContext
  AppContext.cs      主控: 托盘/热键装配、全局鼠标钩子、取词调度、资源解包、日志开关
  TrayHotkey.cs      托盘图标 + 全局热键 (隐藏消息窗)
  TranslateForm.cs   翻译窗: WebView2 渲染、流式增量、边缘/角落缩放、DWM 圆角
  SettingsForms.cs   完整设置 / 使用说明窗 (WebDialogForm, WebView2)
  FloatingButtonForm.cs  划词胶囊浮动按钮 (自绘; 含 RoundRect 圆角工具)
  Translator.cs      OpenAI 兼容 API 调用 + SSE 流式解析
  Config.cs          配置读写 (%LOCALAPPDATA%\LLMTranslation\config.json)
  Models.cs          数据模型 (WebMsg / CfgDto 等前后端消息)
  Dpi.cs             DPI 感知与缩放系数 (Dpi.Scale)
  NativeMethods.cs   Win32 P/Invoke + EdgePanel/EdgeBodyPanel/DWM 圆角等辅助
  WinLog.cs          诊断日志 (log.txt, 仅调试模式写)
  TestTarget.cs      自动化测试用目标窗口 (提供可选中文本)
  app.manifest       DPI 感知清单    app.ico  应用/任务栏图标
web\                 前端渲染页 (编译时打包为内嵌 webassets, 首启解包到 %LOCALAPPDATA%)
  index.html         翻译窗页面: 译文 Markdown/公式渲染 + 工具栏 + "快捷设置"弹窗
  settings.html      完整设置 / 使用说明页面
  marked.min.js      Markdown 解析 (离线)
  katex.min.js/css   KaTeX 公式渲染 (离线, 行内 $...$ / 独立 $$...$$)
  fonts\             KaTeX 字体 (woff2)
libs\                WebView2 互操作 DLL (编译引用 + 内嵌 rundeps.zip 随单文件分发)
dist\                成品: LLMTranslation.exe (单文件) + TestTarget.exe (仅测试)
tools\makeicon.*     生成 app.ico 的小工具
test-e2e.ps1         端到端测试: 拖选/双击 → 胶囊 → 翻译窗
test-api.ps1         端到端测试: 本地 mock SSE 服务验证流式翻译 + 公式渲染
test-ui.ps1          端到端测试: 翻译窗拖动 + 边缘/角落缩放 (读取配置里的热键)
mock-server.ps1      测试用 mock OpenAI SSE 服务
AutoTest.cs/.exe     真实鼠标划词自动化校验 (配合 autotest.manifest)
tmpcheck\            开发期临时验证脚本 (未纳入版本库)
```

## 重新编译

```
build.bat
```

仅调用 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，零依赖。

## 取词原理（双通道）

1. **UI Automation**：读取焦点控件的 TextPattern 选区（浏览器/Office/记事本等大多数应用，不碰剪贴板）；
2. **剪贴板回退**：UIA 取不到时模拟 Ctrl+C 读取选区，读完后**立即恢复原剪贴板内容**。
   - 对控制台类窗口（cmd/PowerShell/终端等）**不**使用 Ctrl+C 回退，避免误触发中断信号。

## 胶囊外观规格（钉死，与系统缩放解耦）

胶囊窗固定 98×40 物理像素、不随 DPI 缩放；字体固定像素字号（`GraphicsUnit.Pixel`）：
"A" 17.5px Bold、"翻译" 14.17px、"R" 18.33px；绘制位置 (8,4)/(18,17)/(42,9)，圆角 9。
任何机器（100%/125%/150%）上观感一致。A/B 像素对照参考值（125% 屏）：白色胶囊 96×38、
文字 bbox 70×24、笔画像素 441。改动 `src/FloatingButtonForm.cs` 的胶囊绘制后应做像素级 A/B 验证。

## 已知限制

- 以**管理员权限**运行的目标窗口受 Windows UIPI 安全机制限制，可能无法取词（本程序以普通权限运行）。
- 个别老旧/自绘控件 UIA 与 Ctrl+C 均不支持时无法取词（可用 Alt+Q 或手动复制后配合剪贴板工具）。
- 双击选词依赖鼠标事件间隔 ≤500ms 的自判定（与系统双击速度默认值一致）。

## 为什么不用现成软件

| 软件 | 体积 | 状况 |
|---|---|---|
| Cherry Studio | ~100MB+ | 功能全但臃肿 |
| pot-desktop | 安装包 31.6MB | 超过 15MB 限制，且已停止维护 |
| STranslate | ~100MB | 超过限制 |
| **本工具** | **0.75MB** | 只做划词翻译这一件事 |

## 打包与部署（单文件）

`build.bat` 一次构建出两个产物：

| 产物 | 大小 | 说明 |
|---|---|---|
| `dist\LLMTranslation.exe` | ~741 KB | **单文件版**：复制到任意位置即可运行（桌面/优盘均可）。首次启动自动把内嵌的 web 前端与 WebView2 依赖解包到 `%LOCALAPPDATA%\LLMTranslation\` |
| `dist\TestTarget.exe` | - | 仅测试用，不用分发 |

卸载：直接删除程序目录即可（`%LOCALAPPDATA%\LLMTranslation` 里的设置与 WebView2 缓存保留，重装沿用）。

数据位置（与 exe 位置无关，跟着用户走）：

- 设置：`%LOCALAPPDATA%\LLMTranslation\config.json`
- 调试日志（设环境变量 `LLMTRANSLATION_DEBUG=1` 后生效）：`%LOCALAPPDATA%\LLMTranslation\log.txt`
- WebView2 缓存：`%LOCALAPPDATA%\LLMTranslation\WebView2\`

注意：同一时间只能运行一个实例（单实例锁）。

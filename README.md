# LLMTranslation — 轻量级 AI 划词翻译小工具

参考 Cherry Studio 的划词翻译流程，但只保留这一个功能，做到极致轻量：

- **整个 dist 目录仅 ~2.8 MB**（要求 ≤15MB）
- 不捆绑任何运行时：基于 Windows 自带的 .NET Framework 4.8（Win10/11 均已内置）
- 可配置任意 **OpenAI 兼容 API**（地址 / 端口 / Key / 模型 / 提示词）
- **公式渲染**：翻译结果用 WebView2 + marked + MathJax(full 全宏包, 含 \boldsymbol 等) 渲染 Markdown 与 LaTeX 公式
  （$...$ 行内、$$...$$ 独立公式；离线本地 JS，不联网；WebView2 不可用时自动回退纯文本）
- **Cherry Studio 风格翻译窗**：圆角窗口；标题栏(置顶/最小化/关闭)可拖动；
  工具栏：源语言→目标语言选择(自动/简体中文/English)、⇄ 交换、显示/隐藏原文、复制(带“复制成功”toast)
- 窗口交互：**四条边/四角透明热区按住缩放**，悬停时显示对应双向箭头光标（最小 320×240）；**原文/译文间灰色分隔条上下拖动**
- **流式实时渲染**：SSE 逐块推送，文本即时增量显示；MathJax 公式 400ms 防抖后排版，避免阻塞流式观感
- 翻译窗快捷键：**C 复制 / R 重译 / Esc 关闭**
- 默认携带 `"thinking":{"type":"disabled"}` 禁用 DeepSeek 思考（更快更省），设置里可重新开启
- 设置中可调**译文字体大小** (10–28, 默认 15)

## 使用流程

1. 双击 `dist\LLMTranslation.exe`，托盘区出现蓝色"译"字图标，常驻后台。
2. 首次运行自动弹出设置窗，填写：
   - **API 地址**：如 `https://api.openai.com/v1` 或本地代理 `http://127.0.0.1:7890/v1`
   - **端口（可选）**：单独覆盖地址中的端口（如一本地转发端口）
   - **API Key / 模型 / 系统提示词**
   - 开机自启（写入 HKCU Run 注册表）
3. 在任意软件中**拖选**或**双击**文字 → 光标旁弹出 `文A 翻译` 浮动按钮（鼠标移开约 1 秒后自动消失）。
4. 点击按钮 → 弹出翻译小窗，流式（SSE）实时显示译文；支持 置顶 / 复制 / `Esc 关闭`。
5. 快捷键 **Alt+Q**：不弹浮动按钮，直接翻译当前选中的文字。
6. 托盘右键：开关划词、设置、使用说明、退出。

配置保存在 exe 同目录 `config.json`（纯文本 JSON，可手改）。

## 目录结构

```
build.bat          编译脚本（用系统自带 csc，无需安装任何 SDK）
src\Program.cs     全部源码（单文件）
src\app.manifest   DPI 感知清单
src\TestTarget.cs  自动化测试用的目标窗口
libs\              WebView2 互操作 DLL（编译引用 + 随 dist 分发）
dist\LLMTranslation.exe  成品
dist\web\          渲染页：index.html + marked.min.js + tex-svg.js(MathJax SVG, 无需字体)
dist\*.dll         WebView2 运行时互操作（~750KB）
dist\TestTarget.exe      仅测试用
test-e2e.ps1       端到端测试：拖选/双击 → 浮动按钮 → 翻译窗
test-api.ps1       端到端测试：本地 mock OpenAI SSE 服务验证流式翻译 + 公式渲染
mock-server.ps1    测试用 mock 服务
test-ui.ps1        端到端测试：翻译窗拖动 + 缩放
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
| Cherry Studio | ~100MB+ | 功能全但臃肿（你想避免的） |
| pot-desktop | 安装包 31.6MB | 超过 15MB 限制，且已停止维护 |
| STranslate | ~100MB | 超过限制 |
| **本工具** | **0.035MB** | 只做划词翻译这一件事 |

## 打包与部署（单文件 / 安装包）

`build.bat` 一次构建出三个产物：

| 产物 | 大小 | 说明 |
|---|---|---|
| `dist\LLMTranslation.exe` | ~659 KB | **单文件版**：复制到任意位置即可运行（桌面/优盘均可）。首次启动自动把内嵌的 web 前端与 WebView2 依赖解包到 `%LOCALAPPDATA%\LLMTranslation\` |
| `dist\LLMTranslation-Setup.exe` | ~673 KB | **自解压安装包**（无需管理员）：安装到 `%LOCALAPPDATA%\Programs\LLMTranslation\`，创建桌面 + 开始菜单快捷方式，写入"添加/删除程序"条目，装完自动启动。支持参数 `/S` 静默、`/NOSTART` 装完不启动 |
| `dist\TestTarget.exe` | - | 仅测试用，不用分发 |

卸载：控制面板卸载，或运行安装目录里的 `Uninstall.exe`（会清理程序文件、快捷方式、注册表；`%LOCALAPPDATA%\LLMTranslation` 里的设置与 WebView2 缓存保留，重装沿用）。

数据位置（与 exe 位置无关，跟着用户走）：

- 设置：`%LOCALAPPDATA%\LLMTranslation\config.json`
- 调试日志（设环境变量 `LLMTRANSLATION_DEBUG=1` 后生效）：`%LOCALAPPDATA%\LLMTranslation\log.txt`
- WebView2 缓存：`%LOCALAPPDATA%\LLMTranslation\WebView2\`

注意：单文件版与安装版共用同一个实例锁，同一时间只能跑一个。

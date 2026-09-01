// 自动拆分自 Program.cs - 模块: Config
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;

namespace LLMTranslation
{
    [DataContract]
    public class Config
    {
        [DataMember] public string ApiBase = "https://api.deepseek.com/v1";   // 默认 DeepSeek (OpenAI 兼容接口)
        [DataMember] public string Port = "";
        [DataMember] public string ApiKey = "";
        [DataMember] public string Model = "deepseek-v4-flash";
        [DataMember] public string Prompt = "你是一个翻译器。请自动识别用户文本的语言。只输出翻译结果，不要任何解释。使用 Markdown 格式输出，数学公式使用 LaTeX 语法（行内用 $...$，独立公式用 $$...$$）。";
        [DataMember] public bool EnableSelection = true;
        [DataMember] public bool AutoStart = false;
        [DataMember] public bool UseSystemProxy = true;
        [DataMember] public int FontSize = 15;
        [DataMember] public string SourceLang = "auto";
        [DataMember] public string TargetLang = "auto";
        [DataMember] public bool ShowOriginal = true;
        [DataMember] public bool EnableThinking = false;   // 默认禁用思考, 翻译更快更便宜
        [DataMember] public int UiZoom = 100;      // 界面缩放 %
        [DataMember] public int ButtonDelay = 0;   // 划词后按钮出现延时(ms)
        [DataMember] public string Hotkey = "Alt+Q";
        [DataMember] public bool UseClipboard = true;   // 剪贴板(Ctrl+C)取词回退; 关闭可避免对目标应用/剪贴板的副作用

        private static string ConfigPath
        {
            // 单文件部署: 配置统一放 %LOCALAPPDATA%\LLMTranslation\config.json
            // (旧版 exe 旁的 config.json 由 AppContext.EnsureRuntime 一次性迁移过来)
            get { return Path.Combine(AppContext.DataRoot(), "config.json"); }
        }

        public static bool Exists() { return File.Exists(ConfigPath); }

        public static Config Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    // ReadAllText 自动跳过 BOM, 避免手写配置时带 BOM 导致解析失败
                    string json = File.ReadAllText(ConfigPath);
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(Config));
                    using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        object obj = ser.ReadObject(ms);
                        if (obj != null)
                        {
                            Config c = (Config)obj;
                            // DCS 反序列化不走字段初始化器, 缺失字段为 0/null, 这里统一sanitize
                            if (c.ApiBase == null) c.ApiBase = "https://api.deepseek.com/v1";
                            if (c.Port == null) c.Port = "";
                            if (c.ApiKey == null) c.ApiKey = "";
                            if (c.Model == null) c.Model = "deepseek-v4-flash";
                            if (c.Prompt == null) c.Prompt = new Config().Prompt;
                            if (c.FontSize < 8 || c.FontSize > 28) c.FontSize = 15;
                            if (string.IsNullOrEmpty(c.SourceLang)) c.SourceLang = "auto";
                            if (string.IsNullOrEmpty(c.TargetLang)) c.TargetLang = "auto";
                            if (c.UiZoom < 80 || c.UiZoom > 200) c.UiZoom = 100;
                            if (c.ButtonDelay < 0 || c.ButtonDelay > 2000) c.ButtonDelay = 0;
                            if (string.IsNullOrEmpty(c.Hotkey)) c.Hotkey = "Alt+Q";
                            if (!json.Contains("UseClipboard")) c.UseClipboard = true;   // 旧配置缺省为开
                            return c;
                        }
                    }
                }
            }
            catch { }
            return new Config();
        }

        public void Save()
        {
            try
            {
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(Config));
                using (FileStream fs = File.Create(ConfigPath))
                {
                    ser.WriteObject(fs, this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存配置失败: " + ex.Message, "LLMTranslation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

}

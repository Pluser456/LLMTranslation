// 自动拆分自 Program.cs - 模块: Translator
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LLMTranslation
{
    public static class Translator
    {
        private static HttpClient CreateClient(Config cfg)
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.UseProxy = cfg.UseSystemProxy;
            if (!cfg.UseSystemProxy) handler.Proxy = null;
            HttpClient client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(90);
            return client;
        }

        public static string BuildUrl(Config cfg)
        {
            string b = (cfg.ApiBase ?? "").Trim();
            if (b.Length == 0) b = "https://api.deepseek.com/v1";
            while (b.EndsWith("/")) b = b.Substring(0, b.Length - 1);
            if (!b.Contains("://")) b = "https://" + b;
            string url = b + "/chat/completions";
            string port = (cfg.Port ?? "").Trim();
            if (port.Length > 0)
            {
                int pn;
                if (int.TryParse(port, out pn))
                {
                    try
                    {
                        UriBuilder ub = new UriBuilder(new Uri(url));
                        ub.Port = pn;
                        url = ub.ToString();
                    }
                    catch { }
                }
            }
            return url;
        }

        public static string JsonEscape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length + 16);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string ExtractContent(DataContractJsonSerializer ser, string json)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    ChatResponse resp = (ChatResponse)ser.ReadObject(ms);
                    if (resp != null && resp.choices != null && resp.choices.Count > 0)
                    {
                        ChatChoice ch = resp.choices[0];
                        ChatDelta d = ch.delta != null ? ch.delta : ch.message;
                        if (d != null) return d.content;
                    }
                }
            }
            catch { }
            return null;
        }

        public static async Task TranslateAsync(Config cfg, string text,
            Action<string> onChunk, Action<string> onError, Action onDone, CancellationToken ct)
        {
            try
            {
                AppContext.Log("request url=" + BuildUrl(cfg));
                string sys = cfg.Prompt;
                if (!string.IsNullOrEmpty(cfg.SourceLang) && cfg.SourceLang != "auto")
                    sys += "\n源语言为：" + TranslateForm.LangName(cfg.SourceLang) + "。";
                if (!string.IsNullOrEmpty(cfg.TargetLang) && cfg.TargetLang != "auto")
                    sys += "\n请将结果翻译为：" + TranslateForm.LangName(cfg.TargetLang) + "。";
                string body = "{\"model\":\"" + JsonEscape(cfg.Model) + "\",\"stream\":true,\"messages\":[{" +
                    "\"role\":\"system\",\"content\":\"" + JsonEscape(sys) + "\"},{" +
                    "\"role\":\"user\",\"content\":\"" + JsonEscape(text) + "\"}]";
                if (!cfg.EnableThinking)
                    body += ",\"thinking\":{\"type\":\"disabled\"}";   // DeepSeek v4 默认开思考, 翻译场景禁用
                body += "}";
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, BuildUrl(cfg));
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                string key = (cfg.ApiKey ?? "").Trim();
                if (key.Length > 0) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);

                HttpClient client = CreateClient(cfg);
                HttpResponseMessage resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                AppContext.Log("response status=" + (int)resp.StatusCode);
                if (!resp.IsSuccessStatusCode)
                {
                    string err = "";
                    try { err = await resp.Content.ReadAsStringAsync().ConfigureAwait(false); }
                    catch { }
                    if (err.Length > 300) err = err.Substring(0, 300);
                    onError("HTTP " + (int)resp.StatusCode + " " + resp.ReasonPhrase + "\n" + err);
                    onDone();
                    return;
                }

                Stream stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using (stream)
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(ChatResponse));
                    string line;
                    while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        line = line.Trim();
                        if (line.Length == 0) continue;
                        if (line.StartsWith("data:")) line = line.Substring(5).Trim();
                        if (line.Length == 0 || line == "[DONE]") continue;
                        if (line[0] != '{') continue;
                        string piece = ExtractContent(ser, line);
                        if (piece != null) onChunk(piece);
                    }
                }
                onDone();
            }
            catch (OperationCanceledException)
            {
                onDone();
            }
            catch (Exception ex)
            {
                AppContext.Log("translate exception: " + ex.ToString());
                string msg = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null) { msg += " -> " + inner.Message; inner = inner.InnerException; }
                onError(msg);
                onDone();
            }
        }
    }
}

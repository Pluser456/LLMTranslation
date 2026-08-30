// 自动拆分自 Program.cs - 模块: Models
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace LLMTranslation
{
    [DataContract]
    public class ChatDelta { [DataMember] public string content; }
    [DataContract]
    public class ChatChoice { [DataMember] public ChatDelta delta; [DataMember] public ChatDelta message; }
    [DataContract]
    public class ChatResponse { [DataMember] public List<ChatChoice> choices; }

    [DataContract]
    public class WebMsg { [DataMember] public string t; [DataMember] public string tgt; [DataMember] public CfgDto cfg; [DataMember] public double dpr; }   // dpr: JS devicePixelRatio (诊断用)
    [DataContract]
    public class CfgDto
    {
        [DataMember] public string ApiBase; [DataMember] public string Port; [DataMember] public string Model;
        [DataMember] public string ApiKey; [DataMember] public string Prompt; [DataMember] public int FontSize;
        [DataMember] public bool EnableThinking; [DataMember] public bool EnableSelection; [DataMember] public bool AutoStart;
        [DataMember] public int UiZoom; [DataMember] public int ButtonDelay; [DataMember] public string Hotkey;
    }

}

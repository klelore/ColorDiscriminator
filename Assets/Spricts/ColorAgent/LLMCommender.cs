using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections;
using System;

public class LLMCommander : MonoBehaviour
{
    [Header("API 设置")]
    // 如果是 DeepSeek: https://api.deepseek.com/v1/chat/completions
    // 如果是 本地Ollama: http://localhost:11434/v1/chat/completions
    public string apiUrl = "https://api.deepseek.com/v1/chat/completions";
    public string apiKey = "sk-19f98991b4f6460ca9cd54f16c850672"; // 填你的 Key
    public string modelName = "deepseek-chat";    // 模型名字

    [Header("游戏引用")]
    public ColorDiscriminator agent; // 拖入你那个训练好的 Agent

    [Header("测试输入")]
    public string userInstruction = "去找个红色的方块";

    // 用于测试的按钮
    [ContextMenu("发送指令")]
    public void TestSend()
    {
        StartCoroutine(PostRequest(userInstruction));
    }

    IEnumerator PostRequest(string prompt)
    {
        Debug.Log($"正在思考: {prompt} ...");

        // 1. 构建 System Prompt (这是灵魂，教 LLM 做人)
        // 核心逻辑：你是一个翻译官，把人话翻译成 0, 1, 2
        string systemPrompt = @"
            你是一个游戏指令解析器。
            场景中有三种颜色的方块：
            0: 红色 (Red, 火, 苹果, 暖色)
            1: 绿色 (Green, 草, 树叶, 自然)
            2: 蓝色 (Blue, 天空, 海洋, 冷色)
            
            请分析用户的意图，返回且仅返回一个 JSON 格式的数据：
            { ""target_index"": int }
            如果用户说的话和颜色无关，默认返回 0。
            不要输出任何额外的废话，只输出 JSON。
        ";

        // 2. 手搓 JSON (为了不依赖插件，稍微丑一点但稳定)
        // 这是一个标准的 OpenAI 格式 Request
        string jsonPayload = $@"
        {{
            ""model"": ""{modelName}"",
            ""messages"": [
                {{ ""role"": ""system"", ""content"": ""{CleanString(systemPrompt)}"" }},
                {{ ""role"": ""user"", ""content"": ""{CleanString(prompt)}"" }}
            ],
            ""temperature"": 0.1
        }}";

        // 3. 发送网络请求
        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // 设置 Header
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("LLM 报错: " + request.error);
                Debug.LogError("返回内容: " + request.downloadHandler.text);
            }
            else
            {
                // 4. 解析结果
                string responseText = request.downloadHandler.text;
                ParseResponse(responseText);
            }
        }
    }

    void ParseResponse(string json)
    {
        try
        {
            // 解析 OpenAI 格式的 JSON
            OpenAIResponse response = JsonUtility.FromJson<OpenAIResponse>(json);
            string content = response.choices[0].message.content;

            Debug.Log($"LLM 原始回复: {content}");

            // 解析我们自定义的 { "target_index": x }
            // 这里因为 content 可能包含换行符或 ```json 标记，简单清洗一下
            content = content.Replace("```json", "").Replace("```", "").Trim();

            GameCommand cmd = JsonUtility.FromJson<GameCommand>(content);

            // 🔥 核心：指挥 Agent
            Debug.Log($">> 指令识别成功！目标索引: {cmd.target_index}");
            ExecuteCommand(cmd.target_index);
        }
        catch (Exception e)
        {
            Debug.LogError("解析失败，LLM 可能没按格式说话: " + e.Message);
        }
    }

    void ExecuteCommand(int index)
    {
        // 这里我们要去修改 ColorDiscriminator 里的变量
        // 记得把 ColorDiscriminator 里的 nextUserTarget 改成 public 或者写个方法

        // 假设你在 Agent 里加了一个 SetTarget(int id) 方法
        // 或者直接改 public 变量：
        // agent.nextUserTarget = index;
        // agent.isTrainingMode = false; // 确保切换到手动/指令模式
        // agent.EndEpisode(); // 强制重开一局，立即执行新任务

        Debug.Log("正在通知 Agent 切换目标..." + index);
        agent.SetUserTarget(index);

    }

    // 简单的字符串清洗，防止 JSON 里的引号冲突
    string CleanString(string s)
    {
        return s.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
    }

    // --- 定义数据结构用于 JsonUtility ---
    [Serializable]
    public class OpenAIResponse
    {
        public Choice[] choices;
    }
    [Serializable]
    public class Choice
    {
        public Message message;
    }
    [Serializable]
    public class Message
    {
        public string content;
    }
    [Serializable]
    public class GameCommand
    {
        public int target_index;
    }
}
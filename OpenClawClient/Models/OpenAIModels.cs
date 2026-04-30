using System.Text.Json.Serialization;

namespace OpenClawClient.Models;

// ---- Request models ----

public class ChatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "openclaw";
    [JsonPropertyName("messages")] public List<ChatRequestMessage> Messages { get; set; } = new();
    [JsonPropertyName("stream")] public bool Stream { get; set; }
    [JsonPropertyName("user")] public string? User { get; set; }
}

public class ChatRequestMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "user";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
}

// ---- Response models ----

public class ChatResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("object")] public string? Object { get; set; }
    [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
    [JsonPropertyName("usage")] public Usage? Usage { get; set; }
}

public class Choice
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("message")] public ResponseMessage? Message { get; set; }
    [JsonPropertyName("delta")] public ResponseMessage? Delta { get; set; }
    [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; }
}

public class ResponseMessage
{
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("content")] public string? Content { get; set; }
}

public class Usage
{
    [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
    [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
    [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
}

// ---- Models endpoint ----

public class ModelsResponse
{
    [JsonPropertyName("object")] public string? Object { get; set; }
    [JsonPropertyName("data")] public List<ModelInfo>? Data { get; set; }
}

public class ModelInfo
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("object")] public string? Object { get; set; }
    [JsonPropertyName("created")] public long Created { get; set; }
    [JsonPropertyName("owned_by")] public string? OwnedBy { get; set; }
}

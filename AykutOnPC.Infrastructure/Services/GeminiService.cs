using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace AykutOnPC.Infrastructure.Services;

public class GeminiService : IAIService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private string? _cachedApiUrl;

    public GeminiService(AppDbContext context, HttpClient httpClient, IConfiguration configuration)
    {
        _context = context;
        _httpClient = httpClient;
        _apiKey = configuration["GeminiSettings:ApiKey"]!;
    }

    private async Task<string> GetApiUrlAsync()
    {
        if (!string.IsNullOrEmpty(_cachedApiUrl)) return _cachedApiUrl;

        // Fetch Key from Config
        var currentKey = _apiKey;

        if (string.IsNullOrEmpty(currentKey) || currentKey.StartsWith("BURAYA")) 
            throw new Exception("Gemini API Key is missing in Database (SiteSettings) and appsettings.json");

        try 
        {
            // 1. List available models
            var listUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={currentKey}";
            var response = await _httpClient.GetAsync(listUrl);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            
            // 2. Find a suitable model (Gemini & supports generateContent)
            string? bestModel = null;
            
            foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
            {
                var name = model.GetProperty("name").GetString()!; // e.g., "models/gemini-1.5-flash"
                var methods = model.GetProperty("supportedGenerationMethods").EnumerateArray();
                
                bool supportsGenerateContent = methods.Any(m => m.GetString() == "generateContent");
                bool isGemini = name.Contains("gemini", StringComparison.OrdinalIgnoreCase);

                if (isGemini && supportsGenerateContent)
                {
                    // Prefer flash or pro if possible
                    if (name.Contains("flash") || name.Contains("pro"))
                    {
                        bestModel = name;
                        break;
                    }
                    bestModel ??= name; // Keep valid fallback
                }
            }

            if (bestModel == null) throw new Exception("No suitable Gemini model found.");

            _cachedApiUrl = $"https://generativelanguage.googleapis.com/v1beta/{bestModel}:generateContent";
            return _cachedApiUrl;
        }
        catch
        {
            // Fallback if list fails
            return "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent";
        }
    }

    public async Task<string> GetAnswerAsync(string userMessage)
    {
        // 0. Resolve API URL
        var apiUrl = await GetApiUrlAsync();

        // 1. Context Building (RAG)
        var knowledgeEntries = await _context.KnowledgeEntries.ToListAsync();
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("You are an AI assistant for a portfolio website called 'AykutOnPC'.");
        contextBuilder.AppendLine("Answer questions based strictly on the following context. If the answer is not in the context, be polite and say you don't know, but you can take a note.");
        contextBuilder.AppendLine("Keep answers concise, professional, yet friendly.");
        contextBuilder.AppendLine("\n--- CONTEXT START ---");
        
        foreach (var entry in knowledgeEntries)
        {
            contextBuilder.AppendLine($"Topic: {entry.Topic}");
            contextBuilder.AppendLine($"Info: {entry.Content}");
            contextBuilder.AppendLine("-");
        }
        contextBuilder.AppendLine("--- CONTEXT END ---\n");

        // 2. Prepare Request
        var fullPrompt = contextBuilder.ToString() + $"\nUser: {userMessage}\nAssistant:";
        
        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = fullPrompt } } }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try 
        {
            var currentKey = _apiKey;

            var response = await _httpClient.PostAsync($"{apiUrl}?key={currentKey}", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return $"Google API Error ({response.StatusCode}): {errorContent}\nAttempted Endpoint: {apiUrl}";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);
            
            // Navigate JSON: candidates[0].content.parts[0].text
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? "I assume I understood, but I have no words.";
        }
        catch (Exception ex)
        {
            return $"AI Service Exception: {ex.Message}";
        }
    }
}

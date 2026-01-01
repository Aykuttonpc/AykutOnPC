using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace AykutOnPC.Infrastructure.Services;

public class LocalRAGService : IAIService
{
    private readonly AppDbContext _context;

    public LocalRAGService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GetAnswerAsync(string userMessage)
    {
        var message = userMessage.ToLower();
        
        // 1. Fetch all knowledge (for small scale this is fine, for large scale use Vector DB)
        var knowledgeBase = await _context.KnowledgeEntries.ToListAsync();

        // 2. Simple Keyword Matching (RAG Light)
        var bestMatch = knowledgeBase
            .Select(k => new
            {
                Entry = k,
                Score = CalculateScore(message, k.Keywords.ToLower() + " " + k.Topic.ToLower())
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestMatch != null && bestMatch.Score > 1) // Threshold
        {
            return bestMatch.Entry.Content;
        }

        // 3. Fallback / Identity responses
        if (message.Contains("merhaba") || message.Contains("selam"))
            return "Merhaba! Ben Aykut'un dijital asistanıyım. Sana nasıl yardımcı olabilirim?";
            
        if (message.Contains("kimsin") || message.Contains("nedir"))
            return "Ben AykutOnPC platformunun yapay zeka asistanıyım. Aykut hakkında sorularını yanıtlamak için buradayım.";

        return "Bunu henüz öğrenmedim. Ama istersen Aykut'a (Admin) bu konuda bilgi eklemesi için not bırakabilirim. 😊";
    }

    private int CalculateScore(string query, string keywords)
    {
        int score = 0;
        var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in queryWords)
        {
            if (keywords.Contains(word)) score += 2; // Keyword match
        }
        
        return score;
    }
}

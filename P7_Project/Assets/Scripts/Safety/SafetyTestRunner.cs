using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

/// <summary>
/// Safety Testing Framework (HTTP Ollama Version)
/// Loads Do-Not-Answer dataset CSV, feeds questions to NPC via HTTP Ollama, logs raw text responses
/// Does NOT trigger animations, TTS, or UI - purely text-based evaluation
/// </summary>
public class SafetyTestRunner : MonoBehaviour
{
    [System.Serializable]
    public class SafetyQuestion
    {
        public int id;
        public string riskArea;
        public string typesOfHarm;
        public string specificHarms;
        public string question;
    }

    [System.Serializable]
    public class SafetyResponse
    {
        public int testNumber;
        public string riskArea;
        public string typesOfHarm;
        public string question;
        public string textResponse; // RAW TEXT ONLY (no metadata, no markup)
        public bool hadMetadata;
        public float responseTime;
    }

    [Header("Configuration")]
    public TextAsset csvFile; // Assign the CSV file here

    [Header("NPCs to Test")]
    public NPCChatInstance testNpc1;
    public NPCChatInstance testNpc2;
    public bool testBothNpcs = false;

    [Header("Output")]
    public bool verbose = true;

    private List<SafetyQuestion> questions = new();
    private List<SafetyResponse> responses = new();

    /// <summary>
    /// Right-click SafetyTestRunner in Inspector → Run Safety Tests
    /// IMPORTANT: Use this in a dedicated test scene with NO game logic running
    /// </summary>
    [ContextMenu("Run Safety Tests")]
    public async void RunSafetyTests()
    {
        if (!ValidateSetup())
            return;

        Debug.Log("[SafetyTestRunner] ====== STARTING SAFETY EVALUATION (HTTP Ollama) ======");
        Debug.Log("[SafetyTestRunner] CSV-Only Mode: No animations, TTS, UI, or game logic triggered");
        
        var ollamaClient = FindObjectOfType<OllamaChatClient>();
        if (ollamaClient != null)
        {
            Debug.Log($"[SafetyTestRunner] Using Ollama: {ollamaClient.endpoint}");
        }

        // Load CSV
        LoadCSV(csvFile.text);
        Debug.Log($"[SafetyTestRunner] Loaded {questions.Count} questions");

        // Run tests
        int testNum = 0;
        foreach (var q in questions)
        {
            testNum++;

            // Test NPC1 (direct inference only)
            await TestNpc(testNpc1, q, testNum, "NPC1");

            // Test NPC2 if enabled (direct inference only)
            if (testBothNpcs && testNpc2 != null)
            {
                await TestNpc(testNpc2, q, testNum, "NPC2");
            }

            // Small delay between questions (avoid flooding)
            await Task.Delay(500);
        }

        // Export results
        ExportResults();
        Debug.Log("[SafetyTestRunner] ====== EVALUATION COMPLETE ======");
        Debug.Log("[SafetyTestRunner] ✓ No NPCs were modified, no animations triggered, no TTS run");
    }

    private bool ValidateSetup()
    {
        if (csvFile == null)
        {
            Debug.LogError("[SafetyTestRunner] CSV file not assigned!");
            return false;
        }

        if (testNpc1 == null)
        {
            Debug.LogError("[SafetyTestRunner] NPC1 not assigned!");
            return false;
        }

        // Check for OllamaChatClient
        if (FindObjectOfType<OllamaChatClient>() == null)
        {
            Debug.LogError("[SafetyTestRunner] OllamaChatClient not found in scene! Create a GameObject with OllamaChatClient component.");
            return false;
        }

        // Verify LLMConfig exists
        if (LLMConfig.Instance == null)
        {
            Debug.LogError("[SafetyTestRunner] LLMConfig not found (ScriptableObject in Resources)!");
            return false;
        }

        Debug.Log("[SafetyTestRunner] ✓ Setup validated: CSV, NPC, OllamaChatClient, LLMConfig present");
        return true;
    }

    private void LoadCSV(string csvText)
    {
        var lines = csvText.Split('\n');

        // Skip header (line 0: id,risk_area,types_of_harm,specific_harms,question)
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var fields = ParseCSVLine(lines[i]);
            if (fields.Count >= 5)
            {
                if (int.TryParse(fields[0], out int id))
                {
                    questions.Add(new SafetyQuestion
                    {
                        id = id,
                        riskArea = fields[1],
                        typesOfHarm = fields[2],
                        specificHarms = fields[3],
                        question = fields[4]
                    });
                }
            }
        }
    }

    private List<string> ParseCSVLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"')
                inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString().Trim('"').Trim());
                current.Clear();
            }
            else
                current.Append(c);
        }
        fields.Add(current.ToString().Trim('"').Trim());

        return fields;
    }

    private async Task TestNpc(NPCChatInstance npc, SafetyQuestion q, int testNum, string npcLabel)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (verbose)
                Debug.Log($"[{npcLabel}] Test {testNum}/{questions.Count}: {q.question.Substring(0, Mathf.Min(60, q.question.Length))}...");

            // Get raw text response (DIRECT inference only - NO NPC methods called)
            // This bypasses: animations, TTS, UI, turn-taking, DialogueManager, etc.
            string response = await GetRawTextResponse(npc, q.question);

            stopwatch.Stop();

            // Check if response is an error
            if (response.StartsWith("[ERROR"))
            {
                Debug.LogError($"[{npcLabel}] Test {testNum} received error response: {response}");
            }

            // Check if response contained metadata
            bool hadMetadata = response.Contains("[META]") && response.Contains("[/META]");

            // Extract clean text (remove metadata tags if present)
            string cleanText = ExtractCleanText(response);

            // Record response
            var record = new SafetyResponse
            {
                testNumber = testNum,
                riskArea = q.riskArea,
                typesOfHarm = q.typesOfHarm,
                question = q.question,
                textResponse = cleanText,
                hadMetadata = hadMetadata,
                responseTime = stopwatch.ElapsedMilliseconds
            };

            responses.Add(record);

            if (verbose)
            {
                string preview = cleanText.Length > 80 ? cleanText.Substring(0, 80) + "..." : cleanText;
                Debug.Log($"  → {preview}");
                Debug.Log($"  → Metadata: {hadMetadata} | Time: {stopwatch.ElapsedMilliseconds}ms");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{npcLabel}] Test {testNum} EXCEPTION: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Get raw text response from NPC using HTTP Ollama
    /// CRITICAL: Only uses NPCProfile for configuration (temperature, penalties)
    /// Does NOT call any NPCChatInstance methods
    /// Does NOT trigger: animations, TTS, DialogueManager, UI, turn-taking, or any game logic
    /// </summary>
    private async Task<string> GetRawTextResponse(NPCChatInstance npc, string question)
    {
        try
        {
            var ollamaClient = FindObjectOfType<OllamaChatClient>();
            if (ollamaClient == null)
            {
                Debug.LogError("[SafetyTestRunner] OllamaChatClient not found in scene!");
                return "[ERROR: OllamaChatClient not found]";
            }

            // Read ONLY: Get system prompt and inference parameters from NPCProfile
            string systemPrompt = LLMConfig.Instance.GetSystemPromptForNPC(npc.npcProfile);

            // Direct HTTP Ollama inference (no side-effects)
            var messages = new List<OllamaChatClient.ChatMessage>
            {
                new OllamaChatClient.ChatMessage { role = "system", content = systemPrompt },
                new OllamaChatClient.ChatMessage { role = "user", content = question }
            };

            var response = await ollamaClient.SendChatAsync(
                messages: messages,
                temperature: npc.npcProfile.temperature,
                repeatPenalty: npc.npcProfile.repeatPenalty
            );

            // Check for errors
            if (response.error != null)
            {
                Debug.LogError($"[SafetyTestRunner] Ollama error: {response.error}");
                return $"[ERROR: {response.error}]";
            }

            // Safety check: if response is empty or null, log it
            if (string.IsNullOrEmpty(response.content))
            {
                Debug.LogWarning("[SafetyTestRunner] Empty response from Ollama HTTP - connection issue?");
                return "[ERROR: Empty response from Ollama]";
            }

            return response.content;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SafetyTestRunner] Exception during HTTP inference: {ex.Message}");
            return $"[ERROR: {ex.Message}]";
        }
    }

    /// <summary>
    /// Extract clean text from response
    /// Removes [META] tags if present, keeps only spoken text
    /// </summary>
    private string ExtractCleanText(string fullResponse)
    {
        // If no metadata, return as-is
        if (!fullResponse.Contains("[META]"))
            return fullResponse;

        // Extract text AFTER [/META] tag
        int metaEndIndex = fullResponse.IndexOf("[/META]");
        if (metaEndIndex >= 0)
        {
            string textAfterMeta = fullResponse.Substring(metaEndIndex + "[/META]".Length).Trim();
            return textAfterMeta;
        }

        return fullResponse;
    }

    private void ExportResults()
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        // Export CSV
        ExportCSV(timestamp);

        // Export JSON
        ExportJSON(timestamp);

        Debug.Log($"[SafetyTestRunner] Exported {responses.Count} responses");
        
        // Show the folder path in console so user can easily find it
        string folderPath = Application.persistentDataPath;
        Debug.Log($"[SafetyTestRunner] ✓ Results folder: {folderPath}");
        Debug.Log($"[SafetyTestRunner] ✓ Look for files: safety_responses_{timestamp}.csv and .json");
    }

    private void ExportCSV(string timestamp)
    {
        string csvPath = $"{Application.persistentDataPath}/safety_responses_{timestamp}.csv";

        var csv = new StringBuilder();
        csv.AppendLine("TestNumber,RiskArea,TypesOfHarm,Question,TextResponse,HadMetadata,ResponseTimeMs");

        foreach (var r in responses)
        {
            string escapedQuestion = EscapeCSV(r.question);
            string escapedResponse = EscapeCSV(r.textResponse);

            csv.AppendLine($"{r.testNumber}," +
                $"\"{r.riskArea}\"," +
                $"\"{r.typesOfHarm}\"," +
                $"\"{escapedQuestion}\"," +
                $"\"{escapedResponse}\"," +
                $"{r.hadMetadata}," +
                $"{r.responseTime}");
        }

        File.WriteAllText(csvPath, csv.ToString());
        Debug.Log($"[SafetyTestRunner] CSV saved: {csvPath}");
    }

    private void ExportJSON(string timestamp)
    {
        string jsonPath = $"{Application.persistentDataPath}/safety_responses_{timestamp}.json";

        var container = new ResponseContainer
        {
            timestamp = System.DateTime.Now.ToString("O"),
            totalTests = responses.Count,
            responses = responses
        };

        string json = JsonUtility.ToJson(container, true);
        File.WriteAllText(jsonPath, json);
        Debug.Log($"[SafetyTestRunner] JSON saved: {jsonPath}");
    }

    private string EscapeCSV(string field)
    {
        return field
            .Replace("\"", "\"\"")
            .Replace("\n", " ")
            .Replace("\r", " ");
    }

    [System.Serializable]
    private class ResponseContainer
    {
        public string timestamp;
        public int totalTests;
        public List<SafetyResponse> responses;
    }

    [ContextMenu("Show Results Folder")]
    public void ShowResultsFolder()
    {
        string folderPath = Application.persistentDataPath;
        Debug.Log($"[SafetyTestRunner] Opening results folder: {folderPath}");
        
        // Try to open the folder
        try
        {
            #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                System.Diagnostics.Process.Start("explorer.exe", folderPath);
            #elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                System.Diagnostics.Process.Start("open", folderPath);
            #elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
                System.Diagnostics.Process.Start("xdg-open", folderPath);
            #endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SafetyTestRunner] Could not open folder: {ex.Message}");
            Debug.Log($"[SafetyTestRunner] Navigate manually to: {folderPath}");
        }
    }
}

# Additional Reference Materials

## 1. Quick Reference - Component Functions

```
┌─────────────────────────────────────────────────────────────┐
│ QUICK FUNCTION REFERENCE                                    │
└─────────────────────────────────────────────────────────────┘

DialogueManager (Singleton)
├─ StartInterview()           → Initialize interview, set phase
├─ RequestTurn(npcName)       → Request speaking turn (bool)
├─ ReleaseTurn()              → Release current speaker
├─ WasLastSpeaker(name)       → Check speaker history (bool)
├─ GetTurnHistory()           → Get recent speaker sequence (string)
├─ GetCurrentPhase()          → Get interview phase
├─ ClearHistory()             → Reset state for new interview
└─ Phases: Introduction → Main → Conclusion

NPCChatInstance
├─ Send(userText)             → Process user input async
├─ ExecuteSpeech(text)        → Run LLM inference
├─ ProcessToken(token)        → Handle streaming tokens
├─ UpdateMemory(facts)        → Store extracted facts
├─ GetSystemPrompt()          → Build context prompt (string)
└─ OnDestroy()               → Cleanup resources

NPCMemory
├─ AddFact(fact)              → Store new fact
├─ GetContext()               → Get memory for prompt (string)
├─ Update(text)               → Process new information
├─ ClearHistory()             → Reset conversation history
├─ SummarizeContext()         → Compress memory for space
└─ GetLastNTurns(n)          → Recent conversation (string)

OllamaChatClient
├─ SendChatAsync(messages)    → LLM API call (async)
├─ BuildRequestJson()         → Create API request body
├─ ExtractContent(line)       → Parse streaming response
├─ HandleError(exception)     → Error management
└─ Streaming: onTokenReceived callback

WhisperContinuous
├─ Start()                    → Initialize STT
├─ CaptureMicrophone()        → Recording loop (coroutine)
├─ SaveWav(path, clip)        → Convert audio to file
└─ OnApplicationQuit()        → Cleanup model

NPCTTSHandler
├─ Initialize(audioSource)    → Setup TTS
├─ EnqueueSpeech(text)        → Add to synthesis queue
├─ ProcessQueue()             → Synthesize queued chunks
├─ IsSpeaking()              → Check playback status (bool)
└─ StopSpeech()              → Interrupt audio
```

## 2. System Flowchart

```
                         ┌─────────┐
                         │  START  │
                         └────┬────┘
                              │
                    ┌─────────▼────────────┐
                    │  Load Scene          │
                    │  Initialize Managers │
                    └─────────┬────────────┘
                              │
                         ┌────▼────────┐
                         │ TUTORIAL?   │
                         └─┬──────────┬┘
                     YES  │          │  NO
                    ┌─────▼─┐   ┌───▼─────┐
                    │Show   │   │Interview│
                    │Welcome│   │Start    │
                    └─────┬─┘   └───┬─────┘
                         │         │
                         └────┬────┘
                              │
                    ┌─────────▼────────┐
                    │ Interview Loop   │
                    └────────┬─────────┘
                             │
                    ┌────────▼────────┐
                    │ Phase Check?    │
                    └──┬─────┬─────┬──┘
                       │     │     │
                    ┌──▼──┬──▼──┬──▼──┐
                    │Intro│Main │End  │
                    └──┬──┴──┬──┴──┬──┘
                       │     │     │
                    (NPCs generate responses, turn-based)
                       │     │     │
                    ┌──▼──▼──▼──▼──┐
                    │ totalTurns   │
                    │>= Threshold? │
                    └──┬─────────┬─┘
                  NO   │         │  YES
                    ┌──▼──┐    ┌─▼──────┐
                    │Main │    │Conclude│
                    │Loop │    │Phase   │
                    └──┬──┘    └──┬─────┘
                       │         │
                       └────┬────┘
                            │
                    ┌───────▼────────┐
                    │ Interview End? │
                    └─┬──────────┬───┘
                  NO  │          │  YES
                      │    ┌─────▼────────┐
                      │    │ Store Results│
                      │    │ Exit         │
                      │    └──────────────┘
                      │
                      └─ Continue Loop
```

## 3. Key Design Decisions

```
DESIGN DECISION MATRIX

1. LLM Selection: Llama2 7B (not ChatGPT)
   ├─ Rationale: Local deployment, no API costs
   ├─ Trade-off: Lower quality vs more control
   └─ Mitigation: Prompt engineering, memory context

2. STT: Whisper DLL (not cloud API)
   ├─ Rationale: Privacy, offline capability
   ├─ Trade-off: Accuracy vs latency
   └─ Mitigation: Use Whisper large model when possible

3. Turn Model: Simple blocking lock (not queue)
   ├─ Rationale: Simplicity, easy to reason about
   ├─ Trade-off: No queuing for impatient NPCs
   └─ Mitigation: Fast response times reduce perceived blocking

4. Memory: Per-NPC in-memory (not database)
   ├─ Rationale: Fast, no I/O latency
   ├─ Trade-off: Lost on exit
   └─ Mitigation: Export option added for persistence

5. Streaming: Per-token callback (not buffered)
   ├─ Rationale: Real-time feedback to user
   ├─ Trade-off: More callback overhead
   └─ Mitigation: Batching tokens if needed

6. TTS: Async sentence chunks (not word-by-word)
   ├─ Rationale: Natural speech rhythm
   ├─ Trade-off: Slight latency increase
   └─ Mitigation: Pre-buffer while speaking
```

## 4. Common Issues & Solutions

```
TROUBLESHOOTING GUIDE

Issue: "OllamaChatClient not found"
├─ Cause: OllamaChatClient not in scene
├─ Solution: Add OllamaChatClient GameObject to scene
└─ Verify: Check LLMConfig singleton

Issue: Whisper model not loading
├─ Cause: File missing or path incorrect
├─ Solution: Place ggml-tiny.bin in StreamingAssets/Whisper/
└─ Verify: Check Application.streamingAssetsPath in console

Issue: No microphone input
├─ Cause: Microphone not found or not authorized
├─ Solution: Check Windows audio settings, grant app permission
└─ Verify: Microphone.devices.Length > 0 in console

Issue: NPC responses taking >10 seconds
├─ Cause: Ollama not running or GPU out of memory
├─ Solution: Restart Ollama, restart scene, check VRAM
└─ Verify: ollama ps in terminal

Issue: Turn stuck (currentSpeaker never released)
├─ Cause: Exception in response processing
├─ Solution: Check console for errors, force release in Update()
└─ Verify: Add timeout mechanism to DialogueManager

Issue: Memory growing unbounded
├─ Cause: StringBuilders not cleared, lists not pruned
├─ Solution: Implement fixed-size circular buffer for history
└─ Verify: Monitor Application.systemMemoryUsage in profiler

Issue: TTS audio choppy or overlapping
├─ Cause: Sentence boundary detection failing
├─ Solution: Manually trim response text
└─ Verify: Log ttsBuffer contents
```

## 5. Performance Optimization Tips

```
OPTIMIZATION CHECKLIST

┌─ Code Optimization
│  ├─ Use StringBuilder instead of string concatenation
│  ├─ Cache GetComponent<>() results
│  ├─ Pool frequently created objects (buffers)
│  ├─ Avoid LINQ in hot paths
│  └─ Profile with Unity Profiler (Window > Analysis)
│
├─ Network Optimization
│  ├─ Keep Ollama on local network (<5ms latency)
│  ├─ Use model quantization (7B instead of 13B)
│  ├─ Reuse HttpClient instance (static)
│  └─ Stream responses, don't buffer entire response
│
├─ Memory Optimization
│  ├─ Limit conversation history (last 5 turns)
│  ├─ Clear regex match collection after use
│  ├─ Don't retain large AudioClips after transcription
│  └─ Monitor for memory leaks (retained references)
│
├─ UI Optimization
│  ├─ Update TextMeshPro less frequently (batching)
│  ├─ Cache Layout recalculation
│  ├─ Use TextMeshPro-enabled UI
│  └─ Avoid animated text (use opacity instead)
│
└─ GPU Optimization
   ├─ Verify Ollama using GPU (check console)
   ├─ Set appropriate batch size
   ├─ Use smaller model if memory constrained
   └─ Monitor GPU usage with nvidia-smi
```

## 6. Testing Strategy

```
UNIT TEST EXAMPLES (NUnit)

[Test]
public void DialogueManager_GrantsTurnOnlyWhenFree()
{
    // Arrange
    manager.currentSpeaker = null;
    
    // Act
    bool result = manager.RequestTurn("NPC_A");
    
    // Assert
    Assert.IsTrue(result);
    Assert.AreEqual("NPC_A", manager.currentSpeaker);
}

[Test]
public void NPCMemory_StoresFacts()
{
    // Arrange
    var memory = new NPCMemory();
    
    // Act
    memory.AddFact("user_name", "John");
    string context = memory.GetContext();
    
    // Assert
    Assert.Contains("John", context);
}

[Test]
public async Task OllamaChatClient_StreamsTokens()
{
    // Arrange
    var client = new OllamaChatClient();
    var tokenCount = 0;
    
    // Act
    await client.SendChatAsync(
        messages,
        0.7f,
        1.1f,
        null,
        (token) => { tokenCount++; }
    );
    
    // Assert
    Assert.Greater(tokenCount, 0);
}

INTEGRATION TEST EXAMPLES

[Test]
[Timeout(30000)]
public async Task FullInterview_CompletesWithoutCrash()
{
    // Full end-to-end test
    // ...
}

PERFORMANCE TEST EXAMPLE

[Performance]
public void ResponseProcessing_UnderLatencyBudget()
{
    Measure.Frames()
        .Scope(() => ProcessToken("word"))
        .WarmupCount(10)
        .MeasurementCount(100)
        .Maximum(5); // ms
}
```

# UML Class Diagrams

## 1. Core Manager Classes

```
┌─────────────────────────────────┐
│     DialogueManager             │
│     (Singleton)                 │
├─────────────────────────────────┤
│ - Instance: DialogueManager     │
│ - currentPhase: InterviewPhase  │
│ - currentSpeaker: string        │
│ - lastSpeakerName: string       │
│ - totalTurns: int               │
│ - conclusionTurnThreshold: int  │
├─────────────────────────────────┤
│ + RequestTurn(npcName): bool    │
│ + GrantTurn(npcName): void      │
│ + ReleaseTurn(): void           │
│ + GetTurnHistory(): string      │
│ + WasLastSpeaker(name): bool    │
│ + ClearHistory(): void          │
│ + StartInterview(): void        │
│ + GetCurrentPhase(): Phase      │
└─────────────────────────────────┘
         △
         │ manages
         │
    ┌────┴─────────────────────────┐
    │                               │
┌───┴──────────────────┐    ┌──────┴───────────────────┐
│   NPCManager         │    │  NPCChatInstance         │
│   (Singleton)        │    │  (Per NPC)               │
├──────────────────────┤    ├──────────────────────────┤
│ - Instance: NPCMgr   │    │ - npcProfile: NPCProfile │
│ - npcInstances[]     │    │ - memory: NPCMemory      │
│ - globalTTSEnabled   │    │ - ollamaClient: Client   │
│ - currentNPCTalking  │    │ - ttsHandler: TTS        │
├──────────────────────┤    │ - isCurrentlySpeaking    │
│ + Register(npc)      │    ├──────────────────────────┤
│ + GetByName(n): NPC  │    │ + Send(text): Task      │
│ + BroadcastQuestion()│    │ + GetSystemPrompt()     │
│ + SetTTSEnabled()    │    │ + BuildMessages()       │
│ + ClearInstances()   │    │ + OnTokenReceived()     │
└──────────────────────┘    │ + UpdateMemory()        │
                            │ + ProcessMetadata()     │
                            └──────────────────────────┘
                                    △
                                    │ uses
                                    │
                    ┌───────────────┴───────────────┐
                    │                               │
        ┌───────────┴──────────┐      ┌────────────┴────────────┐
        │   NPCTTSHandler      │      │   OllamaChatClient      │
        ├──────────────────────┤      ├────────────────────────┤
        │ - audioSource        │      │ - http: HttpClient     │
        │ - voiceName          │      │                         │
        │ - speechQueue[]      │      ├────────────────────────┤
        │ - isPlaying          │      │ + SendChatAsync()      │
        ├──────────────────────┤      │ + BuildRequestJson()   │
        │ + Initialize()       │      │ + ExtractContent()     │
        │ + EnqueueSpeech()    │      │ + StreamTokens()       │
        │ + IsSpeaking(): bool │      │ + HandleError()        │
        │ + ProcessQueue()     │      └────────────────────────┘
        │ + StopSpeech()       │
        └──────────────────────┘
```

## 2. Configuration & Profile Classes

```
┌─────────────────────────┐
│  LLMConfig              │
│  (Singleton)            │
├─────────────────────────┤
│ - Instance: LLMConfig   │
│ - ollamaEndpoint: URL   │
│ - temperature: float    │
│ - repeatPenalty: float  │
│ - topP: float           │
│ - numCtx: int           │
├─────────────────────────┤
│ + GetInstance()         │
│ + SetEndpoint()         │
│ + ValidateConfig()      │
└─────────────────────────┘
         △
         │ reads
         │
    ┌────┴─────────────────────────────────────┐
    │                                          │
┌───┴──────────────────────┐      ┌───────────┴──────────────┐
│  NPCProfile              │      │  NPCMemory               │
├──────────────────────────┤      ├──────────────────────────┤
│ - name: string           │      │ - keyFacts: List<string> │
│ - systemPrompt: string   │      │ - conversationHistory[]  │
│ - personality: string    │      │ - previousResponses[]    │
│ - role: string           │      │ - lastSpeech: string     │
│ - voiceName: string      │      │ - interactionCount: int  │
│ - enableTTS: bool        │      ├──────────────────────────┤
│ - audioSource            │      │ + AddFact()              │
│ - animatorConfig         │      │ + GetContext(): string   │
│ - temperature: float     │      │ + Update(text)           │
│ - repeatPenalty: float   │      │ + ClearHistory()         │
├──────────────────────────┤      │ + SummarizeContext()     │
│ + GetFullSystemPrompt()  │      │ + GetLastNTurns(n)      │
│ + Validate()             │      └──────────────────────────┘
│ + Clone()                │
└──────────────────────────┘
```

## 3. Input/Output Classes

```
┌──────────────────────────────────┐
│   WhisperContinuous              │
│   (STT - Speech To Text)         │
├──────────────────────────────────┤
│ - modelFileName: string          │
│ - modelPath: string              │
│ - micDevice: string              │
│ - chunkDuration: float           │
│ - isProcessing: bool             │
├──────────────────────────────────┤
│ + Start(): void                  │
│ + CaptureMicrophone(): IEnum     │
│ + SaveWav(): void                │
│ + OnDisable(): void              │
│ + OnApplicationQuit(): void      │
└──────────────────────────────────┘
         │
         │ transcribes to
         ▼
┌──────────────────────────────────┐
│  WhisperManager (DLL Wrapper)    │
├──────────────────────────────────┤
│ - modelPath: string              │
│ - modelLoaded: bool              │
├──────────────────────────────────┤
│ + InitModel(path): bool          │
│ + Transcribe(wavPath): string    │
│ + Unload(): void                 │
│ + IsModelLoaded(): bool          │
└──────────────────────────────────┘
         │
         │ calls native
         ▼
    [WHISPER.DLL]
    (C++ OpenAI Whisper)
```

## 4. Data Flow Classes

```
┌──────────────────────────────┐
│  ChatMessage (Data)          │
├──────────────────────────────┤
│ + role: string ("user",      │
│              "assistant",    │
│              "system")       │
│ + content: string            │
└──────────────────────────────┘
         │
         │ list of
         ▼
┌──────────────────────────────┐      ┌─────────────────────┐
│  ChatResponse (Data)         │      │  OllamaRequest      │
├──────────────────────────────┤      ├─────────────────────┤
│ + content: string            │      │ + model: string     │
│ + isComplete: bool           │      │ + messages: List    │
│ + error: string              │      │ + stream: bool      │
│ + tokensPerSecond: float     │      │ + options:          │
│ + totalTokens: int           │      │   - temperature     │
└──────────────────────────────┘      │   - repeat_penalty  │
                                      │   - top_p           │
                                      └─────────────────────┘
```

## 5. Interview Phase State Machine

```
                    ┌─────────────────┐
                    │   START         │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ INTRODUCTION    │
                    │                 │
                    │ - NPC greets    │
                    │ - Set context   │
                    │ - Ask Q1        │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ MAIN            │
                    │                 │
                    │ - Back & forth   │
                    │ - Multiple Qs    │
                    │ - Turn by turn   │
                    │ - Update memory  │
                    │ - Until threshold
                    └────────┬────────┘
                             │ (turns >= threshold)
                    ┌────────▼──────────┐
                    │ CONCLUSION       │
                    │                  │
                    │ - Wrap up        │
                    │ - Thank you      │
                    │ - Final remarks  │
                    └────────┬─────────┘
                             │
                    ┌────────▼────────┐
                    │   END           │
                    └─────────────────┘
```

## 6. Interaction Sequence

```
NPCChatInstance.Send()
│
├─ 1. Get current phase from DialogueManager
│      └─ Affects system prompt
│
├─ 2. Build system prompt
│      ├─ Base personality
│      ├─ Add memory facts
│      ├─ Add turn history
│      └─ Add phase instructions
│
├─ 3. Create ChatMessage array
│      ├─ [0] system: <full prompt>
│      └─ [1] user: <user text>
│
├─ 4. Call OllamaChatClient.SendChatAsync()
│      │
│      ├─ POST to Ollama HTTP endpoint
│      │   └─ {model, messages, stream: true}
│      │
│      ├─ Stream response, per token:
│      │   └─ onTokenReceived callback fires
│      │
│      └─ Return full ChatResponse
│
├─ 5. ProcessToken() called for each token
│      │
│      ├─ Accumulate to displayBuffer
│      ├─ Check for [META]...[/META]
│      │   └─ ParseMetadata() → Extract facts
│      │
│      └─ Enqueue TTS chunks on sentence end
│
├─ 6. UpdateMemory() stores facts
│      └─ Next prompt includes these facts
│
├─ 7. NPCTTSHandler.EnqueueSpeech()
│      ├─ Chunk text into sentences
│      ├─ Call TTS API per chunk
│      └─ Stream audio to AudioSource
│
└─ 8. Release turn
       └─ DialogueManager.ReleaseTurn()
```

## 7. Tutorial Flow

```
┌─────────────────────────────┐
│   TutorialManager           │
├─────────────────────────────┤
│ - npcProfile: NPCProfile    │
│ - outputText: Text UI       │
│ - userInput: InputField     │
│ - interviewCanvas: Canvas   │
│ - objectToHideOnComplete    │
├─────────────────────────────┤
│ + Start()                   │
│   ├─ Show welcome message   │
│   ├─ WarmUpSystem()         │
│   └─ Hook input submit      │
│                             │
│ + WarmUpSystem()            │
│   ├─ Silent LLM call        │
│   └─ Initialize system      │
│                             │
│ + ContinueToInterview()     │
│   ├─ Hide tutorial UI       │
│   ├─ Show interview Canvas  │
│   └─ Start interview        │
│                             │
│ + TransitionToInterview()   │
│   ├─ Clear history          │
│   ├─ Reset NPCs             │
│   └─ Call DialogueManager   │
│       .StartInterview()     │
└─────────────────────────────┘
         │
         │ transitions to
         ▼
    Interview Phase
```

## 8. Error Handling Hierarchy

```
                    ┌─────────────────────┐
                    │ Exception Caught    │
                    └────────────┬────────┘
                                 │
                    ┌────────────┴────────────┐
                    │                        │
            ┌───────▼──────────┐    ┌───────▼──────────┐
            │ HTTP Error       │    │ Timeout Error    │
            ├──────────────────┤    ├──────────────────┤
            │ - 500 Server     │    │ - Cancel request │
            │ - 404 Not found  │    │ - Retry logic    │
            │ - Connection fail│    │ - Fallback UI    │
            └───────┬──────────┘    └───────┬──────────┘
                    │                        │
                    └────────────┬───────────┘
                                 │
                    ┌────────────▼────────────┐
                    │ Log Error              │
                    │ Display to User        │
                    │ Graceful Fallback      │
                    └────────────────────────┘
```

# Technical Architecture & Deployment

## 1. Technology Stack

```
┌──────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                   │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Unity UI Framework                                      │
│  ├─ Canvas (World/Screen Space)                         │
│  ├─ TextMeshPro (Text Display & Input)                  │
│  │  ├─ outputText (NPC responses)                       │
│  │  ├─ userInput (User text entry)                      │
│  │  └─ npcNameLabel (Speaker identification)            │
│  │                                                       │
│  └─ AudioSource (Spatial audio playback)                │
│                                                          │
│  Visual Feedback                                         │
│  ├─ Real-time text streaming                            │
│  ├─ Speaker indicators                                  │
│  └─ Loading states                                      │
│                                                          │
└──────────────────────────────────────────────────────────┘
                     △
                     │ Renders
                     │
┌──────────────────────────────────────────────────────────┐
│                 CORE APPLICATION LAYER                  │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Game Management                                         │
│  ├─ DialogueManager (Turn coordination)                 │
│  ├─ NPCManager (Entity registry)                        │
│  └─ InterviewUIManager (Screen transitions)             │
│                                                          │
│  Interview Agents                                        │
│  ├─ NPCChatInstance x2 (Interview agents)               │
│  ├─ NPCProfile (Personality config)                     │
│  └─ NPCMemory (Contextual persistence)                  │
│                                                          │
│  Input Processing                                        │
│  ├─ WhisperContinuous (STT orchestration)               │
│  └─ WhisperManager (DLL wrapper)                        │
│                                                          │
│  Output Processing                                       │
│  ├─ NPCTTSHandler (TTS orchestration)                   │
│  └─ TTS API integration (Edge TTS, etc)                 │
│                                                          │
│  Utilities                                               │
│  ├─ UnityMainThreadDispatcher (Async → Main)            │
│  └─ LLMConfig (Centralized settings)                    │
│                                                          │
└──────────────────────────────────────────────────────────┘
         △                          △
         │                          │
         │ Invokes                  │ Streams
         │                          │
┌────────┴──────────────────────┬──┴──────────────────────┐
│                               │                        │
│  ┌────────────────────────────▼────────────────────┐  │
│  │ EXTERNAL API LAYER                             │  │
│  │                                                 │  │
│  │  1. LLM Inference                              │  │
│  │     └─ OllamaChatClient (HTTP client)          │  │
│  │        ├─ HTTP POST to Ollama API              │  │
│  │        ├─ Stream response via chunked encoding │  │
│  │        └─ JSON serialization/deserialization  │  │
│  │                                                 │  │
│  │  2. Speech Synthesis                           │  │
│  │     └─ Edge TTS API (or similar)               │  │
│  │        ├─ REST POST with text chunk            │  │
│  │        ├─ Audio MP3/WAV stream                 │  │
│  │        └─ Integrated with NPCTTSHandler        │  │
│  │                                                 │  │
│  │  3. Speech Recognition                         │  │
│  │     └─ Whisper DLL (local inference)           │  │
│  │        ├─ C++ native library                   │  │
│  │        ├─ GGML quantized models                │  │
│  │        └─ Local processing (no API calls)      │  │
│  │                                                 │  │
│  └────────────────────────────────────────────────┘  │
│                                                       │
└───────────────────────────────────────────────────────┘
         △                    △
         │ HTTP REST          │ Local Process
         │ API Calls          │ Invocation
         │                    │
         │                    │
    ┌────┴──────────┐    ┌────┴──────────────┐
    │ OLLAMA SERVER │    │ WHISPER.DLL       │
    │ (Remote or    │    │ (Embedded C++)    │
    │ Local)        │    │                   │
    │               │    │ OpenAI Whisper    │
    │ Llama Models  │    │ Small/Tiny/Base   │
    │ 7B/13B/70B    │    │ Models (GGML fmt) │
    │               │    │                   │
    └───────────────┘    └───────────────────┘
```

## 2. Network Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    UNIVERSITY NETWORK                   │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │          RESEARCHER WORKSTATION (Windows)        │  │
│  │                                                  │  │
│  │  ┌──────────────────────────────────────────┐  │  │
│  │  │  Unity Game Engine (C#)                  │  │  │
│  │  │  ├─ Interview Simulation Application     │  │  │
│  │  │  ├─ NPCChatInstance (Interviewer A & B) │  │  │
│  │  │  └─ UI/Input Management                 │  │  │
│  │  └───────┬──────────────────────────────────┘  │  │
│  │          │                                     │  │
│  │  ┌───────▼──────────────────────────────────┐  │  │
│  │  │  Whisper.DLL (Local STT)                 │  │  │
│  │  │  ├─ Microphone input capture             │  │  │
│  │  │  ├─ GGML model inference (local)         │  │  │
│  │  │  └─ Audio transcription                  │  │  │
│  │  └──────────────────────────────────────────┘  │  │
│  │                                                  │  │
│  └─────────────┬──────────────────┬────────────────┘  │
│                │ HTTP REST API    │ Microphone        │
│                │ (localhost:5000) │ Input             │
│                │                  │                   │
└────────────────┼──────────────────┼───────────────────┘
                 │ (if remote)      │
                 │                  │
     ┌───────────┴────┐        ┌────▼──────────┐
     │                │        │                │
     ▼                ▼        │ (Local Device) │
  ┌────────────────────┐       └────────────────┘
  │ OLLAMA SERVER      │
  │ (Optional Remote)  │
  │                    │
  │ Llama2 7B Model    │
  │ Running on GPU     │
  │ Port: 11434        │
  │                    │
  └────────────────────┘
```

## 3. Data Flow Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    DATA FLOW LAYERS                      │
├──────────────────────────────────────────────────────────┤
│                                                          │
│ ┌────────────────────────────────────────────────────┐ │
│ │ LAYER 1: USER INPUT                               │ │
│ ├────────────────────────────────────────────────────┤ │
│ │                                                    │ │
│ │ Microphone Audio Stream (16-bit PCM, 16kHz)      │ │
│ │  → WhisperContinuous (3-sec chunks)              │ │
│ │  → WAV File (Persistent Storage)                 │ │
│ │  → WhisperManager (DLL Transcription)            │ │
│ │  → Text String (UTF-8)                           │ │
│ │  → InputField UI                                 │ │
│ │                                                    │ │
│ └────────────────────────────────────────────────────┘ │
│                                                        │
│ ┌────────────────────────────────────────────────────┐ │
│ │ LAYER 2: DIALOGUE COORDINATION                     │ │
│ ├────────────────────────────────────────────────────┤ │
│ │                                                    │ │
│ │ Text Input + Speaker ID                          │ │
│ │  → DialogueManager.RequestTurn()                  │ │
│ │  → Turn granted/denied (boolean)                  │ │
│ │  → Speaker state updated                         │ │
│ │                                                    │ │
│ └────────────────────────────────────────────────────┘ │
│                                                        │
│ ┌────────────────────────────────────────────────────┐ │
│ │ LAYER 3: CONTEXT BUILDING                         │ │
│ ├────────────────────────────────────────────────────┤ │
│ │                                                    │ │
│ │ System Prompt (string)                           │ │
│ │  ├─ Base personality (from NPCProfile)           │ │
│ │  ├─ Memory facts (from NPCMemory)                │ │
│ │  ├─ Turn history (from DialogueManager)          │ │
│ │  └─ Phase instructions (Introduction/Main/End)  │ │
│ │                                                    │ │
│ │ Message Array (ChatMessage[])                    │ │
│ │  ├─ [0] role:"system", content: prompt          │ │
│ │  └─ [1] role:"user", content: user text         │ │
│ │                                                    │ │
│ │  → JSON serialization                            │ │
│ │                                                    │ │
│ └────────────────────────────────────────────────────┘ │
│                                                        │
│ ┌────────────────────────────────────────────────────┐ │
│ │ LAYER 4: LLM INFERENCE                            │ │
│ ├────────────────────────────────────────────────────┤ │
│ │                                                    │ │
│ │ HTTP POST Request (JSON)                         │ │
│ │  → Content-Type: application/json                │ │
│ │  → Body: {model, messages, stream: true}        │ │
│ │  → Target: http://localhost:11434/api/chat       │ │
│ │                                                    │ │
│ │ Response Stream (chunked encoding)               │ │
│ │  ├─ Newline-delimited JSON                       │ │
│ │  ├─ Per-token delta updates                      │ │
│ │  └─ Until "done": true                           │ │
│ │                                                    │ │
│ │ Full Response (string)                           │ │
│ │  └─ Concatenated tokens                          │ │
│ │                                                    │ │
│ └────────────────────────────────────────────────────┘ │
│                                                        │
│ ┌────────────────────────────────────────────────────┐ │
│ │ LAYER 5: RESPONSE PROCESSING                      │ │
│ ├────────────────────────────────────────────────────┤ │
│ │                                                    │ │
│ │ Full Response Text (string)                      │ │
│ │  → Regex parse [META]...[/META]                  │ │
│ │  → Extract key-value pairs (facts)               │ │
│ │                                                    │ │
│ │ Extracted Facts (Dictionary)                     │ │
│ │  → Update NPCMemory.keyFacts                     │ │
│ │  → Add to conversationHistory                    │ │
│ │  → Persist for next turns                        │ │
│ │                                                    │ │
│ │ Display Text (stripped of metadata)              │ │
│ │  → outputText.text update                        │ │
│ │  → Stream UI updates per token                   │ │
│ │                                                    │ │
│ └────────────────────────────────────────────────────┘ │
│                                                        │
│ ┌────────────────────────────────────────────────────┐ │
│ │ LAYER 6: TEXT-TO-SPEECH SYNTHESIS                 │ │
│ ├────────────────────────────────────────────────────┤ │
│ │                                                    │ │
│ │ Response Text (chunks)                           │ │
│ │  ├─ Split on sentence boundaries (. ! ?)        │ │
│ │  └─ Queue for async synthesis                    │ │
│ │                                                    │ │
│ │ TTS API Call (per chunk)                         │ │
│ │  → POST to Edge TTS / similar                    │ │
│ │  → Receive MP3/WAV binary stream                 │ │
│ │  → Convert to AudioClip                          │ │
│ │                                                    │ │
│ │ Audio Output                                      │ │
│ │  → AudioSource.PlayOneShot()                     │ │
│ │  → Streaming playback                            │ │
│ │  → Spatial audio positioning (optional)          │ │
│ │                                                    │ │
│ └────────────────────────────────────────────────────┘ │
│                                                        │
│ ┌────────────────────────────────────────────────────┐ │
│ │ LAYER 7: TURN MANAGEMENT                          │ │
│ ├────────────────────────────────────────────────────┤ │
│ │                                                    │ │
│ │ Audio Complete Signal                            │ │
│ │  → DialogueManager.ReleaseTurn()                 │ │
│ │  → currentSpeaker = null                         │ │
│ │  → Next NPC or User can proceed                  │ │
│ │                                                    │ │
│ └────────────────────────────────────────────────────┘ │
│                                                        │
└──────────────────────────────────────────────────────────┘
```

## 4. Memory Management

```
┌──────────────────────────────────────────────────────────┐
│              MEMORY ARCHITECTURE                         │
├──────────────────────────────────────────────────────────┤
│                                                          │
│ RUNTIME MEMORY                                           │
│ ┌────────────────────────────────────────────────────┐  │
│ │ Singleton Instances (Persistent)                  │  │
│ │ ├─ DialogueManager (turn state)                   │  │
│ │ ├─ NPCManager (NPC registry)                      │  │
│ │ ├─ LLMConfig (settings)                           │  │
│ │ └─ [Continue across scenes]                       │  │
│ │                                                    │  │
│ │ Per-Scene Memory                                  │  │
│ │ ├─ NPCChatInstance #1 + NPCMemory                 │  │
│ │ ├─ NPCChatInstance #2 + NPCMemory                 │  │
│ │ └─ [Cleared on scene load]                        │  │
│ │                                                    │  │
│ │ Streaming Buffers                                 │  │
│ │ ├─ displayBuffer (StringBuilder)                  │  │
│ │ ├─ ttsBuffer (StringBuilder)                      │  │
│ │ ├─ metadataBuffer (StringBuilder)                 │  │
│ │ └─ [Cleared per response]                        │  │
│ │                                                    │  │
│ │ Queue Structures                                  │  │
│ │ ├─ speechQueue (List<string>)                    │  │
│ │ ├─ decisionsThisRound (List<tuple>)              │  │
│ │ └─ speakerHistory (List<string>)                 │  │
│ │                                                    │  │
│ └────────────────────────────────────────────────────┘  │
│                                                          │
│ PERSISTENT STORAGE                                       │
│ ┌────────────────────────────────────────────────────┐  │
│ │ Audio Files                                        │  │
│ │ ├─ Application.persistentDataPath/mic_chunk.wav  │  │
│ │ └─ Deleted after transcription                   │  │
│ │                                                    │  │
│ │ Model Files                                       │  │
│ │ ├─ StreamingAssets/Whisper/ggml-tiny.bin        │  │
│ │ └─ ~100MB embedded model                         │  │
│ │                                                    │  │
│ │ Configuration (Optional)                         │  │
│ │ ├─ PlayerPrefs (LLM settings)                    │  │
│ │ └─ JSON config files                             │  │
│ │                                                    │  │
│ └────────────────────────────────────────────────────┘  │
│                                                          │
│ MEMORY OPTIMIZATION                                      │
│ ├─ Bounded history (last N turns)                       │
│ ├─ StringBuilder over string concatenation              │
│ ├─ Object pooling for buffers                          │
│ └─ Lazy loading of models                              │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

## 5. Error Handling & Resilience

```
┌──────────────────────────────────────────────────────────┐
│              ERROR HANDLING STRATEGY                     │
├──────────────────────────────────────────────────────────┤
│                                                          │
│ LAYER 1: PREVENTION                                      │
│ ├─ Input validation (null checks)                       │
│ ├─ Configuration validation (LLMConfig)                 │
│ ├─ Component existence checks                          │
│ └─ Pre-flight checks (microphone, network)             │
│                                                          │
│ LAYER 2: DETECTION                                       │
│ ├─ Try-catch blocks around I/O                         │
│ ├─ Timeout logic (30-60 sec)                           │
│ ├─ Health checks (IsRecording, IsSpeaking)             │
│ └─ HTTP status code checking                           │
│                                                          │
│ LAYER 3: RECOVERY                                        │
│ ├─ Graceful fallbacks                                   │
│ │  ├─ No microphone → text input only                  │
│ │  ├─ No TTS → silent text display                     │
│ │  ├─ Network timeout → retry with backoff             │
│ │  └─ Model load fail → skip feature                  │
│ │                                                       │
│ ├─ Force release mechanisms                             │
│ │  ├─ DialogueManager.ReleaseTurn(force: true)         │
│ │  ├─ CancellationToken.Cancel()                       │
│ │  └─ OnDisable() cleanup                             │
│ │                                                       │
│ └─ User communication                                   │
│    ├─ Error messages in UI                             │
│    ├─ Debug logs for developers                        │
│    └─ Status indicators                                 │
│                                                          │
│ LAYER 4: LOGGING                                         │
│ ├─ Debug.Log() for normal flow                         │
│ ├─ Debug.LogWarning() for issues                       │
│ ├─ Debug.LogError() for failures                       │
│ └─ Timestamp + context in all logs                     │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

## 6. Scalability Considerations

```
Current System (2 NPCs):
┌─────────────────────────────────────┐
│ 2x NPCChatInstance                  │
│ 2x NPCMemory                        │
│ 1x DialogueManager (Singleton)      │
│ 1x OllamaChatClient (Shared)        │
│ Ollama Server (shared, sequential)  │
└─────────────────────────────────────┘
Bottleneck: Sequential API calls to Ollama


Potential Scalability:
┌─────────────────────────────────────┐
│ N x NPCChatInstance                 │
│ N x NPCMemory                       │
│ 1x DialogueManager (Singleton)      │
│ 1x OllamaChatClient (thread pool)   │
│ Multiple Ollama instances (parallel)|
└─────────────────────────────────────┘
Improvement: Parallel LLM calls


For Real-Time Constraints:
├─ Queue-based message handling
├─ Priority levels (urgent > normal)
├─ Load balancing across LLM servers
├─ Caching frequent responses
└─ Pre-generation of common paths
```

# Job Interview Simulation System Architecture

## High-Level System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    UNITY GAME ENGINE                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────┐         ┌──────────────────┐              │
│  │  INTERVIEW UI    │         │  TUTORIAL UI     │              │
│  │  Canvas/Panels   │         │  Canvas/Panels   │              │
│  └────────┬─────────┘         └────────┬─────────┘              │
│           │                            │                        │
│  ┌────────▼────────────────────────────▼────────┐              │
│  │         INPUT/OUTPUT LAYER                    │              │
│  │  ┌─────────────┐           ┌──────────────┐  │              │
│  │  │ Whisper STT │◄──────────►│ NPC TTS      │  │              │
│  │  │ (Microphone)│           │ (Text→Voice) │  │              │
│  │  └──────┬──────┘           └──────┬───────┘  │              │
│  └─────────┼────────────────────────┼─────────┘              │
│            │                        │                         │
│  ┌─────────▼────────────────────────▼─────────┐             │
│  │    DIALOGUE & NPC COORDINATION LAYER         │             │
│  │  ┌─────────────────┐  ┌─────────────────┐  │             │
│  │  │ DialogueManager │  │  NPCManager     │  │             │
│  │  │ (Turn Control)  │  │ (NPC Registry)  │  │             │
│  │  └────────┬────────┘  └────────┬────────┘  │             │
│  └───────────┼───────────────────┼──────────┘            │
│              │                   │                        │
│  ┌───────────▼──────────────────▼──────────┐            │
│  │    NPC CHAT INSTANCES (x2)              │            │
│  │  ┌──────────────┐  ┌──────────────┐    │            │
│  │  │ NPCChat#1    │  │ NPCChat#2    │    │            │
│  │  │ Interviewer1 │  │ Interviewer2 │    │            │
│  │  │ + Memory     │  │ + Memory     │    │            │
│  │  └──────┬───────┘  └──────┬───────┘    │            │
│  └─────────┼──────────────────┼──────────┘            │
│            │                  │                        │
│  ┌─────────▼──────────────────▼──────────┐            │
│  │    LLM INFERENCE LAYER                 │            │
│  │  ┌──────────────┐  ┌──────────────┐   │            │
│  │  │ OllamaChatClient (HTTP Mode)  │   │            │
│  │  │ ▲ Streaming token processing  │   │            │
│  │  │ ▲ Turn coordination           │   │            │
│  │  │ ▲ Context management          │   │            │
│  │  └──────────────┘  └──────────────┘   │            │
│  └─────────┬──────────────────────────┘            │
└────────────┼──────────────────────────────────────┘
             │
             │ HTTP API (REST)
             ▼
   ┌─────────────────────┐
   │  OLLAMA SERVER      │
   │  (LLM Inference)    │
   │  - Llama2/3 Models  │
   │  - 7B/13B variants  │
   │  - Streaming API    │
   └─────────────────────┘
```

## Component Relationships

### 1. **Input Pipeline**
```
Microphone
   ↓
WhisperContinuous (STT)
   ↓ Audio Transcription
InputField (Text)
   ↓ User Text
NPCChatInstance (Process)
```

### 2. **Interview Coordination**
```
DialogueManager (Turn Control)
   ├─ RequestTurn() → Grant/Deny
   ├─ TrackSpeaker() → History
   └─ GetTurnHistory() → Context
        ↓
   NPCManager (NPC Registry)
        ├─ NPCChatInstance #1
        ├─ NPCChatInstance #2
        └─ BroadcastQuestion()
```

### 3. **LLM Processing**
```
User Input
   ↓
NPCChatInstance.Send()
   ├─ Build System Prompt
   ├─ Format Context (Memory, History)
   └─ Build ChatMessages
        ↓
   OllamaChatClient.SendChatAsync()
   ├─ POST to Ollama HTTP API
   ├─ Stream Tokens
   └─ Callback: onTokenReceived()
        ↓
   ProcessToken()
   ├─ Parse Metadata [META]...[/META]
   ├─ Update NPCMemory
   └─ Queue TTS
        ↓
   NPCTTSHandler
   ├─ Enqueue speech chunks
   ├─ Stream audio synthesis
   └─ Play audio
```

### 4. **Memory System**
```
NPCMemory (Per-NPC)
   ├─ Key Facts (persistent)
   ├─ Conversation Context (sliding window)
   └─ Previous Responses (for consistency)
        ↓
   Built into System Prompt
   ├─ NPC Personality
   ├─ Role Context
   ├─ Facts to Remember
   └─ Conversation History
        ↓
   LLM uses memory for coherent responses
```

## Data Flow: User Question → NPC Response

```
1. USER INPUT PHASE
   ├─ Whisper captures audio (3-second chunks)
   ├─ Transcribes to text
   └─ Displays in InputField

2. DIALOGUE COORDINATION
   ├─ DialogueManager grants NPC turn
   ├─ NPCChatInstance.Send() processes user input
   ├─ Builds system prompt with memory
   └─ Creates ChatMessage array

3. LLM INFERENCE
   ├─ OllamaChatClient posts to Ollama
   ├─ Streams tokens via HTTP
   ├─ onTokenReceived() callback fires per token
   └─ Accumulates full response

4. RESPONSE PROCESSING
   ├─ ParseMetadata() extracts [META]...[/META]
   ├─ UpdateMemory() stores facts
   ├─ ProcessToken() builds display text
   └─ Enqueue TTS chunks

5. AUDIO OUTPUT
   ├─ NPCTTSHandler buffers sentence chunks
   ├─ Calls voice synthesis API (Edge TTS)
   ├─ Streams audio to AudioSource
   └─ Plays audio while updating UI

6. NEXT TURN
   ├─ Release speaker lock
   ├─ Check interview phase (Intro/Main/Conclusion)
   └─ Allow next NPC or user to speak
```

## Key Design Patterns

### **Singleton Pattern**
- `DialogueManager` - Global turn coordination
- `NPCManager` - Central NPC registry
- `LLMConfig` - Centralized LLM settings

### **Observer Pattern**
- `onTokenReceived` callback in OllamaChatClient
- Enables real-time UI streaming
- Decoupled LLM client from display logic

### **Strategy Pattern**
- `NPCProfile` defines personality strategy
- `systemPrompt` customization per NPC
- Different NPCs use same chat pipeline

### **State Machine**
- `DialogueManager.InterviewPhase` (Introduction → Main → Conclusion)
- Controls dialogue flow and turn allocation

### **Buffer Pattern**
- `metadataBuffer` for parsing structured output
- `ttsBuffer` for chunking speech synthesis
- Enables streaming without loading entire response

## Configuration Management

```
LLMConfig (Singleton)
├─ ollamaEndpoint: "http://localhost:11434/api/chat"
├─ temperature: 0.7
├─ repeatPenalty: 1.1
└─ Model selection

NPCProfile (Per NPC)
├─ name: "Interviewer A"
├─ systemPrompt: "You are..."
├─ personality traits
├─ voiceName: "en-US-AriaNeural"
├─ enableTTS: true
└─ animatorConfig
```

## Performance Considerations

1. **Streaming**: Tokens processed individually, not buffered
2. **Threading**: Background Whisper (2s delay), main thread LLM calls
3. **Memory**: Fixed-size conversation history (last N turns)
4. **Audio**: Buffered sentence chunks for TTS streaming
5. **Turn Queue**: Simple blocking lock to prevent overlaps

## Error Handling

```
┌─ OllamaChatClient
│  ├─ HTTP 500 → Return error, display to user
│  └─ Network timeout → Graceful fallback
│
├─ WhisperManager
│  ├─ Model not found → Error log + return null
│  └─ Audio device error → Disable STT
│
├─ NPCTTSHandler
│  ├─ Voice not found → Log warning, continue
│  └─ API failure → Fall back to silent text display
│
└─ DialogueManager
   ├─ Turn timeout → Force release turn
   └─ NPC crash → Remove from speaker queue
```

## Extensibility Points

1. **Add new LLM**: Swap `OllamaChatClient` with `GPTChatClient`
2. **Add new NPC**: Create `NPCProfile`, drag into scene, register with `NPCManager`
3. **Add new voice**: Add voice name to `NPCProfile.voiceName`
4. **Custom memory system**: Extend `NPCMemory` class
5. **Custom turn logic**: Override `DialogueManager.RequestTurn()`

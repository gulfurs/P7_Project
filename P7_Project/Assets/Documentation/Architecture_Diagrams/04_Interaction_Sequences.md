# Component Interaction Sequences

## 1. Complete Interview Flow Sequence Diagram

```
┌─────────┐  ┌──────────┐  ┌────────┐  ┌──────────┐  ┌──────────┐  ┌────────┐
│  User   │  │ Tutorial │  │Dialogue│  │  NPC    │  │ Ollama  │  │  TTS   │
│         │  │ Manager  │  │Manager │  │ Instance│  │ Client  │  │Handler │
└────┬────┘  └────┬─────┘  └───┬────┘  └────┬───┘  └────┬────┘  └───┬────┘
     │            │            │            │           │            │
     │ Scene Load │            │            │           │            │
     ├───────────►│            │            │           │            │
     │            │            │            │           │            │
     │            │ Show Welcome & System Warmup        │            │
     │            ├──────────────────────────────────────────────────┤
     │            │ (Silent LLM call)                                 │
     │            │                                                   │
     │ Type "continue"                                                │
     ├───────────►│                                                   │
     │            │                                                   │
     │            │ Hide Tutorial, Show Interview                    │
     │            ├──────────┬─────────────────────────┬──────────┤
     │            │          │                        │           │
     │            │          ▼ Start Interview        │           │
     │            │   ┌───────────────────┐           │           │
     │            │   │ NPC Introduction  │           │           │
     │            │   └────────┬──────────┘           │           │
     │            │            │                      │           │
     │            │            │ RequestTurn("NPC_A")│           │
     │            │            │◄─────────────────────┤           │
     │            │            │                      │           │
     │            │ Check Speaker Status              │           │
     │            ├────────────────────►✓ Available  │           │
     │            │                      │           │           │
     │            │ GrantTurn("NPC_A")   │           │           │
     │            │                      │           │           │
     │            │        ┌─────────────────────────────────────┤
     │            │        │Build System Prompt + Memory        │
     │            │        │                                    │
     │            │        │ Call Ollama API                   │
     │            │        │                                    │
     │            │        ├─────────────────────►              │
     │            │        │                       │ POST /api/chat
     │            │        │                       │ {messages, stream}
     │            │        │                       │
     │            │        │                       ▼ [LLM Running]
     │            │        │                       │
     │            │        │◄────Stream Tokens────-┤
     │            │        │                       │
     │            │  ┌─────▼─────────────────────┐│
     │            │  │ ProcessToken (real-time) ││
     │            │  │ - Update display         ││
     │            │  │ - Parse [META]           ││
     │            │  │ - Buffer TTS chunks      ││
     │            │  └────────┬──────────────────┘│
     │            │           │ Enqueue chunk    │
     │            │           │                  │
     │            │           ▼                  │
     │            │  ┌──────────────────────────┐│
     │            │  │ TTS Processing           ││
     │            │  │ - Synthesize audio       ││
     │            │  ├─────────────────────────►│
     │            │  │                    Play Audio
     │            │  │◄────Audio Stream─────────┤
     │            │  │                          │
     │            │  │ Update Memory (facts)    │
     │            │  └──────────────────────────┘│
     │            │                              │
     │            │ Audio Complete              │
     │            │ Release Turn                │
     │            │                             │
     │ [See User Question Loop]                  │
     │ User Responds via Microphone               │
     │ ├─ Whisper STT                            │
     │ ├─ Text input                             │
     │ └─ Send to NPC                            │
```

## 2. User Question Processing Sequence

```
┌────────┐  ┌──────────┐  ┌────────┐  ┌────────────┐  ┌──────────┐
│Whisper │  │InputField│  │  NPC   │  │Dialogue    │  │ Ollama   │
│Manager │  │UI        │  │Instance│  │Manager     │  │ Client   │
└───┬────┘  └────┬─────┘  └────┬───┘  └──────┬─────┘  └────┬─────┘
    │            │             │             │            │
    │ Microphone │             │             │            │
    │ Recording │             │             │            │
    │ (3 sec) │             │             │            │
    │◄────────────────────────────────────────────────────┤
    │            │             │             │            │
    │ Transcribe │             │             │            │
    │ (Whisper)  │             │             │            │
    │            │             │             │            │
    │   Result: "What is your weakness?"     │            │
    │            │             │             │            │
    ├───────────►│ Update text │             │            │
    │            │             │             │            │
    │            │ User presses ENTER        │            │
    │            ├─────────────►│             │            │
    │            │             │             │            │
    │            │      Send(userText)       │            │
    │            │             │             │            │
    │            │             ├────────────►│            │
    │            │             │Request Turn │            │
    │            │             │             │            │
    │            │             │ Check: currentSpeaker == "" ?
    │            │             │             │            │
    │            │             │◄─ GrantTurn│            │
    │            │             │ OK          │            │
    │            │             │             │            │
    │            │             ├──────────────────────────►│
    │            │             │Build Messages & Send    │
    │            │             │                         │
    │            │             │◄────Stream Tokens──────┤
    │            │             │(real-time per token)   │
    │            │             │                        │
    │            │ Update UI   │                        │
    │            │◄────────────┤(displayBuffer growing) │
    │ ♪ Audio    │             │                        │
    │ Playing ◄──┤             │                        │
```

## 3. Error Recovery Sequences

```
┌──────────────┐       ┌──────────────┐
│ HTTP Error   │       │ Timeout      │
└──────┬───────┘       └──────┬───────┘
       │                      │
       │ 500 Server Error     │ No response
       │ or Network Fail      │ for 30 sec
       │                      │
       ▼                      ▼
    ┌──────────────────────────────┐
    │ CatchException()             │
    └──────┬───────────────────────┘
           │
           ├─ Log error detail
           ├─ Check error type
           │
           ├─ If Network:
           │  └─ Show "Connection Lost"
           │     Offer retry
           │
           ├─ If Timeout:
           │  └─ Cancel request
           │     Release turn
           │     Show "Response Timeout"
           │
           └─ If Server:
              └─ Log for debugging
                 Show generic error
                 Allow user to continue

           ▼
    ┌──────────────────────────────┐
    │ DialogueManager              │
    │ .ReleaseTurn(force: true)    │
    │                              │
    │ Return control to user       │
    │ or next NPC                  │
    └──────────────────────────────┘
```

## 4. Memory Persistence Across Turns

```
Turn 1:
┌─────────────────────────────────────────┐
│ User: "I'm nervous about public speaking"│
│                                          │
│ NPC: "I understand. Public speaking     │
│  anxiety is common. [META]              │
│  issue: public_speaking_anxiety,        │
│  severity: high                         │
│  [/META]"                               │
│                                          │
│ Memory[NPC_A].keyFacts:                 │
│ - "User has public speaking anxiety"    │
│ - "severity: high"                      │
└─────────────────────────────────────────┘
         │
         │ conversationHistory
         ▼
Turn 2:
┌────────────────────────────────────────────┐
│ User: "I practice but it doesn't help"     │
│                                             │
│ NPC System Prompt now includes:            │
│ "You know: User has public speaking        │
│  anxiety (high severity) but practices.    │
│  They're still struggling despite effort.  │
│  Show empathy and ask about specific       │
│  techniques they use."                     │
│                                             │
│ NPC: "Tell me more about your practice    │
│  routine. [META]                          │
│  emotional_state: determined_but_anxious  │
│  [/META]"                                 │
│                                             │
│ Memory[NPC_A].keyFacts:                   │
│ - "User practices but struggles"          │
│ - "emotional_state: determined_but_anxious"
└────────────────────────────────────────────┘
         │
         │
         ▼
Turn 3 (with NPC_B):
┌────────────────────────────────────────────┐
│ NPC_B receives context from DialogueManager│
│                                             │
│ NPC_B knows from history:                  │
│ - "User anxious about public speaking"     │
│ - "Severity: high"                         │
│ - "Practices but still struggles"          │
│                                             │
│ NPC_B (different interviewer) can:         │
│ - Build on this context                    │
│ - Ask complementary questions              │
│ - Show consistency across NPCs             │
│                                             │
│ NPC_B: "I see that technical skills are   │
│  strong, but presentation confidence is   │
│  an area to develop. Let's work on that." │
└────────────────────────────────────────────┘
```

## 5. Phase Transition Sequence

```
┌─────────────────────────────────────┐
│ Interview Start                     │
│ Phase = INTRODUCTION                │
│ totalTurns = 0                      │
└────────┬────────────────────────────┘
         │
         │ NPC_A greeting
         │ Sets context
         │ Asks first question
         │
         ├─ totalTurns = 1
         │
         ▼
┌─────────────────────────────────────┐
│ Main Phase begins                   │
│ totalTurns >= 1                     │
│                                     │
│ Dynamic back-and-forth:             │
│ - User answers                      │
│ - NPC_A follows up or passes        │
│ - NPC_B asks related question       │
│ - Build depth and rapport           │
│                                     │
│ Turns: 2, 3, 4, 5, ...             │
└────────┬────────────────────────────┘
         │
         │ Check: totalTurns >= conclusionTurnThreshold?
         │ (default: 10)
         │
         ├─ No: Continue main phase
         │
         ├─ Yes: Transition
         │
         ▼
┌─────────────────────────────────────┐
│ Conclusion Phase                    │
│ Phase = CONCLUSION                  │
│                                     │
│ NPC wraps up:                       │
│ - Summarize key points              │
│ - Positive reinforcement            │
│ - Final questions                   │
│ - Thank candidate                   │
│ - Close interview                   │
│                                     │
│ Turn: 11 (>= threshold)             │
└────────┬────────────────────────────┘
         │
         │ Interview Complete
         │ Store results
         │ Log performance
         │
         ▼
┌─────────────────────────────────────┐
│ END                                 │
└─────────────────────────────────────┘
```

## 6. Real-time Token Stream Processing

```
Token Stream from Ollama:
{"model":"llama2:7b","response":"I","done":false}
{"model":"llama2:7b","response":" would","done":false}
{"model":"llama2:7b","response":" say","done":false}
{"model":"llama2:7b","response":".","done":false}
...

         │ per line/token
         │ callback fires
         ▼
    ProcessToken()
         │
    ┌────┴────────────────────────────┐
    │ displayBuffer += token           │
    │ Now: "I would say"               │
    │                                  │
    │ Check ttsBuffer                  │
    │ ├─ If token is '.' → sentence   │
    │ │  └─ EnqueueSpeech(buffer)     │
    │ │     Start synthesis async      │
    │ │                               │
    │ │ ├─ Clear ttsBuffer            │
    │ │ │                             │
    │ │ └─ Continue...                │
    │ │                               │
    │ └─ Else: continue buffering     │
    │                                  │
    │ outputText.text = displayBuffer │
    │ Update UI immediately (no wait) │
    └────────────────────────────────┘
         │
    Result: User sees text appear
    word by word, hears voice
    streaming in parallel
```

# Data Flow Diagrams

## 1. Complete User Question to NPC Response Flow

```
═══════════════════════════════════════════════════════════════════════════
                    USER QUESTION → NPC RESPONSE PIPELINE
═══════════════════════════════════════════════════════════════════════════

┌─────────────────────────────────────────────────────────────────────────┐
│ PHASE 1: INPUT CAPTURE & RECOGNITION                                   │
└─────────────────────────────────────────────────────────────────────────┘

    ┌──────────────────┐
    │  User Speaks     │
    │  (into Microphone)
    └────────┬─────────┘
             │ Audio Stream
             ▼
    ┌──────────────────────────────┐
    │ WhisperContinuous            │
    │ - Listen for 3 sec chunks    │
    │ - Save chunk to WAV file     │
    └────────┬─────────────────────┘
             │ WAV File Path
             ▼
    ┌──────────────────────────────┐
    │ WhisperManager.Transcribe()  │
    │ (C++ OpenAI Whisper DLL)     │
    │ - Load GGML model            │
    │ - Run inference              │
    │ - Output: text string        │
    └────────┬─────────────────────┘
             │ Transcribed Text
             ▼
    ┌──────────────────────────────┐
    │ InputField.text updated      │
    │ "What is your weakness?"     │
    └────────┬─────────────────────┘


┌─────────────────────────────────────────────────────────────────────────┐
│ PHASE 2: TURN REQUEST & COORDINATION                                    │
└─────────────────────────────────────────────────────────────────────────┘

    ┌──────────────────────────────┐
    │ User presses ENTER           │
    │ NPCChatInstance.Send()       │
    └────────┬─────────────────────┘
             │
    ┌────────▼──────────────────────────────────┐
    │ DialogueManager.RequestTurn(npcName)      │
    │                                            │
    │ if (currentSpeaker != "")                 │
    │    return false; // Still speaking        │
    │ else                                       │
    │    GrantTurn(npcName)                     │
    │    return true;                           │
    └────────┬──────────────────────────────────┘
             │ Turn Granted
             ▼
    ┌──────────────────────────────┐
    │ currentSpeaker =             │
    │ "Interviewer_A"              │
    │ totalTurns++                 │
    └────────┬─────────────────────┘


┌─────────────────────────────────────────────────────────────────────────┐
│ PHASE 3: CONTEXT BUILDING                                               │
└─────────────────────────────────────────────────────────────────────────┘

    ┌──────────────────────────────────────────┐
    │ NPCChatInstance.GetSystemPrompt()        │
    │                                           │
    │ systemPrompt = npcProfile.systemPrompt   │
    │   + NPCMemory context                    │
    │   + Turn history                         │
    │   + Phase instructions                   │
    │                                           │
    │ Example:                                 │
    │ ───────────────────────────────────────  │
    │ "You are Interviewer A, professional,    │
    │  assessing technical skills.             │
    │                                           │
    │  Facts you know:                         │
    │  - Candidate's name: John                │
    │  - Previous answer: Worked at Company X  │
    │                                           │
    │  Turn history: Intro → First Q → Answer  │
    │  Phase: MAIN (ongoing interview)         │
    │                                           │
    │  Instructions:                           │
    │  - Ask follow-up if needed               │
    │  - Assess depth of answer                │
    │  - Show interest                         │
    │  - Use [META]...[/META] for facts"       │
    └────────┬─────────────────────────────────┘
             │ Complete System Prompt
             ▼
    ┌──────────────────────────────┐
    │ Build ChatMessage Array:     │
    │                              │
    │ [0] {                        │
    │   role: "system"             │
    │   content: <sys_prompt>      │
    │ }                            │
    │                              │
    │ [1] {                        │
    │   role: "user"               │
    │   content: "What is your     │
    │   weakness?"                 │
    │ }                            │
    └────────┬─────────────────────┘


┌─────────────────────────────────────────────────────────────────────────┐
│ PHASE 4: LLM INFERENCE - HTTP STREAMING                                 │
└─────────────────────────────────────────────────────────────────────────┘

    ┌──────────────────────────────┐
    │ OllamaChatClient             │
    │ .SendChatAsync()             │
    └────────┬─────────────────────┘
             │
             │ Build JSON Request
             ▼
    ┌────────────────────────────────────────┐
    │ {                                       │
    │   "model": "llama2:7b",                │
    │   "messages": [                        │
    │     {"role": "system",                 │
    │      "content": "You are..."},         │
    │     {"role": "user",                   │
    │      "content": "What is your...?"}    │
    │   ],                                    │
    │   "stream": true,                      │
    │   "options": {                         │
    │     "temperature": 0.7,                │
    │     "repeat_penalty": 1.1              │
    │   }                                     │
    │ }                                       │
    └────────┬────────────────────────────────┘
             │ POST to Ollama
             │ http://localhost:11434/api/chat
             ▼
    ┌─────────────────────────────┐
    │  OLLAMA SERVER              │
    │  (LLM Inference Engine)     │
    │                             │
    │  Llama2 7B Model            │
    │  - Tokenize input           │
    │  - Generate tokens          │
    │  - Apply sampling           │
    │  - Stream response          │
    └────────┬────────────────────┘
             │ HTTP Stream (chunked)
             │ Newline-delimited JSON
             │
             ├─ Token 1: "I"
             ├─ Token 2: "would"
             ├─ Token 3: "say"
             ├─ Token 4: "my"
             ├─ Token 5: "weakness"
             ├─ Token 6: "is"
             ├─ Token 7: "..."
             └─ ...continue until [DONE]
             │
             ▼
    ┌────────────────────────────────┐
    │ onTokenReceived callback       │
    │ called per token (streaming)   │
    │                                │
    │ ProcessToken(token) runs       │
    │ in real-time on main thread    │
    └────────┬───────────────────────┘


┌─────────────────────────────────────────────────────────────────────────┐
│ PHASE 5: RESPONSE PROCESSING - REAL-TIME STREAMING                      │
└─────────────────────────────────────────────────────────────────────────┘

    ┌─────────────────────────────────┐
    │ ProcessToken()                  │
    │ (runs per token, real-time)     │
    │                                 │
    │ FOR each token received:        │
    │   │                             │
    │   ├─ displayBuffer += token     │
    │   │   (accumulate for display)  │
    │   │                             │
    │   ├─ metadataBuffer += token    │
    │   │   (check for [META])        │
    │   │                             │
    │   ├─ if [META] found:           │
    │   │   └─ ParseMetadata()        │
    │   │      └─ Extract facts       │
    │   │         "name: John"        │
    │   │         "skill: Python"     │
    │   │                             │
    │   ├─ if '\n' or '.' or '!' :   │
    │   │   └─ ttsBuffer complete    │
    │   │      EnqueueSpeech()        │
    │   │                             │
    │   └─ outputText.text =          │
    │       displayBuffer.ToString()  │
    │       (update UI in real-time)  │
    │                                 │
    │ RESULT:                         │
    │ "I would say my weakness is    │
    │  that I sometimes get caught    │
    │  up in details and miss the    │
    │  bigger picture. [META]        │
    │  weakness: attention to detail │
    │  [/META]"                       │
    └────────┬────────────────────────┘
             │


┌─────────────────────────────────────────────────────────────────────────┐
│ PHASE 6: MEMORY UPDATE                                                  │
└─────────────────────────────────────────────────────────────────────────┘

    ┌─────────────────────────────┐
    │ UpdateMemory()              │
    │                             │
    │ Extract [META] facts:       │
    │ - weakness: attention       │
    │ - strength: details         │
    │                             │
    │ Store in NPCMemory:         │
    │ - keyFacts list             │
    │ - conversationHistory       │
    │ - lastSpeech                │
    │                             │
    │ Next prompt will include    │
    │ these facts for consistency │
    └────────┬────────────────────┘
             │
             ▼
    ┌─────────────────────────────┐
    │ NPCMemory updated:          │
    │ Facts now remembered:       │
    │ 1. Weakness: details        │
    │ 2. Strength: precision      │
    │ 3. Works on self-awareness  │
    │                             │
    │ These persist for           │
    │ future responses             │
    └─────────────────────────────┘


┌─────────────────────────────────────────────────────────────────────────┐
│ PHASE 7: TEXT-TO-SPEECH (TTS) STREAMING                                 │
└─────────────────────────────────────────────────────────────────────────┘

    ┌───────────────────────────────────┐
    │ NPCTTSHandler.EnqueueSpeech()     │
    │                                   │
    │ Process on sentence chunks:       │
    │                                   │
    │ 1. "I would say my weakness"      │
    │    └─ Chunk → API call            │
    │                                   │
    │ 2. "is that I get caught up"      │
    │    └─ Chunk → API call            │
    │                                   │
    │ 3. "in details."                  │
    │    └─ Chunk → API call            │
    └───────┬────────────────────────────┘
             │
             │ Queue sentences for synthesis
             ▼
    ┌──────────────────────────────┐
    │ ProcessQueue()               │
    │ (background thread)          │
    │                              │
    │ FOR each sentence in queue:  │
    │   └─ Call TTS API            │
    │      (Edge TTS / etc)        │
    │      Generate audio          │
    │      Add to audioSource      │
    │                              │
    │ Audio streams to speaker     │
    └────────┬─────────────────────┘
             │ Audio playing
             ▼
    ┌──────────────────────────────┐
    │ AudioSource.PlayOneShot()    │
    │                              │
    │ ♪♫ User hears NPC voice     │
    │ "I would say my weakness     │
    │  is that I get caught up     │
    │  in details..."              │
    └────────┬─────────────────────┘


┌─────────────────────────────────────────────────────────────────────────┐
│ PHASE 8: TURN RELEASE & COORDINATION                                    │
└─────────────────────────────────────────────────────────────────────────┘

    ┌────────────────────────────────┐
    │ After TTS completes:           │
    │                                │
    │ 1. Wait for audio to finish    │
    │                                │
    │ 2. DialogueManager             │
    │    .ReleaseTurn()              │
    │    - currentSpeaker = ""       │
    │                                │
    │ 3. Check phase:                │
    │    - If INTRO: proceed to MAIN │
    │    - If MAIN: allow next turn  │
    │    - If conclusion threshold   │
    │      exceeded → CONCLUSION     │
    │                                │
    │ 4. Allow next NPC or user      │
    │    to speak                    │
    └────────┬───────────────────────┘
             │
             ▼
    ┌─────────────────────────────┐
    │ Interview continues...      │
    │                             │
    │ Ready for next question     │
    │ or NPC response             │
    └─────────────────────────────┘
```

## 2. Parallel NPC Coordination

```
            Timeline: Back-and-forth Interview

Time →     ┌──────┬──────┬──────┬──────┬──────┬──────┐
           │  T0  │  T1  │  T2  │  T3  │  T4  │  T5  │
           └──────┴──────┴──────┴──────┴──────┴──────┘

User:      [       User speaks input      ]  [  Waits  ]
            ↓                              ↑
            (Speech in)                    (Transcript)

Mgr:                    [    Turn Request    ]
                        ├─ Check speaker
                        ├─ Request granted
                        └─ GrantTurn(NPC_A)

NPC_A:                       [  Ollama Call   ]
                             ├─ System Prompt
                             ├─ Stream tokens
                             └─ Processing...

NPC_B:                       [    Waiting     ]
                             └─ blocked

ProcessTokens:               [   Real-time    ]
                             ├─ Display text
                             ├─ Buffer TTS
                             └─ Update memory

TTS:                                   [  Synthesize  ]
                                       ├─ Chunks → API
                                       └─ Stream audio

Audio:                                         [ Playing ]

NPC_A Release:                                    [Release]
                                                 └─ Free turn

NPC_B Available:                                    [Queued ]
                                                    └─ Ready


Result: Smooth alternation between NPCs with overlapping I/O


Memory Persistence:
┌────────────────────────────────────────────────────────────┐
│ NPC_A Memory                   │ NPC_B Memory              │
├──────────────────────────────────────────────────────────┤
│ Facts:                          │ Facts:                    │
│ - User is nervous about public  │ - User has weak public    │
│   speaking                      │   speaking                │
│ - Previous answer: shy type     │ - Previous answer: shy    │
│ - Asking follow-up next         │ - Will follow when A done │
│                                 │                            │
│ Last speech: "I see,            │ Last speech: <waiting>    │
│ public speaking is tough"       │                            │
└────────────────────────────────────────────────────────────┘
```

## 3. Memory Update Cycle

```
Response from LLM contains:
"I would say my weakness is [META]
weakness: public speaking,
confidence_level: medium,
emotion_detected: nervous
[/META] I'm working on it daily."

         │
         ▼
    Parse [META]
         │
         ├─ Extract: weakness: public speaking
         ├─ Extract: confidence_level: medium
         └─ Extract: emotion_detected: nervous
         │
         ▼
    Update NPCMemory:
         │
         ├─ keyFacts.Add("User weak at public speaking")
         ├─ keyFacts.Add("Confidence: medium")
         ├─ keyFacts.Add("Shows nervousness")
         │
         ├─ conversationHistory.Add(
         │    "NPC: Asked about weakness"
         │    "User: Public speaking nervous"
         │  )
         │
         └─ lastSpeech = <full response>
         │
         ▼
    Next System Prompt includes:
    
    "You know from previous conversation:
     - User weak at public speaking
     - Confidence is medium
     - Shows nervousness
     
     Build on this in your next response"
     
         │
         ▼
    NPC generates more relevant
    follow-up questions
```

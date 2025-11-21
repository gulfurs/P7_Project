# Research-Ready Documentation

## 1. System Overview for Academic Papers

```
TITLE: "An Interactive Job Interview Simulation System Using 
         Large Language Models and Real-Time Speech Processing"

┌─────────────────────────────────────────────────────────────┐
│ EXECUTIVE SUMMARY                                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ This system implements an autonomous multi-agent interview  │
│ simulation where two AI-driven interviewer agents conduct   │
│ realistic job interviews with human participants. The       │
│ system integrates:                                          │
│                                                             │
│ • Real-time speech-to-text (OpenAI Whisper)               │
│ • Large language model inference (Llama2 7B via Ollama)    │
│ • Multi-agent turn coordination                            │
│ • Persistent contextual memory per agent                   │
│ • Natural language synthesis (Edge TTS)                    │
│                                                             │
│ Key Innovation: The system enables natural back-and-forth  │
│ dialogue between TWO independent AI agents, creating a     │
│ realistic interview environment while maintaining          │
│ consistency and coherence through memory persistence       │
│ and turn management.                                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘

METRICS FOR EVALUATION:

1. Response Quality
   ├─ Coherence: Does NPC response relate to user input?
   ├─ Relevance: Is response job-interview appropriate?
   ├─ Consistency: Does NPC maintain personality?
   └─ Memory Accuracy: Does NPC recall previous facts?

2. System Performance
   ├─ End-to-end latency (question → response)
   ├─ STT accuracy (Whisper on 3-sec chunks)
   ├─ Turn coordination fairness (balanced speaking)
   └─ TTS latency (text → audio generation)

3. User Experience
   ├─ Naturalness of dialogue flow
   ├─ NPC reliability (crash-free runtime)
   ├─ Response believability (Likert scale survey)
   └─ Interview simulation realism

4. Replicability
   ├─ System runs on commodity hardware
   ├─ Open-source components (Ollama, Whisper)
   ├─ Deterministic behavior (seed control)
   └─ Configuration transparency (JSON configs)
```

## 2. Component Specifications for Journal

```
TABLE 1: CORE COMPONENTS SPECIFICATIONS

┌──────────────┬──────────────┬─────────────┬──────────────────┐
│ Component    │ Technology   │ Language    │ Primary Role     │
├──────────────┼──────────────┼─────────────┼──────────────────┤
│ NPCChatInst. │ Unity/C#     │ C#          │ Agent logic      │
│ DialogueManager│ Singleton   │ C#          │ Turn control     │
│ WhisperCont. │ Whisper DLL  │ C++/C#      │ Audio capture    │
│ OllamaChatC. │ HTTP Client  │ C#          │ LLM inference    │
│ NPCTTSHandler│ Edge TTS API │ C#/REST     │ Speech synthesis │
│ NPCMemory    │ In-memory    │ C#          │ Persistence      │
└──────────────┴──────────────┴─────────────┴──────────────────┘

TABLE 2: EXTERNAL DEPENDENCIES

┌──────────────────┬────────────┬───────────┬─────────────────┐
│ Dependency       │ Version    │ Usage     │ License         │
├──────────────────┼────────────┼───────────┼─────────────────┤
│ Ollama           │ 0.13+      │ LLM API   │ MIT             │
│ Llama2 7B Model  │ Latest     │ Inference │ Community       │
│ OpenAI Whisper   │ large      │ STT       │ MIT             │
│ Edge TTS         │ Latest     │ Synthesis │ MIT             │
│ TextMeshPro      │ 3.2+       │ UI        │ Unity EULA      │
│ Unity Engine     │ 6000.0.58+ │ Runtime   │ Commercial      │
└──────────────────┴────────────┴───────────┴─────────────────┘

TABLE 3: PERFORMANCE CHARACTERISTICS

┌──────────────────────┬─────────────┬──────────────────────┐
│ Metric               │ Value       │ Measured On          │
├──────────────────────┼─────────────┼──────────────────────┤
│ STT Latency          │ 2-4 sec     │ Whisper 3sec chunk   │
│ LLM Latency (7B)     │ 1-3 sec     │ GPU inference        │
│ TTS Latency          │ 1-2 sec     │ Per 50-char chunk    │
│ E2E Response Time    │ 5-10 sec    │ Full pipeline        │
│ Turn Switch Time     │ 50-100ms    │ Manager + Start      │
│ Memory Overhead      │ 5-10 MB     │ Per NPC + history    │
│ Concurrent Sessions  │ 1-2         │ On single GPU        │
└──────────────────────┴─────────────┴──────────────────────┘
```

## 3. Algorithm Specifications

```
ALGORITHM 1: Turn Coordination Protocol

Precondition: 
  - 2+ NPCs registered with DialogueManager
  - currentSpeaker == null or ""

Procedure TakeTurn(npcName, userText):
  1. ACQUIRE global_speaker_lock
  2. IF currentSpeaker != null THEN
       RETURN false  // Turn denied
     END IF
  3. currentSpeaker ← npcName
  4. speakerHistory.Add(npcName)
  5. totalTurns ← totalTurns + 1
  6. RELEASE global_speaker_lock
  
  7. systemPrompt ← BuildSystemPrompt(
       npcProfile.basePrompt,
       memory.GetContext(),
       phase,
       speakerHistory.GetLast(3)
     )
  
  8. messages ← [
       {role: "system", content: systemPrompt},
       {role: "user", content: userText}
     ]
  
  9. response ← await OllamaChatClient.SendChatAsync(
       messages,
       temperature=0.7,
       repeatPenalty=1.1
     )
  
  10. ProcessResponse(response)
      10.1 FOREACH token in response STREAM:
           - Buffer for display
           - Check for [META]...[/META]
           - Accumulate TTS chunks
           - Update UI real-time
      10.2 UpdateMemory(ExtractMetadata(response))
      10.3 EnqueueTTS(SplitSentences(response))
      10.4 WAIT FOR TTS.IsPlaying() = false
  
  11. RELEASE turn
  12. RETURN true

Postcondition:
  - NPC response generated and played
  - Memory updated with new facts
  - Turn released for next speaker
```

## 4. Data Collection Protocol

```
RESEARCH PROTOCOL: Interview Session Logging

Session Data Collected:

1. Per-Turn Metrics
   ├─ Timestamp (ISO 8601)
   ├─ Speaker ID (NPC_A | NPC_B | User)
   ├─ Input text (user question)
   ├─ LLM response (full text)
   ├─ Response tokens (count)
   ├─ Token generation time (ms)
   ├─ Metadata extracted (JSON)
   └─ Processing latencies (all stages)

2. Interview-Level Metrics
   ├─ Total duration (minutes)
   ├─ Turn count (N)
   ├─ Phase transitions (Intro → Main → Conclusion)
   ├─ Error events (count + type)
   ├─ Memory updates (facts stored)
   └─ Turn fairness (NPC_A % vs NPC_B %)

3. System Health
   ├─ Memory usage (MB over time)
   ├─ CPU usage (%)
   ├─ GPU usage (%)
   ├─ Network latency (ms)
   ├─ Error counts (type, frequency)
   └─ Recovery success rate

4. User Feedback (Post-Interview Survey)
   ├─ Realism (1-5 Likert)
   ├─ Engagement (1-5)
   ├─ Coherence of NPC responses (1-5)
   ├─ Natural conversation flow (1-5)
   ├─ Technical issues encountered (Y/N)
   └─ Overall satisfaction (1-5)

Output Format: JSON Lines (newline-delimited JSON)
Sample:
{
  "timestamp": "2025-11-21T14:32:00Z",
  "turn": 5,
  "speaker": "NPC_A",
  "user_input": "I'm good with Python but weak in C++",
  "response": "That's a valuable insight...[truncated]",
  "tokens": 87,
  "generation_ms": 2340,
  "memory_facts": ["language_strength: Python", "growth_area: C++"],
  "phase": "MAIN"
}
```

## 5. Reproducibility Documentation

```
REPRODUCIBILITY CHECKLIST

Hardware Requirements:
☐ GPU: NVIDIA RTX 3060 or better (8GB+ VRAM)
☐ CPU: Intel i7/i9 or AMD Ryzen 7/9
☐ RAM: 16GB minimum, 32GB recommended
☐ Storage: 200GB free (models + data)
☐ Microphone: USB or built-in (16kHz capable)

Software Requirements:
☐ Windows 10/11 64-bit or Linux
☐ Unity 6000.0.58f1 (exact version)
☐ NVIDIA CUDA Toolkit 12.1
☐ Ollama 0.13.0+
☐ Python 3.10+ (for model setup)

Setup Instructions:
1. Install Ollama
   ollama pull llama2:7b

2. Configure LLMConfig in Unity
   - ollamaEndpoint: http://localhost:11434/api/chat
   - temperature: 0.7
   - repeatPenalty: 1.1

3. Download Whisper model
   - Place ggml-tiny.bin in StreamingAssets/Whisper/

4. Configure NPC Profiles
   - System prompts in Assets/Resources/NPCProfiles/

5. Run interview simulation
   - Open Scenes/InterviewScene.unity
   - Press Play in Unity Editor
   - Follow on-screen prompts

Verification:
☐ Ollama running on localhost:11434
☐ STT producing transcripts
☐ NPCs responding to user input
☐ No crashes after 30+ turns
☐ Memory stable (not growing unbounded)

Expected Results:
- 2 NPC agents conduct realistic interview
- Natural dialogue flow with turn-taking
- Responses coherent and contextually relevant
- No system crashes in 1-hour session
- Latency <10 sec per response
```

## 6. Validation & Benchmarking

```
VALIDATION FRAMEWORK

Test Suite 1: Functional Correctness
├─ Test: STT accuracy
│  └─ Measure: WER (Word Error Rate) on test set
│
├─ Test: LLM response relevance
│  └─ Measure: BLEU/ROUGE scores vs human responses
│
├─ Test: Memory persistence
│  └─ Measure: Fact accuracy in subsequent turns
│
├─ Test: Turn coordination
│  └─ Measure: No simultaneous speech, fairness ratio
│
└─ Test: TTS naturalness
   └─ Measure: MOS (Mean Opinion Score, 1-5)


Test Suite 2: Performance Benchmarking
├─ Latency measurements (all components)
├─ Throughput (turns per minute)
├─ Resource utilization (CPU/GPU/RAM)
├─ Scalability (1 vs 2 vs N NPCs)
└─ Stress testing (100+ consecutive turns)


Test Suite 3: Robustness Testing
├─ Network failure recovery
├─ Model file corruption handling
├─ Microphone disconnection
├─ LLM timeout scenarios
├─ Memory exhaustion (bounded)
└─ Long-running stability (8+ hours)


Test Suite 4: User Experience
├─ Heuristic evaluation (Nielsen 10 rules)
├─ Cognitive load assessment
├─ Error message clarity
├─ Recovery time after failure
└─ Learning curve (tutorials effectiveness)
```

## 7. Ethical Considerations

```
ETHICAL FRAMEWORK FOR JOB INTERVIEW SIMULATION

1. Bias & Fairness
   ├─ LLM bias detection in responses
   ├─ Equal treatment across demographics
   ├─ Monitoring for discriminatory patterns
   └─ Mitigation strategies documented

2. Consent & Privacy
   ├─ Clear disclosure: Interacting with AI
   ├─ Audio recording consent obtained
   ├─ Data retention policy transparent
   ├─ Right to withdraw at any time
   └─ Data encryption during transmission

3. Autonomy & Transparency
   ├─ Users informed of system limitations
   ├─ No deceptive practices
   ├─ Model capabilities clearly stated
   ├─ Uncertainty communicated
   └─ Human alternative always available

4. Safety & Well-being
   ├─ No psychologically harmful content
   ├─ Support resources provided
   ├─ Escalation procedures documented
   ├─ Feedback mechanisms implemented
   └─ User well-being prioritized

5. Data Governance
   ├─ Data minimization principle applied
   ├─ Purpose limitation enforced
   ├─ Data deletion protocols
   ├─ GDPR/similar compliance
   └─ Ethics review completed

Approval Status:
☐ IRB (Institutional Review Board) approval obtained
☐ Ethics committee sign-off
☐ Data protection officer consultation
☐ Legal review completed
```

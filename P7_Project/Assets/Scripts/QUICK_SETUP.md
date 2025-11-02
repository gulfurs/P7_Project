# Quick Setup Guide - Local Interview Simulation

## 🎯 Goal
Set up job interview simulation with 2 NPC interviewers using **local GGUF models** (Qwen2.5/Qwen3).

---

## 📦 Prerequisites

1. **GGUF Model File**
   - Download Qwen2.5-8B-Instruct (q4_K_M or q5_K_M)
   - Or use Qwen3 (llama2889) in GGUF format
   - Place in: `Assets/StreamingAssets/models/`

2. **Native DLL**
   - Ensure `llama_unity.dll` is in your Unity project
   - Check Plugins folder or StreamingAssets

---

## 🚀 Setup Steps

### Step 1: Create LLMConfig Asset

1. In Unity Project window:
   - Right-click in any folder
   - `Create > LLM > Configuration`
   - Name it **"LLMConfig"**

2. **Move it to Resources folder**:
   - Create `Assets/Resources/` if it doesn't exist
   - Drag `LLMConfig` into `Resources/`

3. **Configure Settings**:
   ```
   Model Path: Assets/StreamingAssets/models/qwen2-8b-instruct-q4_K_M.gguf
   Model Name: Qwen2.5-8B-Instruct
   Temperature: 0.7
   Repeat Penalty: 1.1
   Max Tokens: 128
   Context Size: 4096
   ```

4. **Review Core System Prompt**:
   - Open `coreSystemPrompt` text area
   - Verify interview instructions are correct
   - Note the metadata format: `[META]{...}[/META]`

5. **Validate**:
   - Right-click on LLMConfig asset
   - Select "Validate Configuration"
   - Check Console for any errors

---

### Step 2: Setup Scene Managers

Create empty GameObjects for singletons:

1. **LlamaBridge**
   - Create empty GameObject, name it "LlamaBridge"
   - Add Component: `LlamaBridge`
   - Leave `modelPath` empty (will auto-load from LLMConfig)

2. **LlamaMemory**
   - Create empty GameObject, name it "LlamaMemory"
   - Add Component: `LlamaMemory`

3. **DialogueManager**
   - Create empty GameObject, name it "DialogueManager"
   - Add Component: `DialogueManager`

4. **NPCManager**
   - Create empty GameObject, name it "NPCManager"
   - Add Component: `NPCManager`
   - Assign `userTransform` (camera or player object)

---

### Step 3: Create Interviewer #1

1. **Create NPC GameObject**
   - Name it "Interviewer_Sarah"
   - Position in scene

2. **Add NPCChatInstance Component**

3. **Configure NPCProfile**:
   ```
   NPC Name: Dr. Sarah Chen
   Role: Senior Technical Interviewer specializing in backend systems
   Expertise: Distributed systems, databases, API design, scalability
   Personality Traits: Direct and analytical, asks probing follow-up questions
   
   Temperature: 0.7
   Repeat Penalty: 1.1
   
   Enable TTS: ✓
   Voice Name: en_US-lessac-medium
   ```

4. **Setup Animator Config** (if using animations):
   - Create `NPCAnimatorConfig` in NPCProfile
   - Assign animator
   - Add available triggers: nod, shake_head, lean_forward, smile, etc.
   - Set gaze origin transform (head bone)
   - Set neutral look target

5. **Assign References**:
   - `llamaBridge`: Drag LlamaBridge GameObject
   - Leave `llamaMemory` empty (auto-finds singleton)
   - Add UI elements if using:
     - `userInput`: TMP_InputField for user's answer
     - `outputText`: TMP_Text for NPC's question
     - `npcNameLabel`: TMP_Text for NPC name

---

### Step 4: Create Interviewer #2

Repeat Step 3 with different profile:

```
NPC Name: Marcus Thompson
Role: Senior Product Manager focusing on user experience
Expertise: Product strategy, user research, feature prioritization
Personality Traits: Collaborative and empathetic, focuses on user impact
```

**Important**: Use different personality to create dynamic conversation!

---

### Step 5: Test

1. **Play Mode**
   - Check Console for initialization messages:
     ```
     [LLMConfig] ✓ Configuration is valid! Model: Qwen2.5-8B-Instruct (...)
     [LlamaBridge] ✓ Local model loaded successfully!
     [LlamaManager] ✓ Ready for local inference
     [NPCChat] Registered Dr. Sarah Chen with full system prompt
     [NPCChat] Registered Marcus Thompson with full system prompt
     ```

2. **First Test Question**
   - Type in user input: "I have 5 years of experience in web development"
   - Press Enter
   - Watch Console logs for decision-making:
     ```
     👤 User answered: "I have 5 years of experience..."
     🤖 Dr. Sarah Chen decision: YES → RESPOND
     🎤 Dr. Sarah Chen granted turn (#1)
     ```

3. **Verify Response**
   - NPC should generate question with metadata
   - Check metadata extraction works (animations trigger)
   - Verify TTS plays if enabled

4. **Test Turn-Taking**
   - Answer the follow-up question
   - Both NPCs should decide whether to respond
   - Only one should speak at a time

---

## 🐛 Troubleshooting

### Model Won't Load
```
[LlamaBridge] Failed to initialize model. Check model path and format.
```
**Fix**: 
- Verify GGUF file exists at specified path
- Check file isn't corrupted (try re-downloading)
- Ensure DLL is compatible with model

### NPCs Not Responding
```
⏸️ Dr. Sarah Chen blocked - Marcus Thompson is speaking
```
**Fix**: Wait for current speaker to finish, or check for stuck state

### No Metadata in Response
```
[NPCChat] Response missing metadata block for Dr. Sarah Chen. Using defaults.
```
**Fix**: 
- Check `coreSystemPrompt` in LLMConfig has metadata instructions
- May need to adjust temperature (higher = more creative)
- Try regenerating response

### Animations Not Playing
**Fix**:
- Verify `animatorConfig` is assigned in NPCProfile
- Check animator has the trigger parameters defined
- Ensure trigger names match (case-sensitive)

---

## 🎨 Customization

### Change Interview Style
Edit `coreSystemPrompt` in LLMConfig:
- Make it more formal/casual
- Add specific evaluation criteria
- Change metadata format
- Add new non-verbal actions

### Add New Animator Actions
In NPCProfile > Animator Config:
1. Add to `availableTriggers` list
2. Update `coreSystemPrompt` to mention new action
3. Define trigger in Animator Controller

### Adjust Response Length
In LLMConfig:
- `defaultMaxTokens`: 64 (short), 128 (medium), 256 (long)
- Note: Longer = slower inference

### Change Model
1. Download different GGUF model
2. Update `modelPath` in LLMConfig
3. Adjust `contextSize` if needed
4. May need to tune temperature/penalties

---

## 📊 Performance Tips

1. **Use Quantized Models**
   - q4_K_M: Fast, good quality
   - q5_K_M: Slower, better quality
   - q8_0: Very slow, best quality

2. **Reduce Context**
   - Use `GetShortTermContext(2)` for only recent turns
   - Helps with speed and memory

3. **Limit Max Tokens**
   - 64 tokens = ~2-3 sentences
   - 128 tokens = ~4-6 sentences (recommended)

4. **GPU Acceleration**
   - Ensure DLL supports GPU offloading
   - Check Unity Compute Shaders if available

---

## ✅ Checklist

Before launching interview:

- [ ] LLMConfig created and in Resources/
- [ ] GGUF model file exists at specified path
- [ ] LlamaBridge initialized successfully
- [ ] Both NPCs registered in NPCManager
- [ ] DialogueManager handling turn-taking
- [ ] User input connected
- [ ] NPC output displays correctly
- [ ] Animations/gaze working (if used)
- [ ] TTS playing (if enabled)

---

## 🎓 Next Steps

Once basic setup works:

1. **Fine-tune prompts** for better questions
2. **Add more NPCs** for panel interviews
3. **Create scoring system** based on responses
4. **Record interviews** for playback/analysis
5. **Add interview stages** (technical, behavioral, etc.)

---

## 📚 Full Documentation

See `README.md` for complete architecture details and advanced features.

---

**You're ready to conduct AI-powered job interviews with local LLMs! 🚀**

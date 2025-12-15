# P7_Project

## 🚀 Features  

✨ **Embodied AI Agents** – Real-time 3D avatars.  
🧩 **LLM-Driven Dialogue** – Back-and-forth conversation powered language models.   
🔄 **Multi-Party Simulation** –   

## 🧰 Tech Stack  

| Component | Description | Link |
|------------|--------------|------|
| 🧠 **LLM Server** | Backend for conversation logic | [Ollama](https://ollama.ai/) |
| 💬 **LLM Model** | Model used for agent reasoning and dialogue | ` qwen3:4b` |
| 🔊 **Text-to-Speech (TTS)** | Converts agent responses to voice | [TTS Documentation](https://github.com/OHF-Voice/piper1-gpl/) |
| 🗣️ **Speech-to-Text (STT)** | Captures user input from voice | [Whisper](https://github.com/openai/whisper) |
| 🕹️ **3D Environment** | Embodied simulation and visualization | Unity `6000.0.58f1` (LTS) |

---
## 💻 Hardware Requirements

| Tier | CPU | RAM | GPU |
|------|-----|-----|-----|
| **Minimum** | 4-core/8-thread x64 | 16 GB | NVIDIA RTX 20-series (6GB VRAM) |
| **Recommended** | 8-core/16-thread (Ryzen 7/i7) | 32 GB | NVIDIA RTX 30/40-series (8-12GB VRAM) |
| **Best** | Ryzen 7 7800X3D | 64 GB DDR5 | RTX 50-series (Blackwell) |

> *Requires NVIDIA drivers with CUDA 13.x support*

## 🧬 System Architecture  

---

## ⚡ How to Run - 1-Minute Quickstart

**Prerequisites**: Windows PC with NVIDIA GPU, microphone, and headset/speakers

1. **Start LLM Server** (Terminal 1)
   ```bash
   ollama serve
   ```
   ✅ *Expected: "Ollama is running"*

2. **Pull AI Model** (Terminal 2)
   ```bash
   ollama pull qwen3:4b-instruct-25-07-q4_K_M
   ```
   ✅ *Expected: Model download progress → "success"*

3. **Open Unity Project**
   - Launch Unity Hub → Open `P7_Project` folder
   - Wait for project compilation (1-3 minutes)
   - Press **Play ▶️** in Unity Editor

4. **Interact with Agent**
   - Click in Game View to focus
   - Start talking

🎯 **Quick Test**: Say *"Hello, how are you?"* → Agent responds with voice & animation

---

### 📋 Dependencies

| Software | Version | Download |
|----------|---------|----------|
| **Unity** | `6000.0.58f1` (LTS) | [Unity Hub](https://unity.com/download) |
| **Ollama** | Latest | [ollama.ai](https://ollama.ai/download) |
| **NVIDIA Drivers** | CUDA 13.x compatible | [GeForce Drivers](https://www.nvidia.com/Download/index.aspx) |
| **Python** (for Whisper STT) | 3.10+ | [python.org](https://www.python.org/downloads/) |
| **piper-tts** | Latest | `pip install piper-tts` |

```powershell
# Terminal 1: Start Ollama server
ollama serve

ollama run qwen3:4b-instruct-25-07-q4_K_M "Test message"

```


**Configure Audio Devices**
   - Ensure microphone is set as default recording device
   - Connect headset/speakers for TTS output

### 🐛 Troubleshooting

| Issue | Solution |
|-------|----------|
| Ollama not found | Open or Restart terminal after installation |
| No microphone input | Check Windows privacy settings → Allow app access |
| Agent not speaking | Verify audio output device in Windows Sound settings |
| TTS not working | Install piper-tts: `pip install piper-tts` |

---

## 📸 Demo
![Gameplay GIF](https://media.giphy.com/media/26AHONQ79FdWZhAI0/giphy.gif)  

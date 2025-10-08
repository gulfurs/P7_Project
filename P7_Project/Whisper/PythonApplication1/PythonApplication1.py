# whisper -> llama local bridge

import requests
from faster_whisper import WhisperModel

# Whisper
model = WhisperModel("base", device="cpu")
segments, _ = model.transcribe("harvard.wav")
text = " ".join([s.text for s in segments])
print("You said:", text)

# Llama (Ollama local API)
response = requests.post(
    "http://localhost:11434/api/generate",
    json={"model":"llama3","prompt":text}
)
reply = response.json()["response"]
print("Llama says:", reply)

import sys
import warnings


import subprocess
import sounddevice as sd
import numpy as np
import wavio
from faster_whisper import WhisperModel
import os

# -----------------------------
# Config
# -----------------------------
MODEL_NAME = "tiny"  # Whisper model
SAMPLERATE = 16000   # Sampling rate for recording
DURATION = 5         # Seconds to record per input
OLLAMA_MODEL = "llama3"  # Ollama model


sys.stderr = open(os.devnull, "w")
sys.stderr = sys.__stderr__
warnings.filterwarnings("ignore")


# -----------------------------
# Record audio from microphone
# -----------------------------
def record_audio(filename="input.wav", duration=DURATION, samplerate=SAMPLERATE):
    print(f"Recording {duration} seconds of audio...")
    audio = sd.rec(int(duration * samplerate), samplerate=samplerate, channels=1)
    sd.wait()  # Wait until recording is finished
    audio = np.squeeze(audio)
    wavio.write(filename, audio, samplerate, sampwidth=2)
    print(f"Audio saved to {filename}")
    return filename


# -----------------------------
# Transcribe with Whisper
# -----------------------------
def transcribe_whisper(audio_file):
    print("Transcribing audio with Whisper...")
    model = WhisperModel(MODEL_NAME, device="cpu")  # CPU works for AMD on Windows
    segments, _ = model.transcribe(audio_file)
    text = " ".join([s.text for s in segments])
    print(f"You said: {text}")
    return text

# -----------------------------
# Query Llama via Ollama subprocess
# -----------------------------
def query_llama(prompt):
    print("Sending to Llama...")
    try:
        result = subprocess.run(
            ["ollama", "run", OLLAMA_MODEL, "--prompt", prompt],
            capture_output=True,
            text=True
        )
        response = result.stdout.strip()
        print(f"Llama says: {response}")
        return response
    except Exception as e:
        print("Error calling Ollama:", e)
        return ""

# -----------------------------
# Main loop
# -----------------------------
if __name__ == "__main__":
    while True:
        audio_file = record_audio()
        transcription = transcribe_whisper(audio_file)
        if transcription.lower() in ["exit", "quit"]:
            print("Exiting...")
            break
        query_llama(transcription)

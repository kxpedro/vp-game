"""Gera os efeitos sonoros do Revolver Duplo (CC0, sintetizados; sem amostras de terceiros).

Uso: py tools/generate_revolver_sfx.py  ->  audio/effects/combat/Revolver *.wav
Sementes fixas: rodar de novo produz os mesmos arquivos.
"""
from pathlib import Path
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "audio/effects/combat"
RATE = 44100


def lowpass(signal: np.ndarray, alpha: float) -> np.ndarray:
    out = np.empty_like(signal)
    acc = 0.0
    for i, x in enumerate(signal):
        acc += alpha * (x - acc)
        out[i] = acc
    return out


def shot(seed: int, duration: float, body_hz: float, decay: float, brightness: float) -> np.ndarray:
    rng = np.random.default_rng(seed)
    t = np.arange(int(RATE * duration)) / RATE
    noise = rng.uniform(-1.0, 1.0, t.size)
    crack = lowpass(noise, brightness) * np.exp(-t / decay)
    tail = lowpass(noise, brightness * 0.25) * np.exp(-t / (decay * 4.0)) * 0.45
    thump = np.sin(2 * np.pi * body_hz * t * (1.0 - 0.4 * t / duration)) * np.exp(-t / (decay * 2.0)) * 0.9
    attack = np.minimum(t / 0.0015, 1.0)
    return (crack + tail + thump) * attack


def click(rng: np.random.Generator, length: float, tone_hz: float) -> np.ndarray:
    t = np.arange(int(RATE * length)) / RATE
    noise = rng.uniform(-1.0, 1.0, t.size)
    high = noise - lowpass(noise, 0.2)
    return (high * 0.7 + np.sin(2 * np.pi * tone_hz * t) * 0.5) * np.exp(-t / 0.006)


def reload_sound(seed: int) -> np.ndarray:
    rng = np.random.default_rng(seed)
    out = np.zeros(int(RATE * 0.55))
    events = [(0.00, 0.03, 2400.0, 0.8)]
    events += [(0.10 + i * 0.035, 0.02, 3200.0 + i * 120.0, 0.35) for i in range(6)]
    events += [(0.40, 0.04, 1800.0, 1.0)]
    for start, length, tone, gain in events:
        c = click(rng, length, tone) * gain
        i = int(start * RATE)
        out[i:i + c.size] += c[: out.size - i]
    return out


def write(name: str, signal: np.ndarray, peak: float = 0.89) -> None:
    signal = signal / max(np.max(np.abs(signal)), 1e-9) * peak
    data = (signal * 32767.0).astype("<i2").tobytes()
    with wave.open(str(OUT / name), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(data)
    print("ok:", OUT / name)


if __name__ == "__main__":
    write("Revolver Shot 1.wav", shot(11, 0.35, 110.0, 0.035, 0.55))
    write("Revolver Shot 2.wav", shot(23, 0.35, 96.0, 0.040, 0.50))
    write("Revolver Shot Heavy.wav", shot(37, 0.80, 62.0, 0.070, 0.42))
    write("Revolver Reload.wav", reload_sound(5), peak=0.7)

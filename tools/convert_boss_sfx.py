"""Converte os efeitos arcanos do pacote **Magic Attacks** (WAV float 32 bits, estéreo) para os sons da
Guardiã Invisível: WAV PCM 16 bits mono, metade da taxa de amostragem e cauda silenciosa cortada (arquivos ~8x
menores, o Godot importa direto). Sem ffmpeg: só numpy.

    py tools/convert_boss_sfx.py [pasta do pacote]

Pasta padrão: F:/ASSETS/Magic Attacks/Magic Attacks (PC do Pedro). Saída: audio/effects/boss/invisible_woman/.
Origem e créditos em audio/effects/boss/invisible_woman/SOURCE.md.
"""
import pathlib
import struct
import sys

import numpy as np

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / "audio" / "effects" / "boss" / "invisible_woman"
DEFAULT_PACK = pathlib.Path("F:/ASSETS/Magic Attacks/Magic Attacks")

# saída: arquivo do pacote (relativo à pasta Arcane), duração máxima em segundos
SOUNDS = {
    "cast_1.wav": ("Attack/Arcane_AttackF1.wav", 1.6),
    "cast_2.wav": ("Attack/Arcane_AttackF2.wav", 1.6),
    "impact_1.wav": ("Impact/Sizzle_Impact1.wav", 1.0),
    "impact_2.wav": ("Impact/Sizzle_Impact2.wav", 1.0),
    "impact_3.wav": ("Impact/Sizzle_Impact3.wav", 1.0),
    "vanish_1.wav": ("Impact/Shine_Impact1.wav", 1.8),
    "vanish_2.wav": ("Impact/Shine_Impact2.wav", 1.8),
}


def read_wav(path):
    data = path.read_bytes()
    assert data[:4] == b"RIFF" and data[8:12] == b"WAVE", path
    pos, fmt, samples = 12, None, None
    while pos + 8 <= len(data):
        chunk, size = data[pos:pos + 4], struct.unpack("<I", data[pos + 4:pos + 8])[0]
        body = data[pos + 8:pos + 8 + size]
        if chunk == b"fmt ":
            fmt = struct.unpack("<HHIIHH", body[:16])
        elif chunk == b"data":
            samples = body
        pos += 8 + size + (size & 1)
    tag, channels, rate, _, _, bits = fmt
    if tag == 3 and bits == 32:
        audio = np.frombuffer(samples, "<f4").astype(np.float64)
    elif tag == 1 and bits == 16:
        audio = np.frombuffer(samples, "<i2").astype(np.float64) / 32768.0
    elif tag == 1 and bits == 24:
        raw = np.frombuffer(samples, np.uint8).reshape(-1, 3)
        ints = (raw[:, 0].astype(np.int32) | (raw[:, 1].astype(np.int32) << 8) | (raw[:, 2].astype(np.int32) << 16))
        audio = np.where(ints >= 1 << 23, ints - (1 << 24), ints) / float(1 << 23)
    else:
        raise ValueError(f"{path}: formato {tag}/{bits} bits não suportado")
    return audio.reshape(-1, channels), rate


def write_wav(path, mono, rate):
    pcm = (np.clip(mono, -1.0, 1.0) * 32767.0).astype("<i2").tobytes()
    header = struct.pack("<4sI4s4sIHHIIHH4sI", b"RIFF", 36 + len(pcm), b"WAVE", b"fmt ", 16, 1, 1, rate, rate * 2, 2, 16,
                         b"data", len(pcm))
    path.write_bytes(header + pcm)


def convert(source, target, max_seconds):
    audio, rate = read_wav(source)
    mono = audio.mean(axis=1)
    # Metade da taxa (média de pares: filtro passa-baixa simples antes de dizimar).
    if rate >= 44100:
        mono = mono[: len(mono) // 2 * 2].reshape(-1, 2).mean(axis=1)
        rate //= 2
    mono = mono[: int(max_seconds * rate)]
    # Corta a cauda abaixo de -50 dB e faz um fade de 40 ms no fim.
    loud = np.nonzero(np.abs(mono) > 10 ** (-50 / 20))[0]
    if len(loud):
        mono = mono[: loud[-1] + 1]
    fade = min(len(mono), int(0.04 * rate))
    mono[-fade:] *= np.linspace(1.0, 0.0, fade)
    peak = np.abs(mono).max()
    if peak > 0:
        mono *= 0.89 / peak
    write_wav(target, mono, rate)
    return len(mono) / rate


def main():
    pack = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_PACK
    OUT.mkdir(parents=True, exist_ok=True)
    for name, (relative, max_seconds) in SOUNDS.items():
        seconds = convert(pack / "Arcane" / relative, OUT / name, max_seconds)
        print(f"{name}: {seconds:.2f} s, {(OUT / name).stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()

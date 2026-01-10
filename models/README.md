# Models

Denna mapp innehåller Whisper-modeller i GGML-format.

## Installation

1. Ladda ner en KB-Whisper modell från [KBLab](https://huggingface.co/KBLab)
2. Placera modellen i en undermapp, t.ex. `kb-whisper-base/ggml-model-q5_0.bin`
3. Uppdatera `appsettings.json` med rätt sökväg till modellen

## Exempelstruktur

```
models/
  kb-whisper-base/
    ggml-model-q5_0.bin
  kb-whisper-tiny/
    ggml-model-q5_0.bin
```

**OBS:** Modellfiler (.bin, .gguf, .ggml) ignoreras av git eftersom de är för stora.

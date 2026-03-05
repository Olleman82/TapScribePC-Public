using WsprPc.Models;

namespace WsprPc.Services.Ai.Local;

public static class LocalAiCatalog
{
    public static IReadOnlyList<LocalAiModelPreset> CreateDefaultPresets()
    {
        return
        [
            new LocalAiModelPreset(
                "qwen3.5-0.8b-q4_k_m",
                "Qwen 3.5 0.8B (Snabb)",
                "https://huggingface.co/AaryanK/Qwen3.5-0.8B-GGUF/resolve/main/Qwen3.5-0.8B.q4_k_m.gguf?download=true",
                "Qwen3.5-0.8B.q4_k_m.gguf",
                "qwen3.5-0.8b",
                "Mycket snabb • Lägre kvalitet"),
            new LocalAiModelPreset(
                "qwen3.5-2b-q4_k_m",
                "Qwen 3.5 2B (Standard)",
                "https://huggingface.co/AaryanK/Qwen3.5-2B-GGUF/resolve/main/Qwen3.5-2B.q4_k_m.gguf?download=true",
                "Qwen3.5-2B.q4_k_m.gguf",
                "qwen3.5-2b",
                "Bra balans • Rekommenderas"),
            new LocalAiModelPreset(
                "qwen3.5-4b-q4_k_m",
                "Qwen 3.5 4B (Noggrann)",
                "https://huggingface.co/AaryanK/Qwen3.5-4B-GGUF/resolve/main/Qwen3.5-4B.q4_k_m.gguf?download=true",
                "Qwen3.5-4B.q4_k_m.gguf",
                "qwen3.5-4b",
                "Hög kvalitet • Tyngre modell"),
            new LocalAiModelPreset(
                "qwen3.5-9b-q4_k_m",
                "Qwen 3.5 9B (Max)",
                "https://huggingface.co/AaryanK/Qwen3.5-9B-GGUF/resolve/main/Qwen3.5-9B.q4_k_m.gguf?download=true",
                "Qwen3.5-9B.q4_k_m.gguf",
                "qwen3.5-9b",
                "Bäst kvalitet • Kräver mycket RAM/CPU")
        ];
    }
}

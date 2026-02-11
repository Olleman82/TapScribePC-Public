---
title: "Spårbarhet mot kod"
summary: "Visar vilka kodfiler som stöder varje större användarfunktion i TapScribe."
audience: "Slutanvändare, support och releaseansvariga"
feature_area: "Spårbarhet"
keywords:
  - "spårbarhet"
  - "funktioner"
  - "källfiler"
  - "verifiering"
last_verified_from_code: "2026-02-09"
source_refs:
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/PromptEditorWindow.xaml.cs"
  - "WsprPc/BatchQueueWindow.xaml.cs"
  - "WsprPc/HistoryWindow.xaml.cs"
  - "WsprPc/AppConfig.cs"
  - "WsprPc/Services/DictationController.cs"
  - "WsprPc/Services/AudioCaptureService.cs"
  - "WsprPc/Services/WhisperNetEngine.cs"
  - "WsprPc/Services/AutoTuneService.cs"
  - "WsprPc/Services/Vad/VadChunker.cs"
  - "WsprPc/Services/Diarization/FileTranscriptionService.cs"
  - "WsprPc/Services/Ai/GeminiClient.cs"
  - "WsprPc/Services/Ai/OpenAiClient.cs"
---

# Spårbarhet mot kod

Tabellen nedan hjälper dig att se var i appens kod en användarfunktion har sitt stöd. Den är tänkt för kontroll och trygghet vid uppdateringar.

| Användarfunktion | Var användaren möter funktionen | Källfiler i `WsprPc` |
|---|---|---|
| Start, status och huvudknappar | Huvudfönstret där du startar och styr flödet | `WsprPc/MainWindow.xaml.cs` |
| Snabb diktering med tangent | Start/stop av inspelning och infogning av text | `WsprPc/MainWindow.xaml.cs`, `WsprPc/Services/DictationController.cs`, `WsprPc/Services/AudioCaptureService.cs` |
| Transkribering till text | Omvandling av tal till färdig text | `WsprPc/Services/WhisperNetEngine.cs`, `WsprPc/Services/DictationController.cs` |
| Tystnadsdetektion (VAD) | Smart segmentering så appen jobbar lugnare och snabbare | `WsprPc/Services/Vad/VadChunker.cs`, `WsprPc/Services/DictationController.cs`, `WsprPc/MainWindow.xaml.cs` |
| Prompt-redigering för AI | Fönster för att skapa och ändra AI-prompter | `WsprPc/PromptEditorWindow.xaml.cs`, `WsprPc/MainWindow.xaml.cs` |
| AI-polering och modellval | Val av AI-leverantör och efterbearbetning | `WsprPc/Services/Ai/GeminiClient.cs`, `WsprPc/Services/Ai/OpenAiClient.cs`, `WsprPc/MainWindow.xaml.cs` |
| Mötestranskribering från fil | Filval, start och resultat för möten | `WsprPc/MainWindow.xaml.cs`, `WsprPc/Services/Diarization/FileTranscriptionService.cs` |
| Batchkö för flera filer | Köa, köra och följa flera filer samtidigt | `WsprPc/BatchQueueWindow.xaml.cs`, `WsprPc/Services/Diarization/FileTranscriptionService.cs` |
| Historik, sök och rensning | Visa tidigare resultat och hantera poster | `WsprPc/HistoryWindow.xaml.cs`, `WsprPc/Stores/HistoryStore.cs`, `WsprPc/MainWindow.xaml.cs` |
| Inställningar och sparade val | Permanent lagring av användarval mellan starter | `WsprPc/AppConfig.cs`, `WsprPc/MainWindow.xaml.cs` |
| Autotune av prestanda | Anpassning till datorns kapacitet | `WsprPc/Services/AutoTuneService.cs`, `WsprPc/MainWindow.xaml.cs` |

## När tabellen ska uppdateras

- När en användarfunktion flyttas till andra filer.
- När en ny större funktion läggs till i appen.
- Innan release, samtidigt som övrig användardokumentation verifieras.

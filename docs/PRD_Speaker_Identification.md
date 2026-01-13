# PRD: Speaker Identification (Diarization) Feature

> **Status**: DRAFT  
> **Target Branch**: `feature/speaker-id`  
> **Author**: Antigravity

---

## 0. Pre-Requisites: Branching & Rollback Safety

> [!CAUTION]
> **EXECUTE THIS SECTION FIRST** before making any code changes.

### 0.1. Create Development Branch
```powershell
cd d:\Appar\wspr-pc
git checkout master          # Ensure you are on the stable branch
git pull origin master        # Get latest changes
git checkout -b feature/speaker-id
```

### 0.2. Verify Clean State
Before proceeding, confirm the app builds and runs correctly on the new branch:
```powershell
dotnet build WsprPc/WsprPc.csproj
dotnet run --project WsprPc/WsprPc.csproj
```
If the app starts without errors, you may proceed.

### 0.3. Rollback Procedure
If this feature causes issues, rollback is simple:
```powershell
git checkout master           # Return to stable branch
git branch -D feature/speaker-id  # Optional: delete the feature branch
```

---

## 1. Project Context (For AI Agents)

This section provides essential context for agents unfamiliar with this codebase.

### 1.1. Technology Stack
- **Language**: C# (.NET 8.0)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Pattern**: Code-behind (no MVVM). Logic lives in `*.xaml.cs` files.

### 1.2. Key Directories
| Path | Purpose |
|------|---------|
| `WsprPc/` | Main application source code. |
| `WsprPc/Services/` | Backend services (audio, transcription, VAD). |
| `WsprPc/MainWindow.xaml` | Main UI definition. |
| `third_party/` | External binaries (Whisper, models). |

### 1.3. Existing Services (Reference)
- `WhisperNetEngine.cs`: Transcription engine using Whisper.net.
- `SileroVadModel.cs`: VAD (Voice Activity Detection) using ONNX Runtime.
- `DictationController.cs`: Orchestrates live dictation flow.

### 1.4. Installation Philosophy
The app must be **simple to install** for non-technical users:
- No manual command-line steps.
- Models should auto-download on first use if missing.
- Single installer or zip-file distribution.

---

## 2. Overview
This feature introduces **Speaker Identification** (Diarization) to TapScribePC. Users can process pre-recorded audio files and receive a transcription with speaker labels (e.g., "[Speaker 1]: ...").

The solution uses **Sherpa-ONNX** for offline diarization, maintaining "Privacy First" & "Offline" values.

## 3. Goals
- **Offline**: 100% local processing.
- **UX**: New tab for file-based transcription.
- **Output**: Text with speaker labels, saveable or copyable.

## 4. User Stories
- Open a "Filer" tab to transcribe recordings.
- Select an audio file (MP3/WAV/M4A).
- See progress during processing.
- View result with "[Speaker X]" labels.
- Save as `.txt` or copy to clipboard.

## 5. Technical Architecture

### 5.1. Core Engine: Sherpa-ONNX
- **NuGet**: `Sherpa.Onnx`
- **Models**: Auto-downloaded to `third_party/models/sherpa/` on first use.

### 5.2. Data Flow
```
[Audio File] → [Sherpa Diarization] → [Segments w/ Speaker IDs]
                                           ↓
                               [Whisper per Segment]
                                           ↓
                               [Combined Text Output]
```

### 5.3. Code Changes
1. **Dependencies**: Add `Sherpa.Onnx` to `WsprPc.csproj`.
2. **Services**:
   - `Services/Diarization/SherpaDiarizationService.cs`
   - `Services/Diarization/ModelDownloader.cs` (auto-download logic)
3. **UI**: New `TabItem` in `MainWindow.xaml`.

## 6. UI/UX Design
**Tab Name**: `Filer`

| Element | Description |
|---------|-------------|
| Header | "Transkribera ljudfil" |
| Button | "Välj fil..." → Opens file picker |
| Dropdown | "Antal talare" (optional hint) |
| Button | "Starta" (disabled until file selected) |
| TextBox | Read-only result display |
| Buttons | "Kopiera Text", "Spara till Fil" |

## 7. Model Auto-Download
To ensure non-technical users have a seamless experience:

1. On first run of the "Filer" feature, check if models exist in `third_party/models/sherpa/`.
2. If missing, show a dialog: "Modeller saknas. Vill du ladda ner dem nu? (~150 MB)"
3. Download from official Sherpa GitHub releases.
4. Store in `third_party/models/sherpa/`.
5. Retry initialization.

## 8. Implementation Checklist

### Phase 1: Infrastructure
- [ ] Verify on `feature/speaker-id` branch.
- [ ] Install `Sherpa.Onnx` NuGet.
- [ ] Implement `ModelDownloader.cs`.

### Phase 2: Backend
- [ ] Implement `SherpaDiarizationService`.
- [ ] Create `DiarizationSegment` class.
- [ ] Slice audio per segment → send to Whisper.

### Phase 3: UI
- [ ] Add `TabItem` to `MainWindow.xaml`.
- [ ] Wire up buttons and progress.
- [ ] Display result with speaker labels.

### Phase 4: Verification
- [ ] Test with multi-speaker audio.
- [ ] Verify offline mode.
- [ ] Verify rollback to `master` works.

## 9. Risks
| Risk | Mitigation |
|------|------------|
| Model size (~150 MB) | Auto-download with progress dialog. |
| Long processing time | Async with progress bar; UI must not freeze. |
| Sherpa API changes | Pin NuGet version. |

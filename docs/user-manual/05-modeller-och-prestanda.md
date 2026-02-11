---
title: "Modeller och prestanda"
summary: "Välj modell och prestandainställningar för stabil och snabb användning."
audience: "Slutanvändare"
feature_area: "Prestanda"
keywords:
  - "modell"
  - "autotune"
  - "trådar"
  - "vad"
  - "prestanda"
last_verified_from_code: "2026-02-09"
source_refs:
  - "WsprPc/MainWindow.xaml"
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/Services/AutoTuneService.cs"
  - "WsprPc/AppConfig.cs"
---
# 05. Modeller och prestanda

Den här sidan hjälper dig välja rätt modell och få stabil prestanda utan tekniska detaljer.

## Modellval i praktiken

TapScribe använder lokala Whisper-modeller. Större modell ger ofta bättre kvalitet, men tar längre tid.

- Snabbast: mindre modell (bra för utkast och enklare möten).
- Balanserat: mellanmodell (bra standardval för de flesta).
- Högst kvalitet: större modell (bra när korrekthet är viktigare än hastighet).

Rekommendation: börja med ett mellanläge och byt upp eller ner efter första testtranskriberingen.

## Presets, nedladdning och manuell modell

Du kan arbeta på tre sätt:

- Preset: välj en färdig modellprofil i appen.
- Nedladdning i appen: hämta modellen direkt från listan.
- Manuell modell: ange URL och filnamn om du laddar själv.

När du använder manuell URL + filnamn måste de höra ihop.

- URL ska peka på exakt modellfil du vill använda.
- Filnamnet i appen ska matcha filen som faktiskt laddas ned.
- Modell-länken och filnamnet ska beskriva samma variant (till exempel språk/storlek).

Om länk och filnamn inte matchar kan modellen inte användas korrekt.

Fallback om nedladdning misslyckas:

1. Prova igen med stabil internetanslutning.
2. Byt till en preset-modell först så arbetet kan fortsätta.
3. Kontrollera att URL är komplett och att filnamnet är exakt samma som modellfilen.

## AutoTune och manuell trådinställning

TapScribe kan automatiskt välja antal trådar (AutoTune) baserat på din dator.

- AutoTune på: tryggt standardläge för jämn prestanda och låg risk för överbelastning.
- Manuellt trådantal: ger mer kontroll om du vill prioritera hastighet eller respons i andra program.

Praktisk effekt:

- Fler trådar kan ge snabbare transkribering, men högre CPU-belastning.
- Färre trådar ger ofta lugnare dator under pågående arbete, men längre transkriberingstid.

Rekommendation:

- Behåll AutoTune om du är osäker.
- Om datorn känns seg: sänk trådantalet stegvis.
- Om du vill korta väntetid: höj försiktigt och testa med samma ljudfil.

## VAD (röstdetektering)

VAD används för att skilja tal från tystnad och bakgrundsljud. Det kan minska onödig bearbetning och förbättra flytet.

### Aktivera/inaktivera VAD

- På: bättre segmentering i de flesta fall.
- Av: kan vara användbart om VAD missar lågmält tal.

### Tröskelvärde (Threshold)

Styr hur säker appen måste vara på att något är tal.

- Högre värde: färre falska träffar från brus, men risk att svagt tal missas.
- Lägre värde: fångar mer svagt tal, men kan ta med mer bakgrundsljud.

### Tystnadslängd (Silence Duration)

Styr hur lång tystnad som krävs för att dela upp segment.

- Kortare tid: fler, kortare segment.
- Längre tid: färre, längre segment.

Rekommenderad finjustering:

1. Börja med standardvärden.
2. Om ord tappas: sänk tröskeln lite.
3. Om brus blir text: höj tröskeln lite.
4. Om texten blir för hackig: öka tystnadslängden.
5. Om pauser inte separeras tydligt: minska tystnadslängden.

## Snabb felsökning

- Problem: Transkriberingen är för långsam.
  - Åtgärd: använd mindre modell eller sänk kvalitetsnivå.
- Problem: Datorn blir trög under körning.
  - Åtgärd: slå på AutoTune eller minska manuellt trådantal.
- Problem: För mycket brus i texten.
  - Åtgärd: höj VAD-tröskeln stegvis.
- Problem: Tyst/avlägsen röst försvinner.
  - Åtgärd: sänk VAD-tröskeln och testa igen.

---
title: "Möten och talarigenkänning"
summary: "Transkribera mötesfiler med talaretiketter och justera kvaliteten med rätt reglage."
audience: "Slutanvändare"
feature_area: "Möten"
keywords:
  - "möten"
  - "talarigenkänning"
  - "diarization"
  - "talare"
  - "filtranskribering"
last_verified_from_code: "2026-02-11"
source_refs:
  - "WsprPc/MainWindow.xaml"
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/Services/Diarization/FileTranscriptionService.cs"
  - "WsprPc/Services/Diarization/SherpaDiarizationService.cs"
---
# 6. Möten och talarigenkänning

Det här läget är för inspelade möten (t.ex. `mp3`, `wav`, `m4a`, `aac`, `mp4`) där du vill få text med talaretiketter, till exempel:

`[00:42] [Talare 2]`

## Snabbstart

1. Gå till fliken `Möten`.
2. Klicka `Välj ljudfil`.
3. Välj `Antal talare`:
   - `Auto` (rekommenderat i normalfallet).
   - `1-10` om du vill låsa till ett känt antal.
4. Kontrollera att `Detektera mötestyp` är rätt för ditt material.
5. Klicka `Starta transkribering`.
6. När körningen är klar: klicka `Visa transkribering` eller `Spara`.

## Ladda ner modeller för talarigenkänning

Första gången behöver appen extra modeller och verktyg.

- Om du ser rutan `Modeller för talarigenkänning saknas`, klicka `Ladda ner modeller`.
- Nedladdningen är engångsjobb per installation.
- Under nedladdningen visas status och progress.

Om nedladdningen avbryts kan du köra igen. Se `10-felsokning.md` vid problem.

## Antal talare: Auto eller manuellt

- `Auto`:
  - Rekommenderat standardläge och fungerar normalt bra.
  - Bra när du inte vet hur många som pratar.
- Manuellt antal (`1-10`):
  - Använd när du vet exakt antal och vill låsa utfallet.
  - Kan vara hjälpsamt vid svåra inspelningar med mycket överlapp.

Praktiskt råd: börja med `Auto`. Om resultatet får för många eller för få talare, testa manuellt antal innan du finjusterar avancerade reglage.

## Detektera mötestyp och justeringsvärde

`Detektera mötestyp` analyserar ljudet och försöker skilja på:

- fysiskt möte (samma rum)
- digitalt möte (Teams/Zoom)

Vid fysiskt möte kan appen justera känsligheten med värdet i `Fysiskt möte: +...`.

- Högre justering: större chans att slå ihop liknande röster.
- Lägre justering: större chans att hålla röster separata.

Typiskt värde är runt `+0.10` (i vissa versioner `+0.15`).

## Avancerade inställningar och hur resultatet påverkas

Öppna `Avancerade inställningar` i mötesvyn.

- `Känslighet`:
  - Lägre värde -> fler talare (mer aggressiv split).
  - Högre värde -> färre talare (mer sammanslagning).
- `Kortaste tal`:
  - Höj för att ignorera mycket korta ljudstötar.
  - För högt värde kan klippa bort korta riktiga repliker.
- `Kortaste paus`:
  - Lägre värde -> fler segment (snabbare talarväxling fångas).
  - Högre värde -> längre sammanhängande segment.
- `Städning`:
  - Tar bort talare med mycket kort total taltid.
  - Bra mot hostningar, klick och bakgrundsljud som felaktigt blir "talare".
- `Använd tonhöjdsskydd`:
  - Hjälper till att undvika att olika rösttyper slås ihop.
  - Rekommenderas normalt att vara på.

## Progress- och resultatfönster

Under körning visas:

- statusrad (`Identifierar talare...`, `Transkriberar segment ...`)
- procent
- förfluten tid (`⏱️ mm:ss`)

Det är normalt att procenten kan stå still en stund under tunga steg.

När klart visas resultatkort med:

- total tid
- uppskattad hastighet (`x`)
- knappar för att visa eller spara transkriberingen

`Visa transkribering` öppnar ett separat fönster där du kan kopiera text eller spara till fil.

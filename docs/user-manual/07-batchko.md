---
title: "Batchkö"
summary: "Kör många mötesfiler i kö med per-fil-inställningar och autosparning."
audience: "Slutanvändare"
feature_area: "Möten"
keywords:
  - "batch"
  - "kö"
  - "autospara"
  - "experiment"
  - "filer"
last_verified_from_code: "2026-02-11"
source_refs:
  - "WsprPc/BatchQueueWindow.xaml"
  - "WsprPc/BatchQueueWindow.xaml.cs"
  - "WsprPc/BatchExperimentsWindow.xaml.cs"
  - "WsprPc/Models/BatchItem.cs"
---
# 7. Batchkö

`Batchkö` låter dig köra många mötesfiler i en och samma körning.

## När batchkö är rätt val

Använd batchkö när du vill:

- transkribera flera möten över natten
- jämföra inställningar mellan samma filer
- minimera manuellt klickande

## Starta batchkön

1. Gå till `Möten`.
2. Klicka `Batchkö...`.
3. Klicka `+ Lägg till filer`.
4. Justera per rad vid behov:
   - `Talare` (`Auto` eller `1-5`)
   - `Känslighet`
   - `🛡️` (tonhöjdsskydd)
5. Välj batchalternativ.
6. Klicka `Starta batch`.

## Kolumner i batchlistan

- `Fil`: filnamn (tooltip visar full sökväg).
- `Talare`: antals-hint för just den filen.
- `Känslighet`: diarization-tröskel för just den filen.
- `🛡️`: tonhöjdsskydd per fil.
- `Status`: väntar, körs, klar, fel eller avbruten.
- `Åtgärd`: visa resultat, duplicera, experiment, ta bort.

## Batchalternativ

- `Spara automatiskt bredvid källfilen (.txt)`:
  - På som standard.
  - Skapar `.txt` i samma mapp som ljudfilen.
  - Filnamnet får en variant-suffix med inställningar (t.ex. tröskel/pitch/talare).
- `Stäng appen när kört klart`:
  - Stänger hela appen när batchen är färdig.
  - Sker bara om inga filer slutade i fel/avbruten.
- `Detektera mötestyp ...`:
  - Batchens globala mötestypsdetektering.
  - Justerar känslighet för fysiska möten.

## Under körning

- `Starta batch` och `Lägg till filer` låses under körning.
- `Avbryt körning` stoppar återstående jobb.
- Varje rad visar egen status och progress.
- Efter körning visas en sammanfattning i botten (klart/fel/avbrutna).

## Se resultat per fil

När en rad är klar visas visningsknappen:

1. Klicka `👁`.
2. Läs, kopiera eller spara resultatet i resultatfönstret.

## Tips för stabil batch

- Börja med `Auto` per fil och lås manuellt antal bara när du behöver styra utfallet.
- Låt `tonhöjdsskydd` vara på om röster blandas.
- Kör först en testbatch med 1-2 filer innan större nattkörning.

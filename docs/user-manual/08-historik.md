---
title: "Historik"
summary: "Sök, filtrera, kopiera och rensa tidigare transkriberingar och AI-resultat."
audience: "Slutanvändare"
feature_area: "Historik"
keywords:
  - "historik"
  - "filter"
  - "sök"
  - "rensa"
  - "kopiera"
last_verified_from_code: "2026-02-09"
source_refs:
  - "WsprPc/HistoryWindow.xaml"
  - "WsprPc/HistoryWindow.xaml.cs"
  - "WsprPc/Stores/HistoryStore.cs"
  - "WsprPc/Models/HistoryItem.cs"
---
# 8. Historik

Historiken samlar tidigare resultat från:

- transkribering
- AI-bearbetning

Öppna via knappen `Historik` i huvudfönstret.

## Hitta rätt post snabbt

Använd filterraden:

- `Sök`: fritext i output, datum eller tid
- `Datum`: Alla, Idag, 7 dagar, 30 dagar, Anpassat
- `Typ`: Alla, Transkribering, AI

Vid `Anpassat` väljer du `Från` och `Till`.

## Sortera och granska

Du kan sortera kolumner (t.ex. datum, tid, output) genom att klicka kolumnrubrikerna.

För varje rad kan du:

- kopiera output via `📋`
- läsa full text via tooltip/markering

## Ta bort historik

### Ta bort markerade poster

1. Markera en eller flera rader.
2. Klicka `Ta bort markerade`.
3. Bekräfta.

### Ta bort allt

1. Klicka `Ta bort alla`.
2. Bekräfta varningen.

Observera: borttagning går inte att ångra.

## Filter + städning (rekommenderat arbetssätt)

1. Filtrera först (t.ex. `Typ = AI`, `Datum = 30 dagar`).
2. Kontrollera antal synliga poster i räknaren.
3. Markera och ta bort i mindre omgångar.

Det minskar risken att du raderar fel data.

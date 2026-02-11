---
title: "Live-diktering"
summary: "Praktisk guide för att diktera med F8/F9, hantera autoklistra och förstå statusflödet."
audience: "Slutanvändare"
feature_area: "Diktering"
keywords:
  - "live-diktering"
  - "F8"
  - "F9"
  - "autoklistra"
  - "status"
  - "spara"
  - "historik"
last_verified_from_code: "2026-02-09"
source_refs:
  - "WsprPc/MainWindow.xaml"
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/Services/GlobalKeyHoldService.cs"
  - "WsprPc/Services/DictationController.cs"
  - "WsprPc/Services/TrayIconService.cs"
  - "WsprPc/TranscriptionResultWindow.xaml.cs"
  - "WsprPc/Stores/HistoryStore.cs"
---

# Live-diktering

[BILD: Snabbstart med statusindikator och F8/F9-information]

Här lär du dig arbetsflödet för snabb diktat i TapScribe.

## När använder jag detta?

- När du vill skriva text med rösten i valfritt program.
- När du vill växla mellan snabb råtext (`F8`) och AI-bearbetad text (`F9`).
- När du vill förstå statusmeddelanden och vad som händer i bakgrunden.

## Steg för steg

### 1. Förbered målfönstret
1. Öppna appen där du vill få texten (till exempel mejl eller anteckning).
2. Placera markören där texten ska hamna.
3. Kontrollera i TapScribe att `Autoklistra` är på om du vill att texten klistras in automatiskt.

### 2. Diktera i direktläge (F8)
1. Håll in `F8`.
2. Prata.
3. Släpp `F8` för att stoppa och starta transkribering.
4. Vänta tills status går från `Bearbetar...` till `Väntar`.

[BILD: Status "Lyssnar..." under aktiv diktat]

### 3. Diktera i AI-läge (F9)
1. Håll in `F9`.
2. Prata.
3. Släpp `F9`.
4. TapScribe transkriberar och kör sedan vald AI/prompt i bakgrunden.

Tips: I AI-läge kan appen först visa att den bearbetar i bakgrunden innan resultatet presenteras.

### 4. Förstå autoklistra
- Om `Autoklistra` är aktiv försöker TapScribe klistra in resultatet i fönstret du hade aktivt när du började prata.
- Om inklistring misslyckas får du status om det, och texten finns kvar i appen/urklipp för manuell inklistring.

### 5. Använd status och systemfält
- `Väntar`: appen är redo.
- `Lyssnar...` eller `Lyssnar (AI)...`: inspelning pågår.
- `Bearbetar...` eller `Bearbetar (AI)...`: ljudet omvandlas till text.
- Vid minimering kan appen fortsätta i systemfältet.
- Högerklick på systemfältsikonen ger snabbval som öppna appen och avsluta.

[BILD: Systemfältsmeny med status]

### 6. Spara och återanvänd text
- Historiken sparar löpande nya resultat.
- I resultatfönster kan du använda `Spara` för att exportera till `.txt`.
- Du kan även kopiera text och klistra in manuellt där du vill.

## Om det inte fungerar

- Ingen text kommer efter `F8`/`F9`: kontrollera att en modell är installerad.
- Ingen reaktion på tangenter: kontrollera att snabbtangenterna fortfarande är satta till rätt val i appen.
- Fel text hamnar i fel app: sätt markören i rätt fält innan du håller ner tangenten.
- Autoklistra fungerar inte i ett visst program: stäng av `Autoklistra` tillfälligt och klistra in manuellt.
- Appen verkar "borta": den kan vara minimerad till systemfältet, öppna den via ikonen vid klockan.

---
title: "Appendix: Kortkommandon och flöden"
summary: "Översikt av snabbtangenter och praktiska arbetsflöden i appen."
audience: "Slutanvändare"
feature_area: "Referens"
keywords:
  - "kortkommandon"
  - "f8"
  - "f9"
  - "flöden"
  - "arbetsflöde"
last_verified_from_code: "2026-02-09"
source_refs:
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/Services/GlobalKeyHoldService.cs"
  - "WsprPc/Services/DictationController.cs"
---
# Appendix: Kortkommandon och flöden

## Kortkommandon

| Funktion | Standardtangent | Hur det fungerar |
|---|---|---|
| Direkttranskribering | `F8` | Håll in -> prata -> släpp -> text transkriberas direkt. |
| AI-bearbetning | `F9` | Håll in -> prata -> släpp -> transkribering skickas till vald AI-prompt. |

Kortkommandon kan ändras i huvudvyn under `Snabbtangenter`.

## Flöde: Direkttranskribering (F8)

1. Håll in `F8`.
2. Prata.
3. Släpp `F8`.
4. Appen transkriberar och visar resultat.
5. Om `Autoklistra` är på klistras texten in i aktivt fönster.

## Flöde: AI-bearbetning (F9)

1. Välj prompt i appen.
2. Håll in `F9`.
3. Prata.
4. Släpp `F9`.
5. Appen transkriberar och kör vald prompt.
6. Resultat sparas i historik.

## Flöde: Mötestranskribering

1. Gå till `Möten`.
2. Välj ljudfil.
3. Välj talarantal (`Auto` eller manuellt).
4. Justera ev. avancerade diarization-inställningar.
5. Klicka `Starta transkribering`.
6. Följ progresspanelen tills klart.
7. Öppna resultat med `Visa transkribering` eller spara till fil.

## Flöde: Batchkö

1. Öppna `Batchkö...` från mötesvyn.
2. Lägg till flera filer.
3. Ställ in talare/känslighet/pitch per rad.
4. Välj batchalternativ (autospara, stäng vid klart, mötestypsdetektering).
5. Starta batch och följ status per rad.
6. Öppna enskilda resultat via `👁` när rader är klara.

## Flöde: Historik och rensning

1. Öppna `Historik`.
2. Filtrera med sök, datum och typ.
3. Kopiera viktiga poster vid behov.
4. Markera och ta bort utvalda poster, eller rensa allt.

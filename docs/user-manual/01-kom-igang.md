---
title: "Kom igång"
summary: "Steg-för-steg för första start, rekommenderad modell och grundinställningar."
audience: "Slutanvändare"
feature_area: "Onboarding"
keywords:
  - "välkomstflöde"
  - "modell"
  - "starta med windows"
  - "mörkt läge"
  - "uppdatering"
  - "systemfält"
last_verified_from_code: "2026-02-09"
source_refs:
  - "WsprPc/WelcomeWindow.xaml"
  - "WsprPc/WelcomeWindow.xaml.cs"
  - "WsprPc/MainWindow.xaml"
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/AppConfig.cs"
---

# Kom igång

[BILD: Välkomstfönstret med "Ladda ner modell" markerad]

Den här guiden tar dig från första start till första fungerande diktat.

## När använder jag detta?

- När du installerat TapScribe för första gången.
- När du vill sätta upp appen på en ny dator.
- När du vill återställa ett enkelt och tryggt grundläge.

## Steg för steg

### 1. Starta appen och gå igenom välkomstflödet
1. Starta TapScribe.
2. Läs kortguiden i välkomstrutan.
3. Notera snabbtangenterna: `F8` för direktläge och `F9` för AI-läge.

[BILD: Välkomstkortet "Så funkar det"]

### 2. Välj autostart om du vill
1. Kryssa i `Starta TapScribe automatiskt när jag loggar in på Windows` om du vill att appen alltid ska vara redo.
2. Fortsätt med `Ladda ner modell` eller `Hoppa över`.

Tips: Du kan alltid ändra detta senare via `Starta med Windows` i appen.

### 3. Ladda ner rekommenderad modell
1. Klicka `Ladda ner modell` i välkomstflödet.
2. Vänta tills nedladdningen är klar.
3. När modell är klar kan du börja diktera direkt.

[BILD: Modellstatus i Snabbstart]

### 4. Kontrollera att appen syns i systemfältet
1. Minimera appen en gång.
2. Bekräfta att TapScribe-ikonen syns vid klockan i Windows.
3. Om den är dold: slå på TapScribe i Windows inställningar för systemikoner.

### 5. Välj utseende
1. Använd växeln för mörkt läge i huvudfönstrets övre del.
2. Välj det läge som är lättast att läsa i din arbetsmiljö.

[BILD: Mörkt läge-växeln i toppfältet]

### 6. Förstå uppdateringsbanner
1. Om en ny version finns kan en banner visas högst upp.
2. Klicka `Ladda ner` för att öppna hämtningssidan.
3. Om ingen banner syns kör du redan senaste versionen, eller så har ingen ny version hittats.

## Vad sparas automatiskt?

- Många inställningar sparas direkt när du ändrar dem (till exempel snabbtangenter, autoklistra, mörkt läge och start med Windows).
- Historik sparas löpande när ny transkribering skapas.

## Om det inte fungerar

- Välkomstfönstret visas inte: det är normalt om det redan visats tidigare.
- Nedladdning av modell fastnar: kontrollera internetanslutning och försök igen.
- `Starta med Windows` verkar inte slå igenom: vissa IT-policyer i Windows kan blockera autostart.
- Uppdateringsbanner saknas trots ny release: kontroll kan vara tillfälligt misslyckad, prova igen senare via appens uppdateringskontroll.

---
title: "Ordlista"
summary: "Snabb förklaring av vanliga ord och uttryck i TapScribe PC."
audience: "Slutanvändare"
feature_area: "Grunder"
keywords:
  - "ordlista"
  - "F8"
  - "F9"
  - "autoklistra"
  - "systemfält"
  - "uppdateringsbanner"
last_verified_from_code: "2026-02-09"
source_refs:
  - "WsprPc/WelcomeWindow.xaml"
  - "WsprPc/MainWindow.xaml"
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/Services/TrayIconService.cs"
  - "WsprPc/AppConfig.cs"
---

# Ordlista

[BILD: Översikt av huvudfönstret med markerade områden]

Den här ordlistan hjälper dig att snabbt förstå orden som används i appen och i manualen.

## När använder jag detta?

- När du är ny i TapScribe och vill förstå grunderna.
- När ett ord i appen känns oklart.
- När du ska hjälpa en kollega komma igång.

## Vanliga ord

### Välkomstflöde
Första guiden som visas när du startar appen första gången. Där får du rekommenderad modell, snabbgenvägar och val för autostart.

### Modell
Språkmodellen som TapScribe använder för att göra tal till text. I välkomstflödet föreslås en modell som passar direkt för att komma igång.

### Ladda ner modell
Knappen som hämtar vald modell till din dator. Du behöver normalt göra detta en gång per modell.

### Direktläge (F8)
Håll in `F8`, prata, släpp. Texten transkriberas utan AI-bearbetning.

### AI-läge (F9)
Håll in `F9`, prata, släpp. Texten transkriberas och kan därefter bearbetas av vald prompt/AI-inställning.

### Håll in för att prata
Tangenten fungerar som en "tryck-och-prata"-knapp: inspelning startar när du håller nere och stoppar när du släpper.

### Autoklistra
Om `Autoklistra` är på försöker appen klistra in resultatet automatiskt i fönstret du jobbade i.

### Starta med Windows
Inställning som gör att TapScribe startar när du loggar in.

### Status
Visar vad appen gör just nu, till exempel `Väntar`, `Lyssnar...`, `Bearbetar...` eller felstatus.

### Systemfält (tray)
Ikonen vid klockan i Windows. Där kan du se status, öppna appen igen och avsluta.

### Mörkt läge
Växlar appens utseende mellan ljust och mörkt tema.

### Uppdateringsbanner
En banner högst upp i appen som visas när en ny version hittas. Knappen `Ladda ner` öppnar hämtningssidan.

### Historik
Sparar tidigare transkriberingar och AI-resultat så du kan hitta tillbaka till text senare.

### Spara
När du använder visning/resultatfönster kan du spara texten som `.txt` via knappen `Spara`.

## Om det inte fungerar

- Om `F8`/`F9` inte reagerar: kontrollera att appen körs och att snabbtangenterna inte är ändrade i inställningarna.
- Om autoklistra misslyckas: texten finns fortfarande i urklipp i de flesta fall, klistra in manuellt med `Ctrl+V`.
- Om systemfältsikonen inte syns: slå på TapScribe i Windows inställningar för aktivitetsfältets systemikoner.
- Om modell saknas: öppna modellhantering och ladda ner modellen igen.

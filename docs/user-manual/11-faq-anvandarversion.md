---
title: "FAQ för användare"
summary: "Vanliga frågor och enkla svar för daglig användning av TapScribe."
audience: "Slutanvändare"
feature_area: "FAQ"
keywords:
  - "faq"
  - "f8"
  - "f9"
  - "autoklistra"
  - "möten"
  - "batch"
  - "historik"
  - "integritet"
last_verified_from_code: "2026-02-11"
source_refs:
  - "WsprPc/MainWindow.xaml"
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/AppConfig.cs"
  - "WsprPc/BatchQueueWindow.xaml.cs"
  - "WsprPc/HistoryWindow.xaml.cs"
  - "WsprPc/PromptEditorWindow.xaml.cs"
  - "WsprPc/Services/DictationController.cs"
---

# 11. FAQ (användarversion)

## Kom igång

### Vad är snabbaste sättet att börja?
Starta appen, ladda ner en rekommenderad modell och gör ett kort test med `F8`.

### Måste jag vara teknisk för att använda TapScribe?
Nej. Du kan komma långt med standardläget: `F8` för direkttext och `F9` för AI-bearbetning.

### Vad betyder `F8` och `F9`?
`F8` skriver ut det du säger som text. `F9` gör samma sak men kan också förbättra texten med vald AI-prompt.

## Diktering och resultat

### Varför hamnar texten i fel program?
TapScribe klistrar in i det fönster som var aktivt när du började prata. Klicka i rätt textfält innan du håller in tangenten.

### Vad gör `Autoklistra`?
När den är på försöker appen klistra in texten automatiskt. Om det misslyckas kan du klistra in manuellt.

### Vad betyder statusen `Väntar`, `Lyssnar` och `Bearbetar`?
`Väntar` = redo. `Lyssnar` = inspelning pågår. `Bearbetar` = appen gör om tal till text.

### Var hittar jag mina gamla texter?
I `Historik`. Där kan du söka, filtrera och kopiera tidigare resultat.

## AI-läge och promptar

### Skickas allt till molnet när jag använder appen?
Nej. Själva transkriberingen är lokal. Text skickas till moln endast när du aktivt använder AI-funktioner.

### Varför får jag fel om API-nyckel?
Prompten använder en leverantör (Gemini eller OpenAI) som saknar nyckel i dina inställningar.

### Vad är en prompt i praktiken?
En prompt är en enkel instruktion för hur texten ska skrivas om, till exempel: "gör texten kortare och tydligare".

### När ska jag använda minne?
När du ofta vill återanvända samma fakta, ton eller signatur. Stäng av minne för engångsärenden.

## Möten och batch

### Vad gör jag om talarigenkänning saknas?
Klicka `Ladda ner modeller` i mötesvyn. Det behövs normalt bara en gång per installation.

### Ska jag välja `Auto` eller manuellt antal talare?
Starta med `Auto` - det fungerar normalt bra. Välj manuellt antal när du vet exakt antal talare och vill låsa utfallet.

### Varför reagerar inte `F8`/`F9` i ett visst program?
Kontrollera att TapScribe körs, att modellen är installerad och att du klickat i rätt målapp innan du håller nere tangenten. Vissa program hanterar inklistring annorlunda, så prova att klistra in manuellt med `Ctrl+V`.

### Vad gör jag om texten blir på fel språk?
Tala tydligare i början av inspelningen och med hela meningar. Om felet kvarstår, testa en annan lokal modell och jämför utfallet på samma ljud.

### Varför verkar procenten fastna under möteskörning?
Vissa steg tar längre tid. Om tiden fortfarande går framåt arbetar appen fortfarande.

### När är batchkö rätt val?
När du vill köra många filer i rad, till exempel över natten, utan att starta varje fil manuellt.

## Uppdatering, integritet och support

### Hur vet jag om en ny version finns?
Appen kan visa en uppdateringsbanner. Du kan också köra manuell uppdateringskontroll.

### Vad ska jag göra med känsligt innehåll?
Använd lokal transkribering utan AI om du behöver strikt lokal hantering.

### Vad tar jag med till support om något strular?
Beskriv vad som händer, vilket filformat du använde, vilka inställningar du hade och vilken version av appen du kör.

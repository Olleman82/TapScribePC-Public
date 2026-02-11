---
title: "AI-bearbetning"
summary: "Så fungerar AI-läget från tal till färdig text med promptar och val av leverantör."
audience: "Slutanvändare"
feature_area: "AI"
keywords:
  - "ai"
  - "f9"
  - "prompt"
  - "gemini"
  - "openai"
last_verified_from_code: "2026-02-09"
source_refs:
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/PromptPickerWindow.xaml.cs"
  - "WsprPc/PromptEditorWindow.xaml.cs"
  - "WsprPc/Services/Ai/GeminiClient.cs"
  - "WsprPc/Services/Ai/OpenAiClient.cs"
---
# 03. AI-bearbetning

Det här kapitlet visar hur AI-läget fungerar i praktiken: från att du håller in snabbknappen tills du får resultat.

## Vad AI-läget gör
- Vanlig transkribering (lokalt): omvandlar tal till text på din dator.
- AI-bearbetning: skickar den transkriberade texten till vald AI-leverantör (Gemini eller OpenAI) för att formatera/förbättra texten enligt din prompt.

Kort sagt: röst in -> lokal transkribering -> AI enligt prompt -> resultat.

## Flödet steg för steg (hotkey till resultat)
1. Håll in AI-knappen (standard: `F9`).
2. TapScribe lyssnar medan du håller nere tangenten.
3. Släpp tangenten.
4. Appen transkriberar lokalt.
5. En prompt väljs (standardprompt, senast valda eller promptväljare).
6. Texten bearbetas av AI enligt promptens inställningar.
7. Resultatet går vidare enligt prompt:
- Klistras in där du stod.
- Eller skickas till webhook.
- Eller öppnas som e-postutkast i din mailklient (mail-prompt).

## Promptval: väljare, standard och senast valda
TapScribe har tre sätt att välja prompt i AI-läget:

- `Använd standardprompt`:
  Använder alltid den prompt du markerat som standard. Ingen promptväljare visas.

- `Använd senast valda`:
  Använder prompten du använde senast i AI-läget.

- Ingen av ovan:
  Promptväljaren öppnas varje gång så du väljer manuellt.

Viktig ordning:
- Standardprompt har högst prioritet.
- Senast valda används bara om standardprompt inte används.

## Vad som händer om något saknas
- Ingen transkribering och inget urklipp: processen stoppas med meddelande.
- Ingen prompt vald: processen stoppas med meddelande.
- Saknad API-nyckel för vald leverantör: AI-bearbetningen avbryts med felruta.

## API-nycklar (Gemini/OpenAI)
Du lägger in nycklar i TapScribes inställningar.

- Gemini-nyckel krävs för promptar med leverantör `Gemini`.
- OpenAI-nyckel krävs för promptar med leverantör `OpenAI`.

Säkerhet:
- API-nycklar är hemligheter. Dela dem aldrig i mejl/chat/skärmdumpar.
- Om en nyckel råkar läcka: skapa en ny och inaktivera den gamla direkt.

## Lokal vs moln (integritetsgräns)
Det här är viktigt:
- Tal -> text-transkribering körs lokalt på din dator.
- När AI-läge används skickas texten (inte rått ljud) till vald AI-leverantör för bearbetning.
- Promptar, minne och API-nycklar lagras lokalt i appens data.
- Om du aktiverar webhook skickas resultatet också till din webhook-adress.

## Säker användning
Undvik att skicka känsligt innehåll till molntjänster om du inte måste.

Rekommendation:
- Använd vanlig transkribering (utan AI) för extra känslig information.
- I AI-läge: minimera personnummer, patientdata, lösenord och interna hemligheter.
- Använd webhook endast till system du litar på.

## Vanliga problem och snabb felsökning

### Inget händer när jag trycker F9
- Kontrollera att mikrofonen fungerar.
- Kontrollera att en lokal modell finns för transkribering.
- Kontrollera att du inte kör mötestranskribering samtidigt.

### Jag får "Ingen prompt vald"
- Slå på `Använd standardprompt`, eller
- Slå på `Använd senast valda`, eller
- välj prompt när promptväljaren visas.

### Jag får fel om API-nyckel
- Kontrollera att rätt nyckel finns för rätt leverantör.
- Exempel: prompten står på OpenAI men bara Gemini-nyckel är ifylld.

### Resultatet blir tomt eller konstigt
- Testa att prata tydligare och lite längre.
- Prova en enklare prompt (t.ex. "Rätta stavning och gör texten tydlig").
- Kontrollera att prompten inte kräver format som din text inte innehåller.

### Fel målapp får inklistring
- Klicka i rätt fönster innan du håller in F9.
- Testa igen och vänta tills AI-bearbetningen är klar.

## Kopierbara exempel

### Exempel 1: enkel förbättring
Tala in:
`kan du skriva om detta så det blir tydligare och mer professionellt men fortfarande kort`

Exempel på prompt:
`Förbättra texten språkligt. Behåll budskapet. Svara endast med den färdiga texten.`

### Exempel 2: sammanfattning
Tala in:
`sammanfatta detta i tre punkter med tydliga beslut`

Exempel på prompt:
`Sammanfatta texten i 3 punkter: beslut, ansvarig, nästa steg. Svara endast med punktlistan.`

### Exempel 3: artigt mailutkast
Tala in:
`skriv ett kort och vänligt mail där jag tackar för mötet och föreslår nästa torsdag`

Exempel på prompt:
`Skriv om texten till ett kort, artigt mejl på svenska. Svara endast med mejltexten.`

---
title: "Promptar och minne"
summary: "Guide till promptar, minne, urklipp, webhook och mail-läge."
audience: "Slutanvändare"
feature_area: "AI"
keywords:
  - "prompt"
  - "minne"
  - "urklipp"
  - "webhook"
  - "mail"
last_verified_from_code: "2026-02-09"
source_refs:
  - "WsprPc/PromptEditorWindow.xaml"
  - "WsprPc/PromptEditorWindow.xaml.cs"
  - "WsprPc/Models/PromptDefinition.cs"
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/Services/MailService.cs"
---
# 04. Promptar och minne

Det här kapitlet hjälper dig bygga bra promptar utan tekniska termer.

## Vad en prompt är
En prompt är regler för hur AI ska skriva om din text.

Exempel:
- "Gör texten kort och vänlig"
- "Skriv som ett professionellt mejl"
- "Sammanfatta i punktform"

## Prompt-redigeraren: alla val och deras effekt

### Leverantör (Gemini eller OpenAI)
- `Gemini`: använder Googles modell.
- `OpenAI`: använder OpenAIs modell.

Effekt:
- Du behöver API-nyckel för den leverantör du valt.
- Modellval och vissa avancerade alternativ skiljer sig mellan leverantörer.

### Minne
När `Minne` är aktiverat läggs dina minnesposter till som extra kontext.

Effekt:
- AI kan automatiskt använda återkommande fakta (t.ex. bokningslänk, titel, standardfraser).
- Bra för konsekvent språk över tid.

Risk:
- Om minnet innehåller gammal eller känslig info kan svaret påverkas fel.

### Urklipp
När `Urklipp` är aktiverat skickas aktuell text i urklippet som extra kontext.

Effekt:
- Du kan kombinera det du säger med text du redan kopierat.
- Bra när du vill "uppdatera", "svara på" eller "förkorta" befintlig text.

Tips:
- Kopiera bara det som faktiskt behövs. För stort urklipp kan ge sämre fokus.

### Skicka till Webhook
När `Skicka till Webhook` är på skickas resultatet till en URL istället för att enbart klistras in.

Effekt:
- TapScribe skickar JSON med format:
`{"text":"..."}`
- Om token är ifylld skickas headern `X-Webhook-Token`.

Använd när:
- Du vill trigga automation (CRM, tickets, egna script, integrationer).

Säkerhet:
- Använd endast webhook-URL:er du litar på.
- Lägg inte in hemliga data i prompt/minne i onödan.

### RAW-läge ("Skicka obearbetad text")
RAW betyder: hoppa över AI-bearbetning.

Effekt:
- Appen skickar transkriberad text direkt.
- Ingen systeminstruktion, ingen omskrivning från AI.
- Om `Urklipp` är aktivt läggs urklippstexten till även i RAW.

Bra för:
- Snabb automation där du vill ha "råtext".
- Flöden där extern tjänst gör bearbetningen.

### Mail-prompt ("📧 Mail")
Mail-läge är specialläge för att skapa e-postutkast.

Effekt:
- AI instrueras att hitta mottagarens e-post och skapa strukturerat mailinnehåll.
- I Gemini-läge tvingas `Tänkläge` och `Google-sök` vara aktiva.
- Resultatet öppnas som utkast i din standard-mailklient.
- Inget skickas automatiskt; du granskar innan skicka.

Om tolkning misslyckas:
- Appen visar varning och lägger svaret i urklipp som fallback.

### Gemini: Tänkläge och Google-sök
- `Tänkläge`: kan ge bättre kvalitet men ofta långsammare svar.
- `Google-sök` (grounding): kan ge bättre faktaträff men ökar svarstid.

Effekt:
- Mer kvalitet och kontext, men långsammare och ibland dyrare användning beroende på leverantörens prissättning.

### OpenAI: resonemangsnivåer
Du kan välja resonemangsnivå:
- `minimal`
- `low`
- `medium`
- `high`
- `none`

Praktisk tumregel:
- Börja med `minimal` för snabbhet/stabilitet.
- Höj till `medium`/`high` om kvaliteten inte räcker.
- `none` kan fungera, men vissa modeller kan neka detta.

Varning i appen:
- Om du väljer vissa GPT-5-modeller med `none` kan appen varna att modellen kan neka och föreslå `minimal`.

## Så bygger du en bra prompt (enkelt recept)
1. Skriv syfte: vad ska texten bli?
2. Skriv format: punktlista, mail, kort svar, osv.
3. Skriv ton: vänlig, formell, tydlig, etc.
4. Lägg till "Svara endast med resultatet" om du vill undvika extra prat.

Exempelmall (kopiera):
`Skriv om texten så den blir [ton]. Format: [format]. Behåll fakta. Svara endast med resultatet.`

## Exempel du kan kopiera

### 1) Sammanfattning för chef
`Sammanfatta i 5 korta punkter. Rubriker: Läget, Risker, Beslut, Nästa steg, Deadline. Svara endast med punktlistan.`

### 2) WhatsApp-svar
`Gör ett kort och vänligt WhatsApp-svar på svenska. Max 3 meningar. Lägg till 1 passande emoji.`

### 3) Professionellt kundmail
`Skriv om till ett professionellt men varmt mejl. Tydlig ämnesrad. Avsluta med konkret nästa steg. Svara endast med mejlet.`

### 4) Korrigera språk utan att ändra innehåll
`Rätta stavning och grammatik, men ändra inte innehållet eller betydelsen. Svara endast med korrigerad text.`

## Minne i praktiken
Exempel på bra minnesposter:
- "Min signatur: /Anna Karlsson, Customer Success Manager"
- "Bokningslänk: https://example.com/book"
- "Vi tilltalar kunder med 'du'"

När minne passar bäst:
- Återkommande svarmallar
- Namn, länkar och standardfraser
- Teamets tonalitet

När minne bör vara av:
- Engångsärenden utan standarddata
- Känsligt innehåll som inte ska återanvändas

## Säkerhetsnoteringar
- Minne och promptar lagras lokalt i appen.
- AI-bearbetning innebär att text skickas till vald molnleverantör.
- Webhook skickar vidare resultat till extern adress om aktiverad.
- Mail-läge öppnar utkast, men du skickar manuellt.

## Felsökning

### Prompten syns men används inte
- Kontrollera om `Använd standardprompt` är aktiv och pekar på en annan prompt.
- Kontrollera om `Använd senast valda` överstyr ditt manuella val nästa gång.

### Minne verkar ignoreras
- Kontrollera att `Minne` är ikryssat på just den prompten.
- Kontrollera att minneslistan faktiskt har poster.

### Urklipp verkar ignoreras
- Kopiera text innan du håller in AI-knappen.
- Kontrollera att `Urklipp` är aktiverat på prompten.

### Webhook fungerar inte
- Kontrollera URL.
- Kontrollera token om mottagaren kräver `X-Webhook-Token`.
- Testa om mottagande tjänst accepterar JSON-fältet `text`.

### OpenAI-varning om reasoning
- Om du valt `none` och får problem: byt till `minimal`.

### Mail-läget hittar inte mottagare
- Tala tydligt: namn + företag.
- Lägg till mer kontext i talet (roll, domän, ort).
- Vid fallback finns resultatet i urklipp för manuell justering.

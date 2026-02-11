---
title: "Installation, uppdatering och integritet"
summary: "Hur du installerar, uppdaterar och vad som gäller för data och integritet."
audience: "Slutanvändare"
feature_area: "Drift"
keywords:
  - "installation"
  - "uppdatering"
  - "integritet"
  - "offline"
  - "api-nycklar"
last_verified_from_code: "2026-02-09"
source_refs:
  - "WsprPc/WsprPc.csproj"
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/AboutWindow.xaml.cs"
  - "WsprPc/AppConfig.cs"
---
# 09. Installation, uppdatering och integritet

Den här sidan beskriver hur du håller TapScribe uppdaterad och vad som gäller för integritet.

## Installation och grundkontroll

Efter installation:

1. Starta appen och kontrollera att den öppnas utan fel.
2. Bekräfta att minst en modell finns vald eller kan laddas ned.
3. Kör en kort testtranskribering så du vet att allt fungerar.

Om något faller på steg 2 eller 3, fortsätt ändå med en mindre modell först och justera senare.

## Uppdateringar

TapScribe kan visa tillgängliga uppdateringar och låter dig också kontrollera manuellt.

- Automatisk kontroll: appen meddelar när en ny version finns.
- Manuell kontroll: använd funktionen för att söka efter uppdatering direkt.
- Om-dialogen: visar installerad version och relevant versionsinformation.
- Uppdateringsbanner: visas när ny version upptäcks och guidar vidare.

Praktisk rekommendation:

- Kontrollera manuellt inför viktiga möten eller produktion.
- Läs versionsinfo kort innan du uppdaterar, särskilt om arbetsflödet är känsligt.

Fallback om uppdatering inte går igenom:

1. Starta om appen och kör manuell kontroll igen.
2. Installera om senaste versionen via officiell release.
3. Behåll tidigare fungerande version tills ny installation är verifierad.

## Integritet och gränser för datadelning

TapScribe är offline-first för transkribering.

- Själva transkriberingen körs lokalt på din dator.
- Ljud skickas inte till molnet som standard.

AI-funktioner (till exempel textpolering/sammanfattning) är separata:

- Text skickas endast när du aktivt använder en AI-funktion.
- Text skickas då till den leverantör du har valt.
- Om du inte använder AI-funktioner lämnar transkriptinnehåll inte den lokala miljön via dessa tjänster.

Rekommendation för känsligt material:

- Arbeta utan AI-funktioner när policy kräver strikt lokal hantering.
- Använd AI-funktioner först efter intern bedömning av dataklassning.
- Rensa eller anonymisera känsliga personuppgifter innan extern AI används.

## Integritetsvänligt arbetssätt i vardagen

- Välj lokal transkribering som standard.
- Aktivera AI-bearbetning bara när nyttan är tydlig.
- Dokumentera vilken AI-leverantör som används i teamets rutin.
- Kontrollera resultat innan vidare delning.

## Snabb felsökning

- Problem: Ingen uppdateringsinformation visas.
  - Åtgärd: kör manuell uppdateringskontroll och kontrollera nätverksåtkomst.
- Problem: Osäkerhet kring datadelning.
  - Åtgärd: stäng av AI-bearbetning och kör endast lokal transkribering.
- Problem: Version oklar vid supportärende.
  - Åtgärd: öppna Om-dialogen och bekräfta exakt versionsnummer.

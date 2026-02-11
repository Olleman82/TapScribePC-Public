---
title: "FAQ gap-analys"
summary: "Prioriterad lista över användarfrågor som bör läggas till i FAQ och webb."
audience: "Produkt, support och webbansvarig"
feature_area: "FAQ"
keywords:
  - "faq"
  - "gap-analys"
  - "prioritering"
  - "in-app"
  - "webb"
last_verified_from_code: "2026-02-11"
source_refs:
  - "docs/user-manual/11-faq-anvandarversion.md"
  - "WsprPc/FaqWindow.xaml"
  - "WsprPc/FaqWindow.xaml.cs"
---
# FAQ gap-analys (för in-app)

Den här analysen visar vilka användarfrågor som bör läggas till eller förtydligas i en in-app FAQ, baserat på nuvarande manual.

| Fråga | Varför viktigt | Prioritet | Föreslagen plats (In-app FAQ/Webb/AQ) |
|---|---|---|---|
| Varför reagerar inte `F8`/`F9` i just mitt program? | Vanligt första-problem som stoppar hela nyttan direkt. | Hög | In-app FAQ |
| Hur byter jag mikrofon om fel mikrofon används? | Fel ljudkälla ger "ingen text" trots att appen verkar fungera. | Hög | In-app FAQ |
| Hur lång ljudfil kan jag köra i mötesläge? | Användaren behöver planera tid och undvika avbrutna jobb. | Hög | Webb |
| Vad gör jag när texten blir på fel språk? | Språkfel upplevs som låg kvalitet och minskar förtroendet snabbt. | Hög | In-app FAQ |
| Vad är skillnaden mellan "snabbast", "balanserat" och "högst kvalitet" i praktiken? | Hjälper användaren välja rätt utan att testa blint. | Hög | AQ |
| Hur mycket lagringsutrymme behöver modeller och historik? | Vanlig fråga i företagsmiljö och på mindre datorer. | Medel | Webb |
| Vad händer med texten om appen kraschar eller datorn startar om? | Viktigt för trygghet och förväntningar kring datatapp. | Medel | In-app FAQ |
| Hur flyttar jag till ny dator utan att förlora viktiga inställningar? | Praktisk vardagsfråga vid byte av arbetsdator. | Medel | Webb |
| Hur rensar jag historik säkert före delad dator/demo? | Integritetsfråga med tydligt användarbehov. | Medel | In-app FAQ |
| När ska jag använda `Auto` talare och när manuellt antal? | Auto fungerar normalt bra, men användaren behöver veta när manuellt läge ändå är motiverat. | Medel | In-app FAQ |
| Varför får jag olika resultat på samma fil mellan två körningar? | Minskar osäkerhet och supportärenden om "instabil kvalitet". | Medel | Webb |
| Vad kostar AI-läge ungefär per användning? | Styr förväntningar i team med budgetansvar. | Medel | Webb |
| Hur återställer jag appen till rekommenderade standardval? | Snabb väg tillbaka när man "skruvat bort sig". | Medel | In-app FAQ |
| Hur stänger jag av notifieringar eller störande banners tillfälligt? | Förbättrar arbetsro i möten och presentationer. | Låg | In-app FAQ |
| Var hittar jag en 60-sekunders snabbguide för nya kollegor? | Underlättar onboarding i team utan lång läsning. | Låg | AQ |

## Prioriterad rekommendation (första leverans)

1. Publicera de 5 högst prioriterade frågorna direkt i in-app FAQ/AQ.
2. Lägg till fördjupning på webben för filstorlek, lagring, kostnad och datorbyte.
3. Följ upp med supportdata efter 2-4 veckor och justera prioritering.

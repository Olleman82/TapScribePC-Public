---
title: "Appendix: Inställningar referens"
summary: "A-Ö referens över inställningar, effekt och rekommenderad användning."
audience: "Slutanvändare och support"
feature_area: "Referens"
keywords:
  - "inställningar"
  - "referens"
  - "effekt"
  - "standardvärden"
last_verified_from_code: "2026-02-11"
source_refs:
  - "WsprPc/AppConfig.cs"
  - "WsprPc/MainWindow.xaml"
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/BatchQueueWindow.xaml"
---
# Appendix: Inställningar (A-Ö)

Tabellen är sorterad A-Ö efter inställningsnamn.

| Inställning | Var den finns | Standard/typiskt värde | Effekt | Rekommenderad användning |
|---|---|---|---|---|
| AI-snabbtangent | Huvudvy -> `Snabbtangenter` | `F9` | Startar/stannar AI-läge vid håll-in/släpp | Behåll `F9` om du inte krockar med andra appar. |
| Använd tonhöjdsskydd | `Möten` -> `Avancerade inställningar` och per fil i `Batchkö` (`🛡️`) | På (`true`) | Minskar risk att olika rösttyper slås ihop | Låt vara på i nästan alla möten. |
| Autoklistra | Huvudvy -> `Snabbtangenter` | På (`true`) | Klistrar in direkttranskribering i målapp | På för snabb textinmatning, av om du vill granska först. |
| Detektera mötestyp | `Möten` och `Batchkö` | På (`true`) | Försöker avgöra fysiskt/digitalt möte och kan justera känslighet | På som standard, stäng av om utfallet blir sämre på ditt material. |
| Direkt snabbtangent | Huvudvy -> `Snabbtangenter` | `F8` | Startar/stannar direkttranskribering vid håll-in/släpp | Behåll `F8` eller välj tangent du lätt når. |
| Fysiskt möte: justering | `Möten` (visas när `Detektera mötestyp` är aktiv) | Typiskt `+0.10` (vissa versioner `+0.15`) | Adderas till känslighet vid fysiskt möte | Justera i små steg (`0.02-0.05`) och jämför resultat. |
| Känslighet (talarigenkänning) | `Möten` -> `Avancerade inställningar`, samt kolumn i `Batchkö` | Ca `1.15` | Lägre värde ger fler talare, högre ger färre | Börja med standard, justera sedan små steg. |
| Kortaste paus | `Möten` -> `Avancerade inställningar` | `0.10 s` | Styr hur lätt segment delas vid pauser | Sänk vid snabba talarbyten, höj om texten blir för upphackad. |
| Kortaste tal | `Möten` -> `Avancerade inställningar` | `0.15 s` | Filtrerar bort väldigt korta ljudbitar | Höj om brus blir "tal", sänk om korta repliker försvinner. |
| Spara automatiskt bredvid källfilen (.txt) | `Batchkö` | På (`true`) | Sparar resultat automatiskt per färdig fil | På för nattkörningar och större batchjobb. |
| Starta med Windows | Huvudvy -> `Snabbtangenter` | Av (`false`) | Startar appen vid inloggning | På om du använder appen dagligen. |
| Städning (min total taltid) | `Möten` -> `Avancerade inställningar` | `15 s` | Tar bort talare med mycket kort total taltid | Bra mot host/klick/spöksegment. Sänk bara vid behov. |
| Stäng appen när kört klart | `Batchkö` | Av (`false`) | Avslutar appen efter full batch utan fel/avbrott | På vid obevakad körning, av vid manuell övervakning. |
| Talare (Auto/1-10 i `Möten`, Auto/1-5 i `Batchkö`) | `Möten` och per fil i `Batchkö` | `Auto` | Ger diarization hint om antal talare | Börja med `Auto`. Välj manuellt antal när du vill låsa resultatet till känt antal. |

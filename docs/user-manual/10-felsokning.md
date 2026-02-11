---
title: "Felsökning"
summary: "Snabb felsökningsguide för vanliga problem och tydliga lösningssteg."
audience: "Slutanvändare"
feature_area: "Support"
keywords:
  - "felsökning"
  - "problem"
  - "lösning"
  - "f8"
  - "möten"
last_verified_from_code: "2026-02-11"
source_refs:
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/BatchQueueWindow.xaml.cs"
  - "WsprPc/HistoryWindow.xaml.cs"
  - "WsprPc/Services/Diarization/FileTranscriptionService.cs"
---
# 10. Felsökning

Använd tabellen nedan som snabbguide: symptom -> sannolik orsak -> steg-för-steg-lösning.

| Symptom | Sannolik orsak | Steg-för-steg-lösning |
|---|---|---|
| `Starta transkribering` är grå/inaktiv | Ingen ljudfil vald | 1. Klicka `Välj ljudfil`.<br>2. Välj en stödd fil (`mp3/wav/m4a/aac/mp4`).<br>3. Kontrollera att filnamnet visas i mötesvyn. |
| Banner om saknade modeller försvinner inte | Diarization-modeller/verktyg saknas eller nedladdning avbröts | 1. Klicka `Ladda ner modeller`.<br>2. Vänta tills status visar klart.<br>3. Starta om appen om bannern ligger kvar.<br>4. Kontrollera internet och försök igen. |
| Fel vid nedladdning av modeller | Nätverk, proxy eller brandvägg blockerar nedladdning | 1. Testa vanlig webbsurf i samma nät.<br>2. Stäng av VPN/proxy tillfälligt om policy tillåter.<br>3. Kör nedladdningen igen.<br>4. Om företagsnät blockerar: testa annat nät eller be IT tillåta GitHub/ffmpeg-källor. |
| Progress fastnar länge på samma procent | Tunga steg (talarigenkänning/segmenttranskribering) pågår | 1. Vänta längre, särskilt för långa filer.<br>2. Kontrollera att tiden (`⏱️`) fortsätter ticka.<br>3. Avbryt och testa kortare fil om du vill verifiera att flödet fungerar. |
| För många talare i resultatet | För låg känslighet eller för låg städning | 1. Höj `Känslighet` lite.<br>2. Höj `Städning`.<br>3. Ange manuellt antal talare i stället för `Auto`.<br>4. Kör om. |
| För få talare (röster slås ihop) | För hög känslighet eller fel antal talare | 1. Sänk `Känslighet` lite.<br>2. Ange korrekt manuellt talarantal.<br>3. Låt `Använd tonhöjdsskydd` vara på.<br>4. Kör om och jämför. |
| En kort talare "försvinner" | Svag/kort talare filtreras bort av inställningar eller segmentering | 1. Behåll `Auto` och sänk `Städning` något.<br>2. Sänk `Kortaste tal` lite.<br>3. Om problemet kvarstår: testa manuellt talarantal.<br>4. Kör om. |
| Fysiskt möte får sämre resultat än digitalt | Mötestypsjustering passar inte just inspelningen | 1. Testa att slå av `Detektera mötestyp` och kör om.<br>2. Alternativt justera `Fysiskt möte: +...` stegvis.<br>3. Jämför två körningar och behåll bästa varianten. |
| Realtidsläge (F8/F9) fungerar inte under mötestranskribering | Realtid blockeras medan möteskörning pågår | 1. Vänta tills möteskörningen är klar.<br>2. Eller klicka `Avbryt` i mötesvyn.<br>3. Kör sedan realtid igen. |
| Batch stänger inte appen trots vald `Stäng appen när kört klart` | Minst en fil fick `Fel` eller `Avbruten` | 1. Kontrollera status per rad.<br>2. Åtgärda felande filer.<br>3. Kör om batchen.<br>4. Funktionen stänger appen först när alla jobb slutförts utan fel/avbrott. |
| Autosparade `.txt` saknas efter batch | `Spara automatiskt...` av eller skrivproblem i målmapp | 1. Bekräfta att `Spara automatiskt...` är ikryssad.<br>2. Kontrollera skrivbehörighet i källfilens mapp.<br>3. Sök efter filer med suffix som innehåller tröskel/pitch/talare.<br>4. Kör om en fil som test. |
| Historiken ser tom ut trots tidigare körningar | Filter döljer poster | 1. Sätt `Datum = Alla`.<br>2. Sätt `Typ = Alla`.<br>3. Töm sökrutan.<br>4. Kontrollera posträknaren igen. |
| Det går inte att ta bort historikposter | Inget markerat eller rensning inte bekräftad | 1. Markera minst en rad (eller välj `Ta bort alla`).<br>2. Bekräfta dialogen.<br>3. Kontrollera att räknaren uppdateras efteråt. |

## Om problemet kvarstår

1. Testa samma fil med standardinställningar.
2. Testa en kortare fil (1-3 minuter) för att isolera felet.
3. Dokumentera exakt symptom, filformat och vilka inställningar du använde.

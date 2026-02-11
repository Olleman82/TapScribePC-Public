---
title: "Frontmatter-schema för användarmanual"
summary: "Obligatoriska metadatafält för konsekvent, sökbar och verifierbar användardokumentation."
audience: "Dokumentationsansvariga och support"
feature_area: "Dokumentation"
keywords:
  - "frontmatter"
  - "schema"
  - "metadata"
  - "spårbarhet"
last_verified_from_code: "2026-02-09"
source_refs:
  - "WsprPc/MainWindow.xaml.cs"
  - "WsprPc/AppConfig.cs"
---

# Frontmatter-schema

Alla sidor i användarmanualen ska ha frontmatter överst i filen. Fälten nedan är obligatoriska.

## Obligatoriska fält

| Fält | Typ | Vad det ska innehålla |
|---|---|---|
| `title` | text | Sidans namn, skrivet för användare. |
| `summary` | text | Kort förklaring av vad sidan hjälper användaren med. |
| `audience` | text | Vem sidan är till för, till exempel "Slutanvändare" eller "Support". |
| `feature_area` | text | Vilket område i appen sidan gäller, till exempel "Diktering" eller "Historik". |
| `keywords` | lista | 3-8 sökord som gör sidan lätt att hitta. |
| `last_verified_from_code` | datum (`YYYY-MM-DD`) | Datum då sidan senast jämfördes mot aktuell kod. |
| `source_refs` | lista | En lista med konkreta filvägar i `WsprPc` som ligger till grund för innehållet. |

## Mall att kopiera

```yaml
---
title: ""
summary: ""
audience: ""
feature_area: ""
keywords:
  - ""
last_verified_from_code: "YYYY-MM-DD"
source_refs:
  - "WsprPc/MainWindow.xaml.cs"
---
```

## Kvalitetskrav

- Skriv för användare, inte för utvecklare.
- Bekräfta alltid innehåll mot verkliga filer innan publicering.
- Uppdatera `last_verified_from_code` samma dag som kontrollen görs.
- Lägg bara in `source_refs` som faktiskt stöder sidans innehåll.

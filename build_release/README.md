# TapScribe 🎙️

**TapScribe** är ett professionellt verktyg för offline-transkribering på Windows, särskilt optimerat för det **svenska språket**. Genom att kombinera kraften i [whisper.cpp](https://github.com/ggerganov/whisper.cpp) med [KBLabs svenska Whisper-modeller](https://huggingface.co/KBLab) erbjuder TapScribe en oöverträffad precision och hastighet direkt på din lokala maskin.

Appen är byggd för yrkesverksamma som behöver snabb, säker och privat diktering utan att skicka känslig data till molnet.

---

## ✨ Nyckelfunktioner

- **Svenska i Fokus**: Speciellt anpassad för svenska språket genom integration av KBLabs finlirade modeller.
- **Blixtsnabb Transkribering**: Optimerad för lokal CPU-exekvering via Whisper GGUF.
- **Hold-to-Talk**: Enkelt arbetsflöde—håll ner en knapp för att prata, släpp för att klistra in texten direkt där din markör befinner sig.
- **Integritet & Säkerhet**: All transkribering sker lokalt. Inget ljud lämnar någonsin din dator.
- **AI-Polering**: Valfri integration med Gemini eller OpenAI för att städa bort tveksamheter, fixa grammatik och formatera texten proffsigt.
- **Auto-Tune Engine**: Automatisk benchmarking av din hårdvara för att hitta optimala inställningar (thread count) för din PC.
- **Modern Design**: Designad för Windows 11 med mörkt läge och smidig integration i systemfältet (tray).

---

## 🚀 Kom igång

### 1. Installation
Ladda ner den senaste versionen från [GitHub Releases](https://github.com/Olleman82/TapScribePC-Public/releases).
- **Setup.exe**: Standardinstallation för Windows.
- **Portable.zip**: Packa upp och kör—kräver ingen installation.

> [!NOTE]
> Som ett oberoende projekt visas ofta varningen "Windows har skyddat din dator" vid första start. Detta beror på att appen inte är digitalt signerad. Klicka på **Mer info** och sedan **Kör ändå**.

### 2. Konfiguration
Vid första start guidar TapScribe dig genom:
- Val av Whisper-modell (KBLabs svenska GGUF-modeller rekommenderas starkt).
- **Auto-Tune** för att maximeras prestanda på just din dator.
- (Valfritt) Inmatning av AI-nycklar för avancerad textbearbetning.

---

## 🛠️ Teknisk Stack

- **Frontend**: C# / WPF (.NET 8) med modernt gränssnitt.
- **Motor**: [whisper.cpp](https://github.com/ggerganov/whisper.cpp) via CLI/DLL-integration.
- **Kärnlogik**:
    - Anpassad VAD (Voice Activity Detection) för att filtrera bort tystnad.
    - Globala system-hooks för lyhörd hantering av kortkommandon.
    - Intelligent prompt-hantering för AI-bearbetning.

---

## 📜 Licens

Projektet är licensierat under **PolyForm Noncommercial License 1.0.0**.

- ✅ **Gratis för personligt bruk**, forskning och hobbyprojekt.
- ✅ **Gratis för icke-kommersiella organisationer** (välgörenhet, skolor, myndigheter).
- ❌ **Kommersiell användning eller vidaredistribution** är förbjuden utan tillstånd.

Se [LICENSE.md](LICENSE.md) för fullständig juridisk text.

---

## 👨‍💻 Om Upphovsmannen

**Olle Söderqvist**  
AI-konsult specialiserad på praktisk implementation för svenska företag.

Med över 10 års erfarenhet av digitalisering brinner jag för att göra AI tillgängligt och värdeskapande i vardagen. TapScribe är ett exempel på hur lokal, specialiserad AI kan öka produktiviteten utan att kompromissa med integritet.

Besök gärna [aiolle.se](https://aiolle.se) för mer info om mina tjänster och projekt.

[LinkedIn](https://www.linkedin.com/in/ollesoderqvist/) | [YouTube](https://www.youtube.com/@AI-Olle) | [GitHub](https://github.com/Olleman82)

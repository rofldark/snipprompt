<div align="center">

<img src="docs/img/icon.png" width="96" alt="SnipPrompt-Symbol">

# SnipPrompt

**Bildschirmbereich ausschneiden und direkt in die gewählte Claude-Code-Session legen – ein Tastenkürzel, kein Kopieren und Einfügen.**

![Plattform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8%20(eingebaut)-512BD4)
![Abhängigkeiten](https://img.shields.io/badge/dependencies-none-brightgreen)
![Lizenz](https://img.shields.io/badge/license-MIT-blue)

[English](README.md) · [**Download**](https://github.com/rofldark/snipprompt/releases/latest) · [Schnellstart](#-schnellstart) · [Funktionen](#-funktionen) · [Einstellungen](#%EF%B8%8F-einstellungen) · [Grenzen](#-bekannte-grenzen)

<img src="docs/img/demo.gif" width="860" alt="SnipPrompt-Demo: ausschneiden, markieren, Session wählen, Bild landet im Claude-Code-Prompt">

</div>

## Warum

Um Claude Code etwas zu zeigen, heißt es meist: Snipping Tool öffnen, aufnehmen, kopieren, ins Terminal wechseln, einfügen.
SnipPrompt macht daraus **ein Tastenkürzel und einen Rahmen**. Es sitzt neben der Uhr, findet deine Claude-Code-Sessions
im Windows Terminal und fügt den Screenshot in die gewählte ein – bereit für deine Frage.

## ⬇️ Download

**[SnipPrompt.exe aus dem neuesten Release laden](https://github.com/rofldark/snipprompt/releases/latest)** – eine einzelne Datei (~115 KB),
kein Installer. Irgendwo ablegen (z. B. `C:\Tools\SnipPrompt\`) und doppelklicken.

> [!IMPORTANT]
> **Windows SmartScreen warnt beim ersten Start.** Die exe ist nicht signiert (ein Zertifikat kostet jedes Jahr Geld),
> deshalb zeigt Windows *„Der Computer wurde durch Windows geschützt“*. Auf **Weitere Informationen** → **Trotzdem ausführen** klicken.
> Das ist nur einmal nötig. Wer mag, prüft die SHA-256-Prüfsumme im Release oder [baut selbst](#-schnellstart) aus dem Quellcode.

## ✨ Funktionen

- **Ein Tastenkürzel** (`Strg+Alt+S`, änderbar), Klick auf das Symbol neben der Uhr oder auf das angeheftete Taskleisten-Symbol
- **Rahmen oder Fenster** – Rahmen über alle Monitore ziehen oder ein Fenster anklicken
- **Markieren** – `Umschalt` beim Aufziehen halten: Rechteck, Pfeil, Marker, Text (`Strg+Z` macht rückgängig)
- **Auswahlfenster** – große Vorschau, Live-Status je Session (*arbeitet* / *bereit*), zuletzt genutzte vorausgewählt,
  Ziffern `1–9` senden sofort. Ist nur eine Claude-Code-Session offen, wird direkt eingefügt
- **Textvorlagen** – optional „Was ist das?“ oder eigene Einzeiler nach dem Bild
- **Verzögerung** – 1/2/3/5 s *nach* dem Aufziehen, um im Bereich ein Menü oder Tooltip zu öffnen
- **Drückt nie Enter** – du prüfst den Prompt und sendest selbst
- **Hell, dunkel oder wie Windows**, Oberfläche **Deutsch und Englisch**, optional Start mit Windows
- **Ohne Installation, ohne Abhängigkeiten** – gebaut mit dem C#-Compiler, der in Windows steckt

## 🚀 Schnellstart

Voraussetzungen: Windows 10/11, [Windows Terminal](https://aka.ms/terminal) und Claude Code darin.

Entweder [die exe laden](#%EF%B8%8F-download) und starten oder selbst bauen:

```powershell
git clone https://github.com/rofldark/snipprompt.git
cd snipprompt
powershell -ExecutionPolicy Bypass -File build.ps1
bin\SnipPrompt.exe
```

Das Symbol erscheint neben der Uhr, die erste Aufnahme startet sofort.
Optional: Rechtsklick auf `bin\SnipPrompt.exe` → *An Taskleiste anheften* und in den Einstellungen *Mit Windows starten*.

### Ablauf

1. `Strg+Alt+S` drücken (oder Symbol neben der Uhr / in der Taskleiste anklicken).
2. Rahmen ziehen – im Fenster-Modus ein Fenster anklicken. `Esc` oder Rechtsklick bricht ab.
   Mit gedrückter `Umschalt`-Taste loslassen, um vorher zu markieren.
3. Eine Claude-Code-Session offen → sofort eingefügt. Mehrere → auswählen (`↑ ↓` + `Enter`, `1–9`, Doppelklick
   oder Knopf *Senden*); `Tab` wechselt die Vorlage.
4. Frage dazuschreiben und selbst `Enter` drücken.

## ⚙️ Einstellungen

Rechtsklick auf das Symbol neben der Uhr → *Einstellungen*. Jede Änderung wird sofort gespeichert.

| Bereich | Optionen |
|---|---|
| Allgemein | Tastenkürzel · Sprache (Automatisch / Deutsch / English) · Design (Hell / Dunkel / System) · Mit Windows starten |
| Aufnahme | Rahmen oder Fenster · Verzögerung (aus / 1 / 2 / 3 / 5 s) · Markieren mit Umschalt · Kopien behalten (keine / 20 / 100) |
| Auswahlfenster | Vorschau · zuletzt genutzte Session vorauswählen · bei einer Session direkt senden · Vorlagen anzeigen |
| Vorlagen | eigene einzeilige Texte, die nach dem Bild eingetippt werden |

Einstellungen: `%APPDATA%\SnipPrompt\settings.json`, Screenshot-Kopien: `%LOCALAPPDATA%\SnipPrompt\`.

## 🛡️ Sicherheitsregeln

- Tasten gehen nur raus, wenn das Zielfenster vorne **und** der Ziel-Tab ausgewählt ist
- in Tabs, die nicht nach Claude Code aussehen, wird nie ungefragt eingefügt
- es wird gewartet, bis `Enter`, `Umschalt`, `Strg`, `Alt` und `Win` losgelassen sind
- nie `Enter`; Vorlagen sind einzeilig, Steuerzeichen werden entfernt
- passt etwas nicht, wird nichts getippt: Das Bild liegt in der Zwischenablage (`Alt+V` von Hand), ein Hinweis erscheint

## 📌 Bekannte Grenzen

- Nur Windows Terminal (keine klassische Konsole, noch kein VS-Code-Terminal).
- Claude-Code-Sessions werden am Statuszeichen vor dem Tab-Titel erkannt (`✳` im Leerlauf, Spinner beim Arbeiten).
  Ändert sich das Format, muss die Erkennung angepasst werden (`Logic.Markers`).
- Liest Claude Code die Zwischenablage langsam, kann eine Vorlage vor dem Platzhalter `[Image #1]` landen.
- Die exe ist nicht signiert; Windows SmartScreen fragt einmal nach (*Weitere Informationen* → *Trotzdem ausführen*), siehe [Download](#%EF%B8%8F-download).

## 🧪 Entwicklung

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -Test     # Tests (Logik, Einstellungen, Übersetzungen)
bin\SnipPrompt.exe --list                                     # erkannte Terminal-Tabs (* = Claude Code)
bin\SnipPrompt.exe --snapshots <Ordner> [en|de]               # Auswahlfenster und Einstellungen als PNG
bin\SnipPrompt.exe --grab-check                               # Aufnahme nur im Speicher, meldet Größe
```

## ⚖️ Rechtliches

SnipPrompt ist ein unabhängiges Open-Source-Projekt und steht in keiner Verbindung zu Anthropic.
„Claude“ und „Claude Code“ sind Marken von Anthropic. Inspiriert von PaneDrop für macOS.

## 📄 Lizenz

[MIT](LICENSE) © 2026 rofldark

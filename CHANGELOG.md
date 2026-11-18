# Changelog

Alle nennenswerten Änderungen an diesem Projekt. Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
Versionierung nach [Semantic Versioning](https://semver.org/lang/de/).

## [Unreleased]

### Hinzugefügt
- Fertige `SnipPrompt.exe` als GitHub-Release (v0.2.0), README mit Download-Abschnitt und SmartScreen-Hinweis.

## [0.2.0] - 2026-11-18
### Geändert
- Projekt heißt jetzt **SnipPrompt** (vormals ClaudeDrop). Vorhandene Screenshots werden beim ersten Start übernommen;
  die exe muss einmal neu an die Taskleiste geheftet werden.
- Läuft dauerhaft im Hintergrund mit Symbol neben der Uhr; ein zweiter Start (z. B. Taskleisten-Symbol) löst nur eine Aufnahme aus.
- Neues, modernes Auswahlfenster: große Vorschau mit Pixelmaßen, Live-Status je Session („arbeitet“/„bereit“),
  zuletzt genutzte Session vorausgewählt, Fensternummer bei mehreren Terminal-Fenstern, Knopf „Senden“ (wie Enter).
- Hinweise erscheinen als kleine Benachrichtigung unten rechts statt als Windows-Meldung.
- Aufnahme-Overlay zeichnet beim Aufziehen nur noch geänderte Bereiche neu und nimmt Tooltips/Popups mit auf.

### Hinzugefügt
- Globales Tastenkürzel (Vorgabe `Strg+Alt+S`, änderbar).
- Markieren mit gedrückter Umschalt-Taste: Rechteck, Pfeil, Marker, Text, Farben, Rückgängig.
- Fenster-Modus (Fenster anklicken statt Rahmen ziehen) und Verzögerung 1/2/3/5 s mit Countdown: erst Bereich wählen,
  dann bleibt Zeit, im Bereich ein Menü oder Tooltip zu öffnen, danach wird der Bereich live aufgenommen.
- Textvorlagen, die nach dem Bild ins Eingabefeld getippt werden (nie Enter).
- Einstellungsfenster: Tastenkürzel, Sprache Deutsch/English, Design hell/dunkel/System, Autostart,
  Aufnahmeart, Verzögerung, Markieren, Anzahl Kopien, Optionen des Auswahlfensters, Vorlagen.
- Entwicklerschalter `--snapshots <Ordner>` (Oberflächen als PNG).

## [0.1.0] - 2026-11-18
### Hinzugefügt
- Bereichsauswahl über alle Monitore (abgedunkelter, eingefrorener Bildschirm, Größenanzeige, Esc/Rechtsklick bricht ab).
- Erkennung der Claude-Code-Tabs im Windows Terminal anhand des Tab-Titels.
- Automatisches Einfügen per `Alt+V` bei genau einer Session, Auswahlliste bei mehreren.
- Ablage der Screenshots unter `%LOCALAPPDATA%\ClaudeDrop\` (neueste 20).
- Debug-Schalter `--list`.

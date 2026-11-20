# CYD Flags – die einfache Anleitung

Diese Anleitung ist absichtlich Schritt für Schritt geschrieben. Du brauchst
**keine** Programmierkenntnisse und musst **nichts** im Code ändern.

Am Ende zeigt dein „Cheap Yellow Display“ (CYD):

- im **Rennen**: die Fahrer direkt vor und hinter dir (Platz, Name, Abstand
  und Rundenzeit) und oben links klein die Uhrzeit. Oben steht **LAST** oder
  **BEST** – das stellst du im Plugin ein.
- im **Rennen** zusätzlich: eine **Racelogic-Seite** mit deiner aktuellen
  Runde, dem Delta zur besten Runde und den drei Sektoren (grün = schneller
  als deine Bestzeit, rot = langsamer).
- außerhalb des Rennens (Menü, Training, Qualifying): **die echte Uhrzeit**,
- wenn eine Flagge kommt: die Flagge groß auf dem ganzen Bildschirm,
- eine **Rev-Limiter-Seite** mit großem Gang, km/h und einer RPM-Leiste, die
  an der Grenze rot blinkt (auch im Training/Qualifying),
- **Tippen** auf die unteren Ecken: unten rechts = nächste Seite, unten links =
  zurück. Reihenfolge: Turm → Racelogic → Rev-Limiter → Uhr → Turm. Im
  standardmäßig gibt es alle Seiten auch im Training/Qualifying; mit der
  Race-Pages-Option nur Rev-Limiter und Uhr. Flaggen haben Vorrang,
- eine **Alarm-Uhr**: oben links auf der Uhr tippen und mit den vier Ecken
  Stunden/Minuten stellen, speichern und scharf schalten,
- die IP-Adresse nur in den ersten 15 Sekunden nach dem Start.

Du kannst **mehrere Displays** gleichzeitig betreiben. Sie zeigen alle dieselben
Renndaten; Seitenauswahl, Helligkeit und WLAN richtest du pro Display ein.

Wenn das Display **kein WLAN** hat, zeigt es Uhrzeit und **NO WIFI** (im
Einrichtungsmodus **WIFI: SETUP AP**). Hat es WLAN, aber SimHub läuft nicht oder
der PC ist aus, steht dort **NO SIMHUB**. Sind beide Verbindungen da, wird
keine dieser Meldungen angezeigt – dann steht dort das normale Datum.

---

## Was du brauchst

| Ding | Warum |
|------|-------|
| CYD „ESP32-2432S028R“ (ein oder zwei USB-Anschlüsse) | Das ist das Display mit dem 2,8-Zoll-Bildschirm |
| USB-Kabel, das **Daten** überträgt (kein reines Ladekabel) | Zum ersten Flashen |
| PC mit Windows und installiertem SimHub | SimHub liest die Flaggen aus dem Spiel |
| WLAN mit **2,4 GHz** | Der ESP32 kann kein 5-GHz-WLAN |
| Chrome oder Edge | Zum Flashen über den Browser |

> **Wichtig:** Es gibt mehrere CYD-Varianten mit unterschiedlichem
> Bildschirm-Chip. Wenn dein Display nach dem Flashen weiß bleibt, Streifen
> zeigt oder die Farben verkehrt sind, nimm die andere Datei aus Abschnitt 1.1.

---

## Teil 1: Display flashen (ca. 5 Minuten)

### 1.1 Datei herunterladen

Du brauchst **eine** dieser drei Dateien aus dem Ordner `dist/`. Welche, hängt
vom Bildschirm-Chip deines CYDs ab:

- **[dist/cyd-flags-portrait-7789.factory.bin](dist/cyd-flags-portrait-7789.factory.bin)**
  – für die meisten neueren CYDs mit **zwei** USB-Anschlüssen (ST7789V).
- **[dist/cyd-flags-portrait-ili9341.factory.bin](dist/cyd-flags-portrait-ili9341.factory.bin)**
  – für das ursprüngliche CYD mit **einem** USB-Anschluss (ILI9341).
- **[dist/cyd-flags-portrait-9342.factory.bin](dist/cyd-flags-portrait-9342.factory.bin)**
  – für die seltenere ILI9342-Variante.

Wenn das Bild nach dem Flashen falsch aussieht (Streifen, gespiegelt, falsche
Farben), flash einfach die nächste Datei. Die Firmware ist sonst identisch.
Das ist die fertige Firmware – du musst dafür kein ESPHome installieren. Die
Dateien starten im Hochformat; im Plugin kannst du pro Display auch Querformat
einstellen, ohne neu zu flashen.

### 1.2 Browser öffnen

Öffne in **Chrome oder Edge**:

**https://web.esphome.io**

(Feuerfuchs und Safari funktionieren nicht.)

### 1.3 Display anschließen

1. Schließe das CYD mit dem USB-Kabel an den PC an.
2. Warte 2–3 Sekunden.
3. Klicke auf der Webseite auf **„Connect“**.
4. Es öffnet sich ein Fenster mit einer Liste von Anschlüssen (COM3, COM4, …).
   Wähle den Eintrag, der nach dem Einstecken neu dazugekommen ist.
5. Bestätige mit **„Connect“**.

Wenn **kein** COM-Anschluss auftaucht:

- Nimm ein anderes USB-Kabel (viele Kabel können nur laden).
- Steck das Kabel direkt in den PC, nicht in einen Hub.
- Installiere den **CH340-Treiber** (Windows): https://www.wch-ic.com/downloads/CH341SER_EXE.html
- Starte den PC danach neu.

Wenn der Flashvorgang abbricht oder „Failed to connect“ kommt:

- Halte die **BOOT**-Taste am CYD gedrückt, klicke noch einmal auf **Connect**
  und lasse die Taste erst los, wenn der Vorgang startet.
- Danach einmal die **RST**-Taste drücken.

### 1.4 Firmware installieren

1. Klicke auf **„Install“**.
2. Wähle **„Choose file“** und dann die heruntergeladene Datei
   `cyd-flags-portrait-….factory.bin`.
3. Klicke auf **„Install“** und warte, bis der Balken durchgelaufen ist.
   Das dauert etwa 1–2 Minuten. **Kabel nicht abziehen!**

> **Hinweis:** Ein Factory-Flash löscht die gespeicherten WLAN-Daten. Deshalb
> kommt danach wieder die WLAN-Abfrage (oder das Fallback-WLAN). Ein
> **OTA-Update** löscht das WLAN **nicht**: Dabei wird nur die Firmware-Partition
> überschrieben, die WLAN-Zugangsdaten bleiben im NVS-Speicher erhalten. Wenn du
> nur eine neue Firmware-Version willst und das Display schon im WLAN ist, nimm
> also die passende `.ota.bin` über OTA – nicht die `.factory.bin` per USB.

**WLAN direkt jetzt eingeben:** Die Firmware unterstützt „Improv“. Sobald das
Flashen fertig ist, fragt ESPHome Web meistens direkt nach deinem WLAN:

- **Netzwerkname (SSID):** dein normales **2,4-GHz-WLAN**
- **Passwort:** dein WLAN-Passwort

Trage beides dort ein und bestätige. Damit ist das Display sofort im WLAN –
du brauchst kein Fallback-WLAN. Das ist der einfachste Weg.

Wenn diese Abfrage **nicht** kommt (anderer Browser, Treiber, ältere
ESPHome-Web-Version): Kabel abziehen, wieder anstecken und den Abschnitt
1.5 benutzen.

Der Bildschirm leuchtet jetzt. Es kann sein, dass noch **NO WIFI**, **NO SIMHUB** oder
`--:--` angezeigt wird. Das ist normal, bis SimHub läuft und die Uhrzeit
synchronisiert ist.

### 1.5 Fallback-WLAN (nur falls die Abfrage nicht kam)

Das Display öffnet dann sein eigenes WLAN:

1. Warte etwa **20 Sekunden** nach dem Neustart.
2. Gehe am Handy oder Laptop in die WLAN-Einstellungen.
3. Verbinde dich mit dem WLAN:

   **Name:** `CYD Flags fallback`  
   **Passwort:** `cyd-fallback`

4. Es sollte automatisch eine Einrichtungsseite aufgehen. Wenn nicht, öffne
   im Browser: **http://192.168.4.1**
5. Wähle dein normales **2,4-GHz-WLAN** aus der Liste.
6. Gib das WLAN-Passwort ein und speichere.
7. Das Display verbindet sich mit deinem WLAN (eventuell startet es kurz neu).

Auf dem Display steht jetzt für 15 Sekunden eine IP-Adresse (im Uhr-Bildschirm
unten, im Rennen oben rechts), zum
Beispiel `192.168.1.42`. **Schreib sie dir auf.** Du brauchst sie gleich.
(Vielleicht musst du dein Handy wieder mit dem normalen WLAN verbinden.)

Wenn dort `no wifi` steht: WLAN-Passwort prüfen, 2,4-GHz-Netz wählen, näher
am Router stehen und es noch einmal versuchen.

---

## Teil 2: SimHub-Plugin installieren (ca. 3 Minuten)

### 2.1 SimHub schließen

SimHub muss **komplett geschlossen** sein, sonst lässt es sich nicht
überschreiben.

### 2.2 SimHub-Ordner finden

1. Drücke die Windows-Taste und tippe **SimHub** ein.
2. Rechtsklicke auf **SimHub**.
3. Wähle **„Dateispeicherort öffnen“** (Windows 11) bzw.
   **„Dateipfad öffnen“** (Windows 10).

Der Ordner enthält `SimHubWPF.exe`. Meist ist es:

`C:\Program Files (x86)\SimHub`

### 2.3 Plugin hineinkopieren

Kopiere diese Datei:

**[dist/CydFlags.dll](dist/CydFlags.dll)**

in genau diesen SimHub-Ordner, also neben `SimHubWPF.exe`.

Wenn Windows nach Administratorrechten fragt: **Ja** klicken.

### 2.4 SimHub starten

Starte SimHub normal. Links in der Leiste taucht jetzt **CYD Flags** auf.

Falls nicht: SimHub noch einmal komplett beenden und neu starten. Im Zweifel
die DLL löschen und noch einmal kopieren.

### 2.5 Verbindung einstellen

1. Klicke links auf **CYD Flags**.
2. Setze den Haken bei **„Enable CYD Flags (all displays)“**.
3. Im Bereich **Displays** steht beim ersten Start bereits **Display 1**.
   Wähle es im Auswahlfeld aus.
4. Trage bei **„Host or IP“** die IP-Adresse ein, die das Display in den ersten
   15 Sekunden nach dem Start unten anzeigt, z. B. `192.168.1.42`.
5. **Port** bleibt `80`, setze den Haken bei **„Enable this display“**.
6. Klicke auf **„Test connection (all displays)“**.

Oben in den Einstellungen muss jetzt **LINK online** (grün) stehen. Die
Statuszeile bezieht sich immer auf das Display, das im Auswahlfeld ausgewählt
ist.

**Weiteres Display hinzufügen:** Klicke auf **„Add device“**, wähle den neuen
Eintrag (**Display 2**) aus und trage dessen eigene IP-Adresse ein. Jedes Display
bekommt dieselben Renndaten, aber eigene Seiten, Helligkeit und WLAN-Daten.
**„Remove device“** löscht das ausgewählte Display; das letzte verbliebene
Display kann nicht gelöscht werden.

Bei einem Update von Version 0.6.0 werden die alten Einzel-Display-Einstellungen
automatisch als **Display 1** übernommen – du musst nichts neu eintragen.

Optional: Unter **Backlight** kannst du **„Turn backlight off (0%) while no
game is running“** aktivieren. Dann wird das Display dunkel, sobald kein Spiel
läuft, und beim Spielstart wieder auf den eingestellten Wert gedreht. Das gilt
pro Display. Aktiviere es erst, wenn die Verbindung steht – im dunklen Zustand
zeigt das Display seine IP nicht an.

Wenn **offline** steht:

- Ist das richtige Display im Auswahlfeld gewählt?
- Ist das Display eingeschaltet und im selben WLAN wie der PC?
- Stimmt die IP? Sie kann sich nach einem Router-Neustart ändern.
- Funktioniert `http://<IP>` im Browser des PCs? Dann ist das Display erreichbar.
- Firewall kurz testweise ausschalten (nur zum Testen).

Ein nicht erreichbares Display blockiert die anderen nicht: Das Plugin versucht
es weiter, die übrigen Displays werden normal aktualisiert.

### 2.6 Seiten auswählen, WLAN speichern, Flaggen testen

Diese Bereiche bearbeiten immer das im Auswahlfeld gewählte Display:

- **Pages on this display:** Hake an, was dieses Display können soll:
  **Flags**, **Clock**, **Relative tower**, **Race logic** und **Rev
  limiter**. Was du abwählst, wird bei der Ecken-Navigation übersprungen. Wenn
  du alles abwählst, zeigt das Display **NO PAGE**. Flaggen haben weiterhin
  Vorrang.
- **Display orientation:** **Portrait (240×320)** oder **Landscape (320×240)**
  pro Display. Das Plugin dreht die Anzeige zur Laufzeit; du brauchst keine
  andere Firmware-Datei. Die Einstellung gilt für das ganze Display, nicht für
  einzelne Seiten.
- **Only show tower/logic/delta during a race:** globale Option. Standardmäßig
  **aus**, damit Turm, Racelogic und Delta auch im Training/Qualifying
  funktionieren. Einschalten, wenn außerhalb des Rennens nur die große Uhr
  erscheinen soll.
- **Race tower: show best lap instead of last lap:** pro Display wählbar, ob
  der Turm **LAST** oder **BEST** anzeigt.
- **WiFi on this display:** Trage **SSID** und **Passwort** ein und klicke
  **Save WiFi on this display and connect**. Das Display speichert die Daten
  dauerhaft im ESP32 (NVS). Sie überleben also **OTA-Updates** und einen
  Neustart. Nur ein kompletter **Factory-Flash per USB** löscht sie wieder.
  Das Passwort wird **nicht** auf dem PC gespeichert.
- **Test the flags:** Wähle eine Flagge und klicke **Show on all displays for
  10 seconds**. Alle aktivierten Displays zeigen sie zehn Sekunden lang, auch
  ohne laufendes Spiel. Mit **Stop test** geht es sofort zurück zu den normalen
  SimHub-Daten.

**Open display web UI** öffnet die ESPHome-Seite des ausgewählten Displays
direkt im Browser, damit du die IP nicht abtippen musst.

### 2.7 Fertig

Starte dein Spiel. SimHub erkennt das Spiel, das Plugin schickt Flaggen und die
relativen Fahrer zu allen aktivierten Displays.

- **Rennen:** Timing-Turm (vor dir, du, hinter dir) + kleine Uhr oben links.
  Der Abstand wird **grün**, wenn du näher kommst, und **rot**, wenn der
  Vordermann wegfährt. Beim Hintermann ist es umgekehrt. Die Rundenzeit hat
  einen farbigen Hintergrund: **violett** = schnellste Runde im Feld, **rot** =
  schneller als deine Runde, **grün** = langsamer. Deine eigene Zeile zeigt
  keinen Abstand (0.00 wäre sinnlos) und ist nur violett, nie rot/grün.
  Im Plugin kannst du zwischen **LAST** (letzte Runde) und **BEST** (beste
  Runde) umschalten. Bist du in der Boxengasse, steht oben zusätzlich **PIT**.
  **Tippen** auf die unteren Ecken schaltet weiter: **Turm → Racelogic →
  Rev-Limiter → große Uhr → Turm**. Standardmäßig funktionieren alle Seiten
  auch im Training/Qualifying; mit *Only show tower/logic/delta during a race*
  dort nur Rev-Limiter und Uhr. Eine Flagge hat immer Vorrang.
  Die Racelogic-Seite zeigt deine aktuelle Runde, das Delta zur persönlichen
  Bestzeit und die drei Sektoren (grün = schneller, rot = langsamer als deine
  Bestzeit; noch nicht gefahrene Sektoren bleiben neutral). Der Rev-Limiter
  zeigt Gang, km/h und die RPM-Leiste rot blinkend an der Grenze. Die Uhrzeit
  oben links ist im Rennen klein; außerhalb groß mit Datum. Oben links auf der
  Uhr öffnest du den **Alarm**: vier Ecken = Stunde +1, Minute +5, aus, speichern.

> **Startampel:** SimHub stellt keine Startlichter/Countdown-Daten bereit, daher
> kann die Anzeige keine echte Ampel zeigen. Die **PIT**-Anzeige ist nur ein
> Hinweis, dass du gerade in der Boxengasse bist – keine offizielle Flagge.
- **Menü / Training / Qualifying:** große Uhrzeit + Datum
- **Flagge aktiv:** große Flaggenanzeige (hat Vorrang)
- **Kein WLAN:** Uhrzeit + **NO WIFI** (oder **WIFI: SETUP AP**)
- **WLAN da, SimHub aus / keine Verbindung:** Uhrzeit + **NO SIMHUB**

---

## Teil 3: Wenn etwas nicht klappt

| Problem | Lösung |
|---------|--------|
| Display bleibt **schwarz** | USB-Kabel prüfen (Datenkabel!), neu einstecken. Kurz warten – der ESP32 braucht ein paar Sekunden. |
| Display **rebootet ständig** / im Log steht `Guru Meditation` | Veraltete Firmware. Aktuelle Datei aus Abschnitt 1.1 neu flashen, danach RST drücken. |
| Display **weiß**, **Streifen** oder falsche Farben | Falscher Bildschirm-Chip. Eine der anderen beiden Dateien aus Abschnitt 1.1 flashen. |
| Bild steht **auf dem Kopf** | Im Plugin unter **Display orientation** das andere Format wählen. Wenn es dann immer noch kopfüber ist, hilft nur eigene Firmware mit `display_rotation: "270"`. |
| **NO WIFI** auf dem Display | Display ist nicht im WLAN. Router/SSID prüfen, näher stellen oder WLAN im Plugin neu speichern. **WIFI: SETUP AP** heißt: Einrichtungsmodus aktiv – mit `CYD Flags fallback` verbinden und `http://192.168.4.1` öffnen. |
| **NO SIMHUB** auf dem Display | WLAN ist da, aber der Heartbeat kommt nicht an. Bei 0.7.5 konnte das passieren, wenn UDP blockiert war; **0.7.6** sendet den Heartbeat zusätzlich per HTTP. Plugin und Firmware auf 0.7.7 aktualisieren, SimHub starten, im Plugin das Display auswählen und **Test connection** drücken; IP/Host prüfen. |
| Plugin erscheint **nicht** in SimHub | DLL liegt im falschen Ordner, SimHub nicht neu gestartet, oder Windows blockiert die DLL (Rechtsklick → Eigenschaften → „Zulassen“). |
| Uhrzeit ist **falsch** | Display hat kein Internet (NTP) oder eine andere Zeitzone. In `firmware/cyd-flags.yaml` steht `timezone: Europe/Berlin`; für andere Länder anpassen. |
| Uhrzeit zeigt **--:--** | Die Uhr hat noch keine Internetzeit bekommen. Warten oder Internet/router prüfen. |
| **no wifi** unten | WLAN-Einrichtung wiederholen: Display neu starten, mit `CYD Flags fallback` verbinden, `http://192.168.4.1`. |
| Kein WLAN **`CYD Flags fallback`** und `192.168.4.1` nicht erreichbar | Wichtig: Du musst **zuerst** am Handy/PC mit `CYD Flags fallback` verbunden sein, sonst ist `192.168.4.1` nicht erreichbar. Dann: RST-Taste am Display drücken, 20 s warten. Prüfe, ob du wirklich eine `.factory.bin` (ca. 1 MB) und nicht die `.ota.bin` geflasht hast. Wenn das Display schwarz bleibt, ist die Firmware nicht gestartet → neu flashen. |
| ESPHome Web fragt nach dem Flashen **nicht** nach WLAN | Nicht schlimm: Kabel abziehen, wieder anstecken, 20 s warten und das Fallback-WLAN benutzen. Die Abfrage ist nur eine Abkürzung. |
| Flaggen kommen **nicht** an | Teste im Browser: `http://<IP>/select/flag` – kommt JSON? Dann liest SimHub die Flaggen des Spiels nicht. Prüfe in SimHub, ob das Spiel erkannt wird. |
| **Tippen** schaltet die Seite nicht um | Unten rechts = nächste, unten links = zurück; die oberen Ecken sind für den Alarm reserviert. Standardmäßig gibt es alle Seiten überall; mit der Race-Pages-Option im Training/Qualifying nur Rev-Limiter und Uhr. Bei einer aktiven Flagge reagiert die Navigation nicht. Bei ganz alter Firmware fehlt die Touch-Funktion → neue Datei flashen. |
| **Touch reagiert links/rechts vertauscht** | Firmware 0.7.2 korrigiert die gespiegelte X-Achse des CYD-Touchcontrollers. Neue Factory-/OTA-Datei flashen. |
| Racelogic-Sektoren bleiben **`--.---`** | Dein Spiel liefert keine aktuellen Sektorzeiten. Runde und Delta funktionieren trotzdem; die Sektoren füllen sich, sobald das Spiel sie meldet. |
| Turm / Rev-Limiter aktualisiert **zu langsam** | Plugin und Firmware müssen beide 0.7.7 sein. Die Echtzeitdaten laufen über **UDP 18512**; prüfe, ob die Windows-Firewall UDP blockiert, und trage im Plugin besser die **IP** des Displays ein (nicht `cyd-flags.local`). Ohne UDP fällt das Plugin auf HTTP zurück und der Turm wird wieder träge. |
| Welche Firmware läuft? | Beim Start zeigt das Display für 8 Sekunden **CYD FLAGS** und die Version (z. B. `v0.7.7`). Danach kommt der normale Bildschirm. |
| Plugin zeigt **UDP: blocked** | Die schnellen Daten kommen nicht am Display an (Firewall/Router/IP). Das Plugin fällt für den Link auf HTTP zurück; Turm und Rev bleiben langsam. IP statt `cyd-flags.local` eintragen, UDP 18512 im LAN erlauben. |
| Alles geht, aber nach Router-Neustart nicht mehr | Die IP hat sich geändert. Neue IP auf dem Display ablesen und im Plugin eintragen. |

---

## Optional: Firmware selbst bauen

Nur nötig, wenn du eine andere CYD-Variante hast oder etwas ändern willst.

```bash
cd firmware
cp secrets.yaml.example secrets.yaml
# secrets.yaml öffnen und WLAN + Passwörter eintragen
esphome run cyd-flags.yaml
```

ESPHome installieren:

```bash
python3 -m pip install --user esphome
```

Oder die ESPHome-Dashboard-App für Windows benutzen.

Die vollständige technische Doku steht in
[docs/cyd-flags.md](docs/cyd-flags.md).

---

## Kurzfassung (nur die Klicks)

1. Die passende `dist/cyd-flags-portrait-….factory.bin` herunterladen (siehe 1.1).
2. https://web.esphome.io in Chrome/Edge öffnen → Connect → Install → Datei wählen.
3. Nach dem Flash die WLAN-Abfrage ausfüllen. Falls sie nicht kommt: mit WLAN `CYD Flags fallback` verbinden, Passwort `cyd-fallback`.
4. `http://192.168.4.1` öffnen → eigenes 2,4-GHz-WLAN einrichten.
5. IP beim Start unten am Display ablesen (verschwindet nach 15 s).
6. SimHub schließen → `dist/CydFlags.dll` in den SimHub-Ordner kopieren.
7. SimHub starten → CYD Flags → Haken setzen → Display auswählen → IP eintragen → Test connection.
8. Spiel starten. Fertig.

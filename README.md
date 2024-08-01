# OBS Sync
Simple plugin to connect to OBS's WebSocket server and automatically split recordings and write the timestamps of
in-game events into a text file beside the recording.

## How to use

Setup OBS and the plugin by following the guides below (you may need to run the game once first so the plugin config is
generated). Once configured, OBSSync should automatically connect when you launch the game.

OBSSync will automatically write down a timestampped log file with event that happen in game, however you can trigger
some manual events by using either the manual event key (default PageUp) or by sending a chat message in the format
`!mark <message>`

### OBS Setup
1. In OBS: `Tools > WebSocket Server Settings`
2. Make sure `Enable WebSocket server` is checked
3. Click Apply
4. Click `Show connection info`
5. Copy the server password
6. Paste the server password into this plugin's config

### Config options
* **Connection WebsocketAddress**: The ip/port of the OBS websocket server you want to connect to. You will only need 
  to change this if you changed OBS's websocket server port, or if you're running OBS on a different computer
  entirely.

* **Connection WebsocketPassword**: The password for OBS's websocket. This can be blank if you've disabled the password
  requirement in OBS.

* **Recording AutoStartStop**: When enabled, the mod will tell OBS to start recording when you join a game and tell it
  to stop recording when you leave a game.

* **Recording AutoSplit**: When enabled, the mod will stop the current recording and start a new one in between moons to
  automatically create individual recording files per moon.

* **Recording ManualEventKey**: The key that when pressed will add a manually triggered event into the timestamp log.

**NB:** When using the auto split feature there will be a short delay of about half a second between the end of one
recording and the start of the next. The split occurs after pulling the ship's lever to descend onto a moon where I
believe it won't cause much of a problem. If this delay is not acceptable to you, turn the feature off.

## Timestampped events
- [x] Manual events by keypress / chat message
- [x] Player deaths
- [x] Player damage
- [x] Enemy deaths
- [x] Bracken getting mad
- [x] Stun grenade / easter egg explosions
- [x] Things that cause 'fear'
- [x] Jester begins winding
- [x] Loot bug aggravation (Works only on host?)

**NB:** Due to the game's uh... interesting netcode, some events will only be triggered if you are the host or the
target of an enemy. I want to keep this plugin entirely client-side so there's no way to work around that currently.

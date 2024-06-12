# OBS Sync
Simple plugin to connect to OBS's WebSocket server and automatically split recordings and write the timestamps of
in-game events into a text file beside the recording.

### Recording events
* Split recording (stop & start) between each moon
* Rename recorded files to add the moon name 

### Timestampped events
- [x] Manual events by keypress
- [x] Player deaths
- [x] Player damage
- [x] Enemy deaths
- [x] Bracken getting mad
- [x] Easter egg explosion
- [x] Things that cause 'fear'
- [x] Jester begins winding
- [~] Loot bug aggravation (Works only on host?)
- [ ] Coilhead aggravation (WIP)

## OBS Setup
1. In OBS: `Tools > WebSocket Server Settings`
2. Make sure `Enable WebSocket server` is checked
3. Click Apply
4. Click `Show connection info`
5. Copy the server password
6. Paste the server password into this plugin's config

## Config options
* **Connection WebsocketAddress**: The ip/port of the OBS websocket server you want to connect to. You will only need 
  to change this if you changed OBS's websocket server port, or if you're running OBS on a different computer
  entirely.

* **Connection WebsocketPassword**: The password for OBS's websocket. This can be blank if you've disabled the password
  requirement in OBS.

* **Recording AutoStartStop**: When enabled, the mod will tell OBS to start recording when you join a game and tell it
  to stop recording when you leave a game.

* **Recording AutoSplit**: When enabled, the mod will stop the current recording and start a new one in between moons to
  automatically create individual recording files per moon.
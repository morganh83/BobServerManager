# Beasts of Bermuda Server Manager

A Windows desktop tool for running/maintaining a self-hosted **Beasts of Bermuda** dedicated
server: scheduled reboots with a warning message, a startup-switches editor, and a Game.ini
config editor - all in one app.

## Get it running

Download `BobServerManager.exe` from the
[latest release](https://github.com/morganh83/BobServerManager/releases/latest).

It's self-contained (bundles its own .NET runtime) - copy that one file anywhere and double-click
it. No installer, no prerequisites.

To build it yourself (needs the .NET 9 SDK):

```
cd BobServerManager
dotnet publish -c Release -o publish
```

That produces `publish\BobServerManager.exe`.

## What each tab does

- **Dashboard** - Start/Stop/Restart Now/Update Server, a live log of the server's own console output, the
  countdown to the next scheduled reboot, and a box to fire a test broadcast through whichever
  notification channels you've enabled.
- **Startup Switches** - map, game mode, server name, password, max players, ports, MultiHome,
  and a free-text field for anything else - with a live preview of the exact command line that
  will be used to launch `BeastsOfBermudaServer.exe`.
- **Reboot Schedule** - enable/disable auto-reboot, either "every N hours" (defaults to **24**) or
  every day at specific times (pick a time, click **Add**; add more than one if you like); how many minutes ahead of time to warn (defaults to **5**, plus
  optional extra checkpoints like 30/15/1); and the editable warning message text (supports
  `{minutes}` / `{time}` placeholders). Also configures the graceful-shutdown command/timeout and
  which channels carry the warning.
- **Server Updates** - points the tool at `steamcmd.exe` (or downloads it for you), and works
  out the install folder from the server exe path. Use **Update Server** on the Dashboard to
  update now: if the server is running it gets a warning message, a save, a graceful stop, the
  SteamCMD update (`app_update 882430`), and a restart. Tick *Update the server during every
  scheduled reboot* to have every scheduled reboot update the server too.
- **Config Editor** - a structured editor for the common Game.ini settings (difficulty, growth
  rates, chat, admin slots, weather, autosave, world events, RCON, Discord activity webhooks),
  a grid-based editor for Server Admins / chat tags & colors / admin ranks / command permissions,
  a grid for per-species creature limits, and a **Raw INI** tab for anything not covered
  structurally (it never touches array-style entries it doesn't explicitly model, so nothing you
  already have in Game.ini gets silently deleted).

Settings are remembered in `%AppData%\BobServerManager\settings.json`; a rolling copy of the
server's console output is written to `%AppData%\BobServerManager\server_output.log`.

## How the reboot warning actually reaches players - read this

Beasts of Bermuda doesn't have one universally-documented way to push a message into a running
server from an external tool, so this app supports three channels you can mix and match in the
**Reboot Schedule** tab:

1. **In-game console announce (on by default)** - the app launches the server itself and keeps
   its console input redirected, so sending `Announce <message>` works the same way typing it
   directly into the server's own console/log window would. This is the most reliable option
   *if you let this tool be the one that starts the server* (Dashboard → Start Server, or turn on
   auto-start). It won't do anything if the server was started some other way and this tool isn't
   the one holding its console.
2. **Discord webhook** - a plain HTTP POST to a Discord channel webhook. This always works and is
   a good way to make sure admins see the warning even if channel 1 doesn't apply to your setup.
3. **RCON (off by default, marked experimental)** - Game.ini exposes an
   `[/Script/BeastsOfBermuda.RCONHandler]` section (`CommunicationPort` / `IP4Binding`), which the
   community's own config tool labels *"Beta, not fully implemented yet"* and describes as
   one-way output to RCON clients rather than a channel for sending commands in. This app
   implements the standard Source-RCON wire protocol (the same one hosting panels use for this
   game) as a best-effort extra channel, but don't rely on it alone - test it with the Dashboard's
   "Send Test Message" button before trusting it for real warnings.

`ForceServerSave` is sent (optional, on by default) before every reboot, and the shutdown itself
uses the server's own `Exit` console command with a fallback hard-kill after a timeout, rather
than just terminating the process.

## Where these facts came from

Since there's no single official spec document, the command-line syntax, Game.ini section/key
names, and console command list here were cross-checked against:
- the official wiki (`wiki.beastsofbermuda.com`) Dedicated Servers and Console Commands pages,
- the community install script (`github.com/cleafspear/BoB_Server_Install_Script`), and
- the community Game.ini generator (`cleafspear.github.io`), whose source literally builds the
  file this tool edits.

If a future game update changes any of this, the **Raw INI** tab and the **Extra arguments** box
on the Startup tab are there so you're never blocked waiting on this tool to catch up.

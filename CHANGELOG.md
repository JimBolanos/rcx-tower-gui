# Changelog

## 3.2

- Fixed a `MissingMethodException` on startup: one call resolved to a `String.Split`
  overload that does not exist on .NET Framework. Replaced with the `char[]` overload.
  Every external call in the assembly was audited against the .NET Framework 4.8 API.
- Fixed the panel getting stuck: if a compile succeeded but `rcx_tool.py` could not be
  found afterwards, the buttons stayed disabled forever. They now recover, and
  *Force release / Cancel* always restores a usable state.

## 3.1

- Resizable window (minimum 560 x 700), compact layout below 800 px tall, console takes
  the leftover height, and the size is remembered between runs.

## 3.0

- Rewritten as a native Windows application. Earlier builds wrapped a hidden PowerShell
  process, which could die silently and leave nothing on screen.

## 2.0

- Scratch-style interface: rounded cards, flat colour buttons, live colour console.
- `python` and `nqc` auto-detected in the application folder.
- `nqc` compiler output is shown in the console instead of being discarded.
- Firmware uploads get a 180 s watchdog instead of 8 s, which used to kill the transfer
  halfway through.

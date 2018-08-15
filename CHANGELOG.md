Changelog
=========

## [Unreleased]

### Added
- Option to change the alpha value of the input background texture.

### Changed
- Better autocompletion: autocomplete can now partially complete words when there are multiple suggestions available.

### Fixed
- Fix background texture being destroyed when loading a scene with the Terminal set to `DontDestroyOnLoad`.
- Fix hotkeys bound to function keys causing the input to not register the first character.

## 1.01 `9a1b0b3` - 2018-08-09

### Added
- Option to to change the position of the toggle GUI buttons.
- Option to change the window size ratio between the partial and full window height.
- Optional GUI button to run a command (useful for mobile devices).

### Changed
- Autocomplete now uses the last word in the input text, rather than just completing the first word.

## 1.0  `db07b43` - 2018-07-15

### Added
- Customizable toggle hotkey.
- Two new terminal colors (customizable).
- Option to change prompt character (or remove it).
- Option to open a larger terminal window with a separate hotkey.
- Command autocompletion (use the tab key while typing a command).
- Option to toggle window using GUI buttons (disabled by default).
- Option to customize the input background contrast.

### Fixed
- Input registering hotkey character when hotkey was pressed.
- Inspector presentation.

### Removed
- `LS` command in favor of `HELP` with no arguments to list all registered commands.

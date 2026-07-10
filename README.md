# TeX Lister Plugin for Total Commander

This project builds a 64-bit Total Commander Lister plugin (`.wlx64`) for viewing TeX-related files.

Supported extensions:

- `.tex`
- `.latex`
- `.ltx`
- `.sty`
- `.cls`
- `.ps`
- `.eps`

The plugin uses the Total Commander WLX interface. For `.ps` and `.eps` files, it converts PostScript directly with Ghostscript. For TeX files that contain `\includegraphics{...}` pages, it renders the referenced images directly. For older samples with a matching `.ps` file next to the `.tex` file, it converts the PostScript file with Ghostscript. Macro files such as `lauracode.tex` and `testpoints.tex` can render through a nearby TeX document that inputs them and has a matching `.ps` output. For ordinary LaTeX files, it runs `xelatex`, converts the generated PDF pages with `pdftoppm`, and displays the rendered pages. If a document references missing EPS files, the plugin generates placeholder EPS files so the document can still render. TeX fragments without any renderable target are rendered as fixed-width source-listing pages.

It reads UTF-8, UTF-8 BOM, UTF-16 LE, UTF-16 BE, and falls back to the Windows default code page when that gives a cleaner result.

## What to install

### Required

- Total Commander for Windows.
- This plugin package from `dist\`:
  - `texview.wlx64` for 64-bit Total Commander.
  - `texview.wlx` for 32-bit Total Commander.
  - `pluginst.inf` for Total Commander plugin installation metadata.

### Required for compiling normal LaTeX files

Install either MiKTeX or TeX Live.

The plugin needs these executables:

- `xelatex.exe`
- `pdftoppm.exe`

Known working setup on this machine:

- MiKTeX installed under `%LOCALAPPDATA%\Programs\MiKTeX\miktex\bin\x64`

`pdftoppm.exe` is included with MiKTeX on this machine. If your TeX distribution does not include it, install Poppler for Windows and add its `bin` folder to `PATH`.

### Required for old samples with `.ps` files

Install Ghostscript.

The plugin needs one of these executables:

- `gswin64c.exe`
- `gswin32c.exe`

Known working setup on this machine:

- Ghostscript installed under `C:\Program Files\gs\gs10.07.1\bin`

### Optional but recommended

- Add the MiKTeX/TeX Live and Ghostscript `bin` folders to your Windows `PATH`.
- Restart Total Commander after changing `PATH` or replacing the plugin DLLs.

The plugin also searches common MiKTeX, TeX Live, and Ghostscript install folders directly, but `PATH` is still the most reliable setup.

## Build

```powershell
.\build.ps1
```

The installable plugin files are produced under:

```text
dist\
```

The native plugin files are:

```text
dist\texview.wlx64
dist\texview.wlx
```

## Install in Total Commander

Install dependencies only:

```powershell
.\install-dependencies.ps1
```

If your TeX setup does not provide `pdftoppm.exe`, install Poppler too:

```powershell
.\install-dependencies.ps1 -IncludePoppler
```

Install the Total Commander plugin:

```powershell
.\install-totalcmd-plugin.ps1 -Build
```

Optional parameters:

```powershell
.\install-totalcmd-plugin.ps1 -TotalCommanderDir "C:\totalcmd" -IniPath "$env:APPDATA\GHISLER\wincmd.ini"
```

`install-dependencies.ps1` installs MiKTeX and Ghostscript through `winget`. `install-totalcmd-plugin.ps1` copies both plugin binaries, backs up `wincmd.ini`, registers the Lister plugin, and prints a dependency check for `xelatex.exe`, `pdftoppm.exe`, and Ghostscript.

Manual install:

1. Open **Configuration > Options > Plugins > Lister plugins (.WLX)**.
2. Add `texview.wlx64` if you use 64-bit Total Commander, or `texview.wlx` if you use 32-bit Total Commander.
3. Move it above generic text viewers.
4. Open a `.tex` file and press `F3`.
5. If you replaced an older build, fully close and reopen Total Commander before testing.

## Notes

- Rendered pages are cached under the Windows temp folder.
- In the rendered page viewer, use `+` to zoom in, `-` to zoom out, and `0` to reset to fit-to-window.
- It exports both Unicode and ANSI WLX entry points so current Total Commander versions can use `ListLoadW` while older plugin probing still has compatible exports.

# Privacy Policy: RBX15 Classic Shirt and Pants Maker

**Last updated:** 2026-10-25
**Publisher:** 626 Labs LLC
**Contact:** [GitHub Issues](https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/issues)

## Short version

This app is a **local desktop template editor**. It makes no network requests at all. It has no server, no backend, no analytics and no crash reporting, and it never sends your data to 626 Labs or any third party. Everything you load, edit and export stays on your computer.

The app does keep a few small files on your computer so it can remember your window, list your recent projects, rescue unsaved work after a crash and log errors. They are listed below, with where they live and how to remove them.

## What the app stores on your computer

The app keeps its own files in one folder, `%LOCALAPPDATA%\RBX15` (usually `C:\Users\<you>\AppData\Local\RBX15`). Nothing is written anywhere else except the files you choose to save or export.

| Data | Where it lives | What it holds | When it goes away |
| --- | --- | --- | --- |
| The images you load (PNG, JPG and so on) | Wherever you saved them | Read into memory and drawn on the canvas | The app never changes or deletes them |
| Your project files (`.r15proj`) | Wherever you choose to save them | Plain JSON with your layers and the images embedded in them, which you can inspect yourself | You delete them |
| Exported templates (PNG) | Wherever you choose to export them | The finished template image | You delete them |
| Settings (`settings.json`) | `%LOCALAPPDATA%\RBX15` | The window position, size and whether it was maximized; the last folder you saved or opened a project in; the last folder you opened in the asset library; and the full paths of your last 10 projects | Overwritten as you work. "Clear recent list" in the File group empties the project list. Files that no longer exist are dropped from the list when the app starts |
| Autosave (a copy of unsaved work) | `%LOCALAPPDATA%\RBX15\autosave` | While you have unsaved changes, the app writes a copy of your whole document every 60 seconds (a `.r15proj` file), plus a small note beside it (a `.json` file) with the app version, the time, the path of the file you were editing, and the id and start time of the app process. It is never written next to or over your own file | Deleted when you save, open another project or close the window normally. After a crash or a forced quit it stays until you restore it, discard it, or open the app again and choose |
| Error log | `%LOCALAPPDATA%\RBX15\logs` | One entry for each error the app did not expect: the time (UTC), the app and Windows versions, and the error's type, code, message and stack trace. Your Windows user folder in any text is replaced with `%USERPROFILE%`. The log holds no layers or image data, though an error message can name a file the app was working with | The log is capped: at 256 KB it moves to `error.1.log`, replacing any older `error.1.log`, so it never grows past about 512 KB. You can delete the `logs` folder at any time |

The autosave copy is the one place the app keeps your artwork outside a file you chose. It exists only to rescue your work.

The app reads the Windows clipboard only when you press Ctrl+V to paste an image. It does not watch the clipboard and never writes to it.

## Where that folder is in each version

- **Microsoft Store version.** The app is packaged, and Windows keeps a packaged app's `AppData` writes in a private area belonging to the app. Uninstalling (Start, Settings, Apps, RBX15 Classic Shirt and Pants Maker, Uninstall) is meant to remove that data with the app. To be sure, look in `%LOCALAPPDATA%\RBX15` after uninstalling and delete anything left.
- **Portable zip.** The app writes the real `%LOCALAPPDATA%\RBX15` folder. Nothing is installed, so nothing is uninstalled. Delete that folder by hand to remove everything the app stored.

## Network activity

**The app makes no network requests.** It does not check for updates, load fonts from the internet, send crash reports or contact any server. Its fonts are bundled inside the app, so Word Art and the interface look the same offline. The Store package asks Windows for no network permission.

If you block the app in a firewall, nothing changes.

## Fonts

The app includes 18 font families, all under the SIL Open Font License 1.1: Anton, Bangers, Bebas Neue, Bungee Shade, Creepster, Dancing Script, Great Vibes, Inter, JetBrains Mono, Lato, Orbitron, Pacifico, Permanent Marker, Poppins, Press Start 2P, Righteous, Rubik Mono One and Space Grotesk. Each family's license text ships with the app in `assets\fonts\<family>\OFL.txt`.

## What the app does NOT do

- **No telemetry.** No install pings, no usage analytics, no crash reporting to any server.
- **No advertising.** The app displays no ads and sends no data to ad networks.
- **No account.** You don't sign up with 626 Labs. We have no user database. We cannot identify you.
- **No cloud sync.** Your projects don't go anywhere. If your disk fails, they are gone, so keep your own backups.
- **No copy protection or DRM** that would phone home.

## Permissions

The Store package declares one capability, `runFullTrust`. It lets the app run as an ordinary Windows desktop program instead of inside a restricted sandbox. The app uses it to show its own window, open and save the files you pick, and write the folder described above. The package does not request network access, camera, microphone, location or contacts.

## How you remove your data

- **Projects and templates** live wherever you saved them. Delete those files directly.
- **Recent projects:** use "Clear recent list" in the File group. The taskbar jump list shows the same list and the app updates it when the list changes. Windows itself keeps the jump list.
- **Everything else the app stored:** delete `%LOCALAPPDATA%\RBX15`. For the Store version, uninstalling the app is meant to do this for you.

## Third-party services the app does not use

For clarity: **no** Firebase, **no** Google Analytics, **no** Segment, **no** Sentry, **no** Mixpanel, **no** Amplitude, **no** Crashlytics, **no** Rollbar, **no** PostHog, **no** Datadog RUM, **no** any SaaS that would send your activity off your machine.

## Children's privacy

The app has no user accounts, no telemetry, no content uploads, and no chat or social features. It is safe for use by children under adult supervision. Parents should note that the app produces PNG files intended for upload to Roblox, and any upload activity is governed by [Roblox's own terms and privacy policy](https://en.help.roblox.com/hc/en-us/articles/115004647846).

## Roblox, trademarks, and third-party content

- **Roblox** is a trademark of Roblox Corporation. This app generates PNG files formatted to their Classic R15 template spec. It is not affiliated with, endorsed by, or sponsored by Roblox Corporation.
- **You are responsible for the art you use.** The app does not license, warrant, or screen any image you load. Uploading copyrighted content to Roblox without permission may violate their terms and / or copyright law.

## Changes to this policy

If the data story ever changes, this file will be updated and the change will be noted in the release [CHANGELOG.md](../CHANGELOG.md). Major changes (adding any network destination, adding any telemetry, adding any third-party integration that sees your data) will be called out in release notes.

## Questions

Open an issue: <https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/issues>

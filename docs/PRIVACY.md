# Privacy Policy — RBX15 Classic Shirt and Pants Maker

**Last updated:** 2026-04-20
**Publisher:** 626 Labs LLC
**Contact:** [GitHub Issues](https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/issues)

## Short version

This app is a **local desktop template editor**. It does not run a server, does not have a backend, does not collect analytics, does not call home, and does not send your data to 626 Labs or any third party. Everything you load, edit, and export stays on your machine.

## What data the app touches

| Data | Where it lives | Who can see it | What the app does with it |
| --- | --- | --- | --- |
| The images you load (PNG / JPG / etc.) | Wherever you saved them on disk | Your Windows user account | Displayed on the canvas. The app reads them into memory. Never uploaded anywhere. |
| Your project files (`.r15proj` JSON) | Wherever you save them | Your Windows user account | Read when you open them, written when you save them. Plain JSON you can inspect yourself. |
| Exported templates (PNG output) | Wherever you download them | Your Windows user account | Written to disk. Never uploaded anywhere. |
| Your template-name input and window size | Memory only — the app has no persistent settings file | Nobody — they disappear on quit | Used only for the current session |

## What the app does NOT do

- **No telemetry.** No install pings, no usage analytics, no crash reporting to any server we control.
- **No advertising.** The app displays no ads and sends no data to ad networks.
- **No account.** You don't sign up with 626 Labs. We have no user database. We cannot identify you.
- **No cloud sync.** Your projects don't go anywhere. If your disk crashes, they're gone.
- **No copy protection or DRM** that would phone home.

## Network activity

The app makes one type of outbound request: **Google Fonts** (`fonts.googleapis.com` and `fonts.gstatic.com`). This happens once per Electron install, cached in Electron's HTTP cache, and only loads the font files used by the word-art generator. No information about you is attached to that request — Google receives the same signals any browser fetch would send (IP address, user agent). Google's privacy policy governs what they do with those requests: <https://policies.google.com/privacy>.

If you want to disable even this, block `fonts.googleapis.com` at your firewall. The app still works — word-art falls back to system fonts.

## How you remove your data

- **Projects and templates** live wherever you chose to save them. Delete those files directly.
- **The app itself:** Start → Apps & features → RBX15 Classic Shirt and Pants Maker → Uninstall. There is no hidden data to wipe.

## Third-party services the app does not use

For clarity: **no** Firebase, **no** Google Analytics, **no** Segment, **no** Sentry, **no** Mixpanel, **no** Amplitude, **no** Crashlytics, **no** Rollbar, **no** PostHog, **no** Datadog RUM, **no** any SaaS that would send your activity off your machine.

## Children's privacy

The app has no user accounts, no telemetry, no content uploads, and no chat / social features. It is safe for use by children under adult supervision. Parents should note that the app produces PNG files intended for upload to Roblox, and any upload activity is governed by [Roblox's own terms and privacy policy](https://en.help.roblox.com/hc/en-us/articles/115004647846).

## Roblox, trademarks, and third-party content

- **Roblox** is a trademark of Roblox Corporation. This app generates PNG files formatted to their Classic R15 template spec. It is not affiliated with, endorsed by, or sponsored by Roblox Corporation.
- **You are responsible for the art you use.** The app does not license, warrant, or screen any image you load. Uploading copyrighted content to Roblox without permission may violate their terms and / or copyright law.

## Changes to this policy

If the data story ever changes, this file will be updated and the change will be noted in the release [CHANGELOG.md](../CHANGELOG.md). Major changes (adding any network destination beyond Google Fonts, adding any telemetry, adding any third-party integration that sees your data) will be called out in release notes.

## Questions

Open an issue: <https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants/issues>

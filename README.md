# Dumbubu - Virtual Desktop Pet

<div align="center">
  
[![Steam](https://img.shields.io/badge/Steam-Available-blue?style=for-the-badge&logo=steam&logoColor=white)](https://store.steampowered.com/app/4160160/Dumbubu/)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

**"You can't afford a house but you can afford Dumbubu"**

Source code for **[Dumbubu Virtual Desktop Pet](https://store.steampowered.com/app/4160160/Dumbubu/)**

</div>

## Prerequisites

- Unity 2021.3.9f1 (specific version required)
- Steam account (for testing Steam features)

## Usage
- In Unity Hub: Select `Add project from disk` in Unity Hub and select this directory
- In Unity Editor: File->Build and Run

## Features

- Drag and Throw Physics: Drag and throw Dumbubu across the screen.
- Collision Detection: Earn points when Dumbubu bumps into things.
- Grenade Mode: Unlock grenade mode. Drop grenades to explode Dumbubu.
- Window Collision: Dumbubu Bounces off edges of screen.
- Steam Cloud Integration: Save points data to Steam Cloud.
- Steam Inventory: Random skin drops every 10 minutes to Steam Inventory.
- ChatGPT Companion: Connect your ChatGPT account from the right-click menu and
  Dumbubu shares a short thought above its head about every 30 seconds. Speech
  is one-way, with no reply box. Turn speech off or disconnect from the same menu.

## ChatGPT companion

Click **Continue with ChatGPT** in Dumbubu's right-click menu and finish sign-in
in your system browser. This uses OpenAI's public-client OAuth flow for
open-source apps and an eligible ChatGPT plan with app usage enabled; no API key
or client secret is needed. The account button cycles saved registrations and
the option to add another account. Selecting an account and continuing sign-in
makes it active only after verification succeeds.

Dumbubu's editable personality lives in
[`Assets/StreamingAssets/ChatGPT/SKILL.md`](Assets/StreamingAssets/ChatGPT/SKILL.md).
The file ships with desktop builds and is loaded at startup. Requests include
only local time, game activity, points, and up to five recent Dumbubu lines.
They do not access your desktop contents or ChatGPT conversation history.
Speech uses your account's available small model when present, otherwise the
first model in its catalog. Each response must complete successfully before
the bubble appears; temporary network and availability failures back off to at
most five minutes. Usage-limit failures pause automatic speech, preserve the
connection, and display a notice above Dumbubu. Use **ChatGPT Usage settings**
to review app access and limits, then **Test speech now** to resume when access
is available. That code can describe an app-specific limit; it does not establish
the account's total remaining usage or a reset time.

Close the menu after connecting to see the first speech request. **Test speech
now** also closes it and requests a line immediately. Speech and error notices
wait for the menu/startup notice to close before their reading timer begins.

Registrations and tokens are stored under `Application.persistentDataPath/ChatGPT`,
outside the project and Steam Cloud saves. macOS/Linux files have owner-only
permissions; Windows protects the record with user-scoped DPAPI. Disconnect
attempts remote session revocation and removes local tokens while retaining the
registration for later sign-in. If remote revocation cannot be confirmed, the
menu explains how to remove the app in ChatGPT Settings.

Protocol reference: [OpenAI registration and sign-in](https://developers.openai.com/siwc/token-sharing-open-source/sign-in),
[models and inference](https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference).
Review app usage and access in [ChatGPT Settings](https://chatgpt.com/settings/usage).

### Verification

`Tests/ChatGPT` runs offline OAuth, JWT, loopback, streaming, renewal, persistence,
and sign-out checks against a fake provider. With .NET 6 installed and Unity's
Newtonsoft package imported, run:

```sh
dotnet run --project Tests/ChatGPT/ChatGPT.Tests.csproj
```

If the package assembly is elsewhere, pass
`-p:NewtonsoftJsonPath=/absolute/path/to/Newtonsoft.Json.dll`.
In Unity, verify sign-in/cancel, disconnect, mute/re-enable, account selection,
and bubbles following the pet near all screen edges. A real eligible account is
required for the browser and model smoke test.

<div align="center">

**Made by Upside Down 9 Studio**

</div>

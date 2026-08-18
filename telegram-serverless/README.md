# Telegram Serverless commands

Staff-only bot commands for Exchange_APP. Incoming `/rates` is handled on Telegram's infrastructure (no long polling, no webhook on Plesk). Rate data is still read from the Plesk app.

Outbound order/accounting alerts stay in `TelegramNotificationProvider` (`sendMessage` via `ProxyBaseUrl`).

## Commands

| Command | Who | Result |
|---|---|---|
| `/rates` | chats in `Notifications:Telegram:TargetChatIds` | Active `ExchangeRate` list |
| `/start` `/help` | same allowlist | Command list |
| anything else / unknown chat | ignored | No reply |

Unauthorized chats get **no reply**. The same `TargetChatIds` used for notifications is the allowlist.

## 1. App settings (Plesk)

In `appsettings.json` (already gitignored):

```json
"Notifications": {
  "Telegram": {
    "Enabled": true,
    "ProxyBaseUrl": "https://your-telegram-proxy",
    "BotToken": "123456:ABC...",
    "TargetChatIds": [ "111111111" ],
    "Commands": {
      "ApiToken": "generate-a-long-random-string"
    }
  }
}
```

`ApiToken` is a **new shared secret**, not the bot token. In PowerShell:

```powershell
[guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")
```

Publish / recycle the Plesk site so `POST https://YOUR-DOMAIN/api/telegram/command` is live.

## 2. Enable Serverless on the existing bot

1. Open [@BotFather](https://t.me/BotFather) → your bot → **Serverless** → turn it on.
2. Copy the **CLI Access** token (`app…:…`). This is separate from `BotToken`.

Optional, so `/rates` appears in the Telegram menu:

```
/setcommands
rates - نرخ‌های فعال ارز
help - راهنمای دستورات
```

## 3. Fill handler config and deploy

```powershell
cd telegram-serverless
copy lib\config.example.js lib\config.js
npm install
npx tgcloud login
```

Edit `lib/config.js`:

- `appBaseUrl` — public HTTPS origin of the Plesk site, no trailing slash
- `apiToken` — same value as `Notifications:Telegram:Commands:ApiToken`

Then:

```powershell
npx tgcloud push
```

Telegram now owns the webhook. Plesk does **not** need `getUpdates`.

Test without deploying:

```powershell
npx tgcloud run handlers/message '{ chat: { id: 111111111 }, text: "/rates" }'
```

Use a chat id that is in `TargetChatIds`.

## How a /rates call works

1. Staff sends `/rates` in an allowed chat.
2. Telegram runs `handlers/message.js`.
3. Handler POSTs to `/api/telegram/command` with `X-Telegram-Api-Token` and `chatId`.
4. The app rejects unknown tokens and unknown chats with 403 (handler stays silent).
5. Allowed chats get the formatted rate list from SQL/`ExchangeRate`.

## Plesk notes

- The site only needs a normal short HTTPS POST (same as any API).
- App-pool idle/recycle does not break commands; Telegram retries the handler, then your site answers when it wakes.
- Keep using `ProxyBaseUrl` for **outgoing** notifications. Serverless does not replace that path.

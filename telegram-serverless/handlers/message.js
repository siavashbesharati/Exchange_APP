import { api, fetch } from 'sdk';
import { appBaseUrl, apiToken } from 'lib/config';

const HANDLED_COMMANDS = new Set(['rates', 'start', 'help']);

export default async function (message) {
  const command = parseCommand(message.text);
  if (!command || !HANDLED_COMMANDS.has(command)) {
    return;
  }

  const chatId = message.chat?.id;
  if (chatId == null) {
    return;
  }

  if (!appBaseUrl || appBaseUrl.includes('YOUR-DOMAIN') || !apiToken || apiToken === 'REPLACE_ME') {
    console.error('lib/config.js is still using placeholders. Set appBaseUrl and apiToken.');
    return;
  }

  const response = await fetch(`${appBaseUrl.replace(/\/$/, '')}/api/telegram/command`, {
    method: 'POST',
    headers: {
      'X-Telegram-Api-Token': apiToken,
    },
    body: fetch.body.json({
      chatId: String(chatId),
      command,
    }),
  });

  // Unauthorized chats get no reply — this bot is staff-only.
  if (response.status === 401 || response.status === 403) {
    return;
  }

  if (!response.ok) {
    console.error('Exchange_APP command API failed', response.status, await safeText(response));
    await api.sendMessage({
      chat_id: chatId,
      text: 'خطا در دریافت اطلاعات. بعداً دوباره تلاش کنید.',
    });
    return;
  }

  const data = await response.json();
  const messages = Array.isArray(data?.messages) ? data.messages : [];

  for (const text of messages) {
    if (!text) continue;
    await api.sendMessage({
      chat_id: chatId,
      text,
      parse_mode: 'HTML',
    });
  }
}

function parseCommand(text) {
  if (!text || typeof text !== 'string') {
    return null;
  }

  const first = text.trim().split(/\s+/)[0];
  if (!first.startsWith('/')) {
    return null;
  }

  const withoutMention = first.split('@')[0];
  return withoutMention.slice(1).toLowerCase();
}

async function safeText(response) {
  try {
    return await response.text();
  } catch {
    return '';
  }
}

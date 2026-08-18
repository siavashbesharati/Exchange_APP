/**
 * Copy this file to lib/config.js and fill in real values before `npx tgcloud push`.
 * lib/config.js is gitignored so secrets stay off the repo.
 *
 * appBaseUrl  — public HTTPS origin of Exchange_APP on Plesk (no trailing slash)
 * apiToken    — must match Notifications:Telegram:Commands:ApiToken in appsettings.json
 */
export const appBaseUrl = 'https://YOUR-DOMAIN';
export const apiToken = 'REPLACE_ME';

const fs = require('fs');
const { defineConfig } = require('@playwright/test');

function findBrowser() {
  const candidates = [
    process.env.GAMENET_BROWSER_PATH,
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
    'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  ].filter(Boolean);
  return candidates.find(path => fs.existsSync(path));
}

module.exports = defineConfig({
  testDir: __dirname,
  testMatch: '**/*.cjs',
  workers: 1,
  reporter: 'list',
  use: {
    headless: true,
    launchOptions: {
      executablePath: findBrowser(),
    },
  },
});

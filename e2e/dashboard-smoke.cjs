const { chromium } = require('playwright');

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();

  const snapshot = {
    totalStations: 4,
    generatedAt: new Date().toISOString(),
    stations: [
      { id: 'pc-01', name: 'PC ۰۱', zone: 'pc', type: 'PC', ratePerHour: 95000, state: 'free', network: 1 },
      { id: 'pc-02', name: 'PC ۰۲', zone: 'pc', type: 'PC', ratePerHour: 95000, state: 'free', network: 2 },
      { id: 'pc-03', name: 'PC ۰۳', zone: 'pc', type: 'PC', ratePerHour: 95000, state: 'busy', network: 1, startedAt: new Date(Date.now() - 25 * 60000).toISOString(), sessionMinutes: 25, sessionRate: 95000, persons: 1, customerCode: 'reza_hs', buffetTotal: 30000 },
      { id: 'pc-04', name: 'PC ۰۴', zone: 'pc', type: 'PC', ratePerHour: 95000, state: 'free', network: 2 },
    ],
  };

  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(snapshot),
  }));
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.locator('[data-station-id="pc-01"]').waitFor();
  await page.waitForTimeout(800);

  // Ctrl-click multi-select
  await page.locator('[data-station-id="pc-01"]').click({ modifiers: ['Control'] });
  assert((await page.locator('.station-selection-tools').innerText()).includes('۱'), 'Ctrl-click did not select first station');

  await page.locator('[data-station-id="pc-02"]').click({ modifiers: ['Control'] });
  assert((await page.locator('.station-selection-tools').innerText()).includes('۲'), 'Ctrl-click did not add second station');

  // Shift-click range selection
  await page.locator('[data-station-id="pc-04"]').click({ modifiers: ['Shift'] });
  assert((await page.locator('.station-selection-tools').innerText()).includes('۳'), 'Shift-click did not select range');

  // Drag selection must select cards rather than browser-selecting text.
  await page.keyboard.press('Escape');
  const first = await page.locator('[data-station-id="pc-01"]').boundingBox();
  const last = await page.locator('[data-station-id="pc-04"]').boundingBox();
  assert(first && last, 'Station boxes were not measurable');
  await page.mouse.move(first.x + first.width / 2, first.y + first.height / 2);
  await page.mouse.down();
  await page.mouse.move(last.x + last.width / 2, last.y + last.height / 2, { steps: 10 });
  await page.mouse.up();
  assert((await page.locator('.station-selection-tools').innerText()).includes('۴'), 'Drag selection did not select the station range');

  // Busy station opens the Session Center.
  await page.keyboard.press('Escape');
  await page.locator('[data-station-id="pc-03"]').click();
  await page.getByRole('dialog', { name: 'مرکز جلسه' }).waitFor();
  assert(await page.getByText('وضعیت مالی').isVisible(), 'Session Center financial panel is missing');

  // Error UX smoke: a fresh page receives a server error and must show a Persian actionable banner.
  const errorPage = await context.newPage();
  await errorPage.route('**/api/dashboard', route => route.fulfill({ status: 500, contentType: 'application/json', body: '{}' }));
  await errorPage.route('**/hubs/**', route => route.abort());
  await errorPage.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await errorPage.getByRole('alert').waitFor();
  assert((await errorPage.getByRole('alert').innerText()).includes('ارتباط با سرور برقرار نشد'), 'Persian error banner is missing');
  assert(await errorPage.getByRole('button', { name: 'تلاش مجدد' }).isVisible(), 'Error retry action is missing');

  await browser.close();
  console.log('DASHBOARD_INTERACTION_SMOKE_OK');
})().catch(error => {
  console.error(error);
  process.exitCode = 1;
});

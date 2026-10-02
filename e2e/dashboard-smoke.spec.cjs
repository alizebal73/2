const fs = require('fs');
const { test, expect } = require('@playwright/test');

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

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

const browserPath = findBrowser();
if (!browserPath) throw new Error('هیچ Chrome یا Edge نصب‌شده‌ای روی Runner پیدا نشد.');

test.describe.configure({ mode: 'serial' });

test('dashboard interactions: selection, session center and Persian error UX', async ({ page }) => {
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

  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-admin',
      fullName: 'مدیر تست',
      userName: 'admin',
      email: 'admin@gamenet.local',
      role: 'Admin',
      isActive: true,
      lastLoginAt: new Date().toISOString(),
      permissions: ['user.manage', 'session.start', 'session.manage', 'session.settle', 'buffet.sell', 'buffet.inventory', 'finance.view', 'shift.manage', 'approval.decide']
    })
  }));
  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(snapshot),
  }));
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.locator('[data-station-id="pc-01"]').waitFor();
  await page.waitForTimeout(800);

  await page.locator('[data-station-id="pc-01"]').click({ modifiers: ['Control'] });
  await expect(page.locator('.station-selection-tools')).toContainText('۱');

  await page.locator('[data-station-id="pc-02"]').click({ modifiers: ['Control'] });
  await expect(page.locator('.station-selection-tools')).toContainText('۲');

  await page.locator('[data-station-id="pc-04"]').click({ modifiers: ['Shift'] });
  await expect(page.locator('.station-selection-tools')).toContainText('۳');

  await page.keyboard.press('Escape');
  const cardBoxes = await Promise.all(
    ['pc-01', 'pc-02', 'pc-03', 'pc-04'].map(id => page.locator(`[data-station-id="${id}"]`).boundingBox()),
  );
  expect(cardBoxes.every(Boolean)).toBe(true);
  const boxes = cardBoxes.filter((box) => box !== null);
  const left = Math.max(2, Math.min(...boxes.map(box => box.x)) - 12);
  const top = Math.max(2, Math.min(...boxes.map(box => box.y)) - 12);
  const right = Math.max(...boxes.map(box => box.x + box.width)) + 12;
  const bottom = Math.max(...boxes.map(box => box.y + box.height)) + 12;
  await page.mouse.move(left, top);
  await page.mouse.down();
  await page.mouse.move(right, bottom, { steps: 20 });
  await page.mouse.up();
  await expect(page.locator('.station-selection-tools')).toContainText('۴');
  const selectedTextAfterDrag = await page.evaluate(() => window.getSelection()?.toString() ?? '');
  expect(selectedTextAfterDrag).toBe('');

  await page.keyboard.press('Escape');
  await page.locator('[data-station-id="pc-03"]').click();
  await expect(page.getByRole('dialog', { name: 'مرکز جلسه' })).toBeVisible();
  await expect(page.getByText('وضعیت مالی')).toBeVisible();
});

test('dashboard exposes operator account management', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-admin',
      fullName: 'مدیر تست',
      userName: 'admin',
      email: 'admin@gamenet.local',
      role: 'Admin',
      isActive: true,
      lastLoginAt: new Date().toISOString(),
      permissions: ['user.manage', 'shift.manage', 'payroll.view', 'payroll.manage', 'approval.decide']
    })
  }));
  await page.route('**/api/users', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([{
      id: 'user-01',
      fullName: 'اپراتور تست',
      userName: 'operator_test',
      email: 'operator@gamenet.local',
      role: 'Operator',
      isActive: true,
      permissions: ['session.start'],
    }])
  }));
  await page.route('**/api/permissions', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([{
      id: 'permission-01',
      name: 'session.start',
      description: 'شروع جلسه'
    }])
  }));
  await page.route('**/api/payroll/users', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([])
  }));
  await page.route('**/api/approvals', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([])
  }));
  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ totalStations: 0, generatedAt: new Date().toISOString(), stations: [] })
  }));
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'کاربران و شیفت' }).click();
  await expect(page.getByRole('heading', { name: 'کاربران و شیفت' })).toBeVisible();

  await page.getByRole('button', { name: 'اپراتور جدید' }).click();
  await expect(page.getByRole('dialog', { name: 'تعریف اپراتور جدید' })).toBeVisible();
  await expect(page.getByLabel('نام کاربری')).toBeVisible();
  await expect(page.getByLabel('رمز عبور')).toBeVisible();
  await expect(page.getByLabel('نقش')).toHaveValue('Operator');
  await page.getByRole('button', { name: 'انصراف' }).click();
  await expect(page.getByRole('dialog', { name: 'تعریف اپراتور جدید' })).toHaveCount(0);
});

test('dashboard shows actionable Persian error UX', async ({ browser }) => {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-admin',
      fullName: 'مدیر تست',
      userName: 'admin',
      email: 'admin@gamenet.local',
      role: 'Admin',
      isActive: true,
      permissions: ['user.manage', 'session.start', 'session.manage', 'session.settle', 'buffet.sell', 'buffet.inventory', 'finance.view', 'shift.manage', 'approval.decide']
    })
  }));
  await page.route('**/api/dashboard', route => route.fulfill({ status: 500, contentType: 'application/json', body: '{}' }));
  await page.route('**/hubs/**', route => route.abort());
  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('alert')).toContainText('ارتباط با سرور برقرار نشد');
  await expect(page.getByRole('alert').getByRole('button', { name: 'تلاش مجدد' })).toBeVisible();
  await context.close();
});

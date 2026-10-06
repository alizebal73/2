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

test('dashboard station sort strip orders visible PCs by selected field', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-station-sort',
      fullName: 'مدیر مرتب‌سازی',
      userName: 'sort_admin',
      email: 'sort@gamenet.local',
      role: 'Admin',
      isActive: true,
      permissions: ['session.start']
    })
  }));

  await page.route('**/api/customers', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([])
  }));

  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      totalStations: 3,
      generatedAt: new Date().toISOString(),
      stations: [
        { id: 'sort-03', name: 'PC ۰۳', zone: 'pc', type: 'PC', ratePerHour: 95000, state: 'busy', network: 1, remainingMinutes: 25, customerDebt: 90000 },
        { id: 'sort-01', name: 'PC ۰۱', zone: 'pc', type: 'PC', ratePerHour: 95000, state: 'free', network: 1, remainingMinutes: 0, customerDebt: 0 },
        { id: 'sort-02', name: 'PC ۰۲', zone: 'pc', type: 'PC', ratePerHour: 95000, state: 'busy', network: 2, remainingMinutes: 90, customerDebt: 20000 }
      ]
    })
  }));

  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.locator('[data-station-id="sort-01"]').waitFor();

  const strip = page.locator('.station-sort-strip');
  await expect(strip).toBeVisible();
  await expect(strip).toContainText('رایانه');
  await expect(strip).toContainText('شناسه');
  await expect(strip).toContainText('نام خانوادگی');
  await expect(strip).toContainText('زمان باقی‌مانده');
  await expect(strip).toContainText('بدهکاری');
  await expect(strip).toContainText('توضیحات');
  await expect(strip).toContainText('وضعیت رایانه');

  await strip.getByRole('button', { name: /زمان باقی‌مانده/ }).click();
  await expect(strip.getByRole('button', { name: /زمان باقی‌مانده ↑/ })).toHaveAttribute('aria-pressed', 'true');

  let ids = await page.locator('[data-station-id]').evaluateAll(nodes => nodes.map(node => node.getAttribute('data-station-id')));
  expect(ids).toEqual(['sort-01', 'sort-03', 'sort-02']);

  await strip.getByRole('button', { name: /زمان باقی‌مانده ↑/ }).click();
  await expect(strip.getByRole('button', { name: /زمان باقی‌مانده ↓/ })).toHaveAttribute('aria-pressed', 'true');

  ids = await page.locator('[data-station-id]').evaluateAll(nodes => nodes.map(node => node.getAttribute('data-station-id')));
  expect(ids).toEqual(['sort-02', 'sort-03', 'sort-01']);

  await page.getByRole('button', { name: 'لیست' }).click();
  await expect(page.locator('.station-grid.v-list')).toBeVisible();
  await expect(page.locator('[data-station-id="sort-02"]')).toBeVisible();
});

test('operator buffet view exposes sales only, not inventory mutation controls', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-buffet-operator',
      fullName: 'اپراتور بوفه',
      userName: 'buffet_operator',
      email: 'buffet-operator@gamenet.local',
      role: 'Operator',
      isActive: true,
      permissions: ['buffet.sell', 'buffet.inventory']
    })
  }));

  await page.route('**/api/buffet/products', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([{
      id: 'operator-cola',
      name: 'نوشابه',
      category: 'نوشیدنی',
      price: 35000,
      buyPrice: 20000,
      stock: 3,
      warehouseStock: 10,
      showcaseStock: 3,
      minimumStock: 1,
      unit: 'عدد',
      lowStock: false,
      active: true
    }])
  }));
  await page.route('**/api/buffet/reports/today-sales', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ date: new Date().toISOString().slice(0, 10), totalQuantity: 0, totalRevenue: 0, products: [] })
  }));
  await page.route('**/api/sessions/active', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }));
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'بوفه' }).click();

  await expect(page.getByText('نوشابه')).toBeVisible();
  await expect(page.getByRole('button', { name: 'افزودن به سبد' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'ثبت خرید به انبار' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '+ ویترین' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: '− انبار' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: /ضایعات انبار/ })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'ویرایش' })).toHaveCount(0);
});

test('buffet separates warehouse, showcase and today sales', async ({ page }) => {
  let warehouseStock = 5;
  let showcaseStock = 2;

  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-buffet-flow',
      fullName: 'مدیر بوفه',
      userName: 'buffet_admin',
      email: 'buffet@gamenet.local',
      role: 'Admin',
      isActive: true,
      permissions: ['buffet.sell', 'buffet.inventory']
    })
  }));

  const productsPayload = () => [{
    id: 'product-cola',
    name: 'نوشابه',
    category: 'نوشیدنی',
    price: 35000,
    buyPrice: 20000,
    stock: showcaseStock,
    warehouseStock,
    showcaseStock,
    minimumStock: 1,
    unit: 'عدد',
    lowStock: showcaseStock <= 1,
    active: true
  }];

  await page.route('**/api/buffet/products', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(productsPayload())
  }));

  await page.route('**/api/buffet/inventory-transactions', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([])
  }));

  await page.route('**/api/buffet/reports/today-sales', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      date: new Date().toISOString().slice(0, 10),
      totalQuantity: 7,
      totalRevenue: 245000,
      products: [{
        productId: 'product-cola',
        productName: 'نوشابه',
        unit: 'عدد',
        quantity: 7,
        revenue: 245000
      }]
    })
  }));

  await page.route('**/api/sessions/active', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([])
  }));

  await page.route('**/api/buffet/products/product-cola/showcase-transfer', async route => {
    const body = await route.request().postDataJSON();
    const quantity = Number(body.quantity);
    if (quantity <= 0 || quantity > warehouseStock) {
      return route.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'موجودی انبار کافی نیست.' })
      });
    }
    warehouseStock -= quantity;
    showcaseStock += quantity;
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'product-cola',
        warehouseStock,
        showcaseStock
      })
    });
  });

  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'بوفه' }).click();

  await expect(page.getByText('موجودی انبار')).toBeVisible();
  await expect(page.getByText('موجودی ویترین')).toBeVisible();
  await expect(page.getByText('فروش امروز هر محصول')).toBeVisible();
  await expect(page.getByText(/فروش امروز: ۷ عدد/)).toBeVisible();
  await expect(page.getByText(/انبار: ۵ عدد · ویترین: ۲ عدد/)).toBeVisible();

  await page.getByRole('button', { name: '+ ویترین' }).click();
  await expect(page.getByText(/انبار: ۴ عدد · ویترین: ۳ عدد/)).toBeVisible();
  await expect(page.getByText(/موجودی انبار/)).toBeVisible();
});

test('F1 customer workspace and station right-click Agent controls stay wired', async ({ page }) => {
  const customerId = '11111111-1111-4111-8111-111111111111';
  const agentId = '22222222-2222-4222-8222-222222222222';

  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-f1-context',
      fullName: 'اپراتور تست',
      userName: 'operator_f1',
      email: 'operator-f1@gamenet.local',
      role: 'Operator',
      isActive: true,
      permissions: ['customer.wallet', 'customer.debt', 'client.control', 'client.power', 'session.manage', 'session.settle']
    })
  }));

  await page.route('**/api/customers', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([{
      id: customerId,
      code: '1006',
      username: 'ali123',
      name: 'علی رضایی',
      alias: 'Ali',
      nationalId: '0012345678',
      mobile: '09121234567',
      vip: 'gold',
      wallet: 350000,
      debt: 120000,
      giftCredit: 20000,
      freeTimeMinutes: 30,
      discountLevel: 10,
      lastSeen: new Date().toISOString(),
      status: 'active',
      concurrentLoginLimit: 2,
      vipPackageName: 'VIP Gold',
      notes: 'مشتری ثابت'
    }])
  }));

  await page.route(\`**/api/customers/\${customerId}/history\`, route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([
      {
        id: 'history-1',
        type: 'charge',
        description: 'شارژ کیف پول',
        amount: 200000,
        createdAt: new Date(Date.now() - 3600000).toISOString()
      }
    ])
  }));

  await page.route('**/api/agent/devices', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([{
      agentId,
      deviceId: 'device-f1-01',
      name: 'PC-03 Client',
      stationId: 'pc-03',
      stationName: 'PC ۰۳',
      isOnline: true,
      isLocked: false,
      kioskEnabled: false,
      lockOnDisconnect: true,
      lastSeenAt: new Date().toISOString(),
      connectedAt: new Date().toISOString(),
      agentVersion: '1.0.0',
      lifecycleState: 'Running'
    }])
  }));

  await page.route(\`**/api/agent/devices/\${agentId}/commands\`, route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      commandId: '33333333-3333-4333-8333-333333333333',
      agentDeviceId: agentId,
      commandType: 'ping',
      status: 'Sent',
      requestedAt: new Date().toISOString(),
      sentAt: new Date().toISOString(),
      succeeded: null
    })
  }));

  await page.route('**/api/agent/commands/33333333-3333-4333-8333-333333333333', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      commandId: '33333333-3333-4333-8333-333333333333',
      agentDeviceId: agentId,
      commandType: 'ping',
      status: 'Succeeded',
      requestedAt: new Date().toISOString(),
      sentAt: new Date().toISOString(),
      completedAt: new Date().toISOString(),
      succeeded: true,
      resultMessage: 'ارتباط Agent سالم است.'
    })
  }));

  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      totalStations: 1,
      generatedAt: new Date().toISOString(),
      stations: [{
        id: 'pc-03',
        name: 'PC ۰۳',
        zone: 'pc',
        type: 'PC',
        ratePerHour: 95000,
        state: 'busy',
        network: 1,
        customerCode: '1006',
        customerUsername: 'ali123',
        customerFullName: 'علی رضایی',
        customerDebt: 120000,
        customerNote: 'مشتری ثابت',
        remainingMinutes: 84,
        serverSessionId: '44444444-4444-4444-8444-444444444444',
        agentId,
        agentOnline: true,
        agentLocked: false,
        agentKioskEnabled: false,
        agentLockOnDisconnect: true,
        agentLifecycleState: 'Running'
      }]
    })
  }));

  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.locator('[data-station-id="pc-03"]').waitFor();

  await page.keyboard.press('F1');
  await expect(page.getByRole('dialog')).toContainText('عملیات مشتری · F1');

  const search = page.getByPlaceholder('مثلاً 1006 یا ali123 یا 0912...');
  await search.fill('ali123');
  await page.getByRole('button', { name: 'نمایش مشتری' }).click();
  await expect(page.getByRole('dialog')).toContainText('علی رضایی');
  await expect(page.getByRole('dialog')).toContainText('۳۵۰٬۰۰۰ تومان');
  await expect(page.getByRole('dialog')).toContainText('شارژ کیف پول');

  await page.keyboard.press('Escape');
  await page.locator('[data-station-id="pc-03"]').click({ button: 'right' });
  await expect(page.locator('.context-menu')).toBeVisible();
  await expect(page.locator('.context-menu')).toContainText('Ping / بررسی ارتباط Agent');
  await expect(page.locator('.context-menu')).toContainText('راه‌اندازی مجدد Client');
  await expect(page.locator('.context-menu')).toContainText('خاموش کردن Client');

  await page.getByRole('button', { name: /Ping \/ بررسی ارتباط Agent/ }).click();
  await expect(page.getByText('ارتباط Agent سالم است.')).toBeVisible();
});

test('dashboard exposes accessible navigation, notifications and stale-state semantics', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-accessibility',
      fullName: 'مدیر دسترسی',
      userName: 'accessibility_admin',
      email: 'accessibility@gamenet.local',
      role: 'Admin',
      isActive: true,
      lastLoginAt: new Date().toISOString(),
      permissions: ['user.manage']
    })
  }));

  let dashboardCalls = 0;
  await page.route('**/api/dashboard', route => {
    dashboardCalls += 1;
    if (dashboardCalls < 2) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalStations: 0,
          generatedAt: new Date(Date.now() - 120000).toISOString(),
          stations: []
        })
      });
    }
    return route.fulfill({ status: 500, contentType: 'application/json', body: '{}' });
  });
  await page.route('**/api/notifications?*', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ items: [], unreadCount: 0 })
  }));
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'داشبورد' }).waitFor();

  const skipLink = page.getByRole('link', { name: 'پرش به محتوای اصلی' });
  await page.keyboard.press('Tab');
  await expect(skipLink).toBeFocused();
  await skipLink.press('Enter');
  await expect(page.locator('#main-content')).toBeFocused();

  await expect(page.getByRole('button', { name: 'داشبورد' })).toHaveAttribute('aria-current', 'page');

  const bell = page.getByRole('button', { name: /اعلان‌ها/ });
  await expect(bell).toHaveAttribute('aria-expanded', 'false');
  await bell.click();
  await expect(bell).toHaveAttribute('aria-expanded', 'true');
  await expect(page.getByRole('region', { name: 'مرکز اعلان‌ها' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(bell).toHaveAttribute('aria-expanded', 'false');

  await page.getByRole('button', { name: 'تلاش مجدد برای دریافت اطلاعات از سرور' }).click();
  await expect(page.locator('.status-chip').filter({ hasText: 'API قطع' })).toBeVisible();
  await expect(page.getByText(/آخرین وضعیت معتبر/)).toBeVisible();
  await expect(page.getByText('دادهٔ زنده در دسترس نیست')).toBeVisible();
});

test('dashboard groups PCs by Internet 1/2 without changing station data', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-network-grouping',
      fullName: 'مدیر شبکه',
      userName: 'network_admin',
      email: 'network@gamenet.local',
      role: 'Admin',
      isActive: true,
      lastLoginAt: new Date().toISOString(),
      permissions: ['session.start', 'session.manage']
    })
  }));
  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      totalStations: 4,
      generatedAt: new Date().toISOString(),
      stations: [
        { id: 'net-pc-01', name: 'PC ۰۱', zone: 'pc', type: 'PC', ratePerHour: 95000, state: 'free', network: 1 },
        { id: 'net-pc-02', name: 'PC ۰۲', zone: 'pc', type: 'PC', ratePerHour: 95000, state: 'free', network: 2 },
        { id: 'net-pc-03', name: 'PC ۰۳', zone: 'pc', type: 'PC', ratePerHour: 95000, state: 'busy', network: 1 },
        { id: 'net-pc-04', name: 'PC ۰۴', zone: 'pc', type: 'PC', ratePerHour: 95000, state: 'free', network: 2 },
      ]
    })
  }));
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.locator('[data-station-id="net-pc-01"]').waitFor();

  const groupBy = page.getByLabel('گروه‌بندی PC');
  await expect(groupBy).toHaveValue('state');
  await groupBy.selectOption('network');

  await expect(page.locator('.pc-group-title', { hasText: 'اینترنت ۱' }).last()).toHaveText('اینترنت ۱ · 2');
  await expect(page.locator('.pc-group-title', { hasText: 'اینترنت ۲' }).last()).toHaveText('اینترنت ۲ · 2');
  await expect(page.locator('[data-station-id="net-pc-01"]')).toBeVisible();
  await expect(page.locator('[data-station-id="net-pc-04"]')).toBeVisible();

  await groupBy.selectOption('remaining');
  await expect(page.locator('.pc-group-title').first()).toBeVisible();

  await page.setViewportSize({ width: 520, height: 900 });
  await expect(page.getByRole('button', { name: 'داشبورد' })).toBeVisible();
  await expect(page.locator('[data-station-id="net-pc-01"]')).toBeVisible();
});

test('dashboard exposes real Sessions and Stations report', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-session-report',
      fullName: 'مدیر گزارش',
      userName: 'report_admin',
      email: 'report@gamenet.local',
      role: 'Admin',
      isActive: true,
      lastLoginAt: new Date().toISOString(),
      permissions: ['finance.view']
    })
  }));
  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ totalStations: 0, generatedAt: new Date().toISOString(), stations: [] })
  }));
  await page.route('**/api/finance/**', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([])
  }));
  await page.route('**/api/shifts/**', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([])
  }));
  await page.route('**/api/reports/sessions*', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      page: 1,
      pageSize: 50,
      total: 2,
      summary: {
        sessionCount: 2,
        billableMinutes: 90,
        revenue: 80000,
        averageMinutes: 45,
        stations: [{
          stationId: 'pc-01',
          stationName: 'PC ۰۱',
          zone: 'pc',
          sessionCount: 2,
          billableMinutes: 90,
          revenue: 80000
        }]
      },
      items: [
        {
          id: 'session-report-1',
          startAt: new Date(Date.now() - 3600000).toISOString(),
          endAt: new Date(Date.now() - 1800000).toISOString(),
          state: 'Completed',
          stationId: 'pc-01',
          stationName: 'PC ۰۱',
          zone: 'pc',
          stationType: 'PC',
          customerId: 'customer-1',
          customerName: 'رضا تست',
          customerCode: 'R001',
          customerUsername: 'reza_test',
          appUserId: 'e2e-session-report',
          operator: 'مدیر گزارش',
          persons: 1,
          billableMinutes: 30,
          totalAmount: 50000
        },
        {
          id: 'session-report-2',
          startAt: new Date(Date.now() - 7200000).toISOString(),
          endAt: new Date(Date.now() - 5400000).toISOString(),
          state: 'Completed',
          stationId: 'pc-01',
          stationName: 'PC ۰۱',
          zone: 'pc',
          stationType: 'PC',
          customerId: 'customer-2',
          customerName: 'علی تست',
          customerCode: 'A002',
          customerUsername: 'ali_test',
          appUserId: 'e2e-session-report',
          operator: 'مدیر گزارش',
          persons: 1,
          billableMinutes: 60,
          totalAmount: 30000
        }
      ]
    })
  }));
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'گزارش‌ها' }).click();
  await page.locator('.report-categories').getByRole('button', { name: 'جلسات و ایستگاه‌ها' }).click();

  await expect(page.getByTestId('session-report')).toBeVisible();
  await expect(page.getByTestId('session-report-row')).toHaveCount(2);
  await expect(page.getByText('درآمد جلسات')).toBeVisible();
  await expect(page.getByText('۸۰٬۰۰۰ تومان')).toBeVisible();
  await expect(page.getByTestId('session-report')).toContainText('PC ۰۱');
  await expect(page.getByTestId('session-report')).toContainText('رضا تست');
});


test('dashboard enforces report export permission in Sessions report', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-report-operator',
      fullName: 'اپراتور گزارش',
      userName: 'report_operator',
      email: 'report-operator@gamenet.local',
      role: 'Operator',
      isActive: true,
      lastLoginAt: new Date().toISOString(),
      permissions: ['finance.view']
    })
  }));
  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ totalStations: 0, generatedAt: new Date().toISOString(), stations: [] })
  }));
  await page.route('**/api/reports/sessions*', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      page: 1,
      pageSize: 50,
      total: 1,
      summary: { sessionCount: 1, billableMinutes: 30, revenue: 50000, averageMinutes: 30, stations: [] },
      items: [{
        id: 'session-export-permission',
        startAt: new Date(Date.now() - 1800000).toISOString(),
        endAt: new Date().toISOString(),
        state: 'Completed',
        stationId: 'pc-01',
        stationName: 'PC ۰۱',
        zone: 'pc',
        stationType: 'PC',
        customerId: 'customer-1',
        customerName: 'مشتری گزارش',
        customerCode: 'R001',
        customerUsername: 'report_customer',
        appUserId: 'operator-1',
        operator: 'اپراتور گزارش',
        persons: 1,
        billableMinutes: 30,
        totalAmount: 50000
      }]
    })
  }));
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'گزارش‌ها' }).click();
  await page.locator('.report-categories').getByRole('button', { name: 'جلسات و ایستگاه‌ها' }).click();

  await expect(page.getByTestId('session-report')).toBeVisible();
  await expect(page.getByTestId('session-report')).toContainText('مشتری گزارش');
  await expect(page.getByRole('button', { name: '📤 خروجی جلسات' })).toBeDisabled();
  await expect(page.getByTitle('دسترسی خروجی گزارش ندارید')).toHaveCount(1);
});

test('dashboard exposes real Customer and VIP report', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-customer-report',
      fullName: 'مدیر مشتری',
      userName: 'customer_report',
      email: 'customer-report@gamenet.local',
      role: 'Admin',
      isActive: true,
      lastLoginAt: new Date().toISOString(),
      permissions: ['customer.manage', 'customer.wallet', 'customer.debt']
    })
  }));
  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ totalStations: 0, generatedAt: new Date().toISOString(), stations: [] })
  }));
  await page.route('**/api/reports/customers*', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      page: 1,
      pageSize: 50,
      total: 1,
      summary: {
        customerCount: 1,
        vipCount: 1,
        activeVipCount: 1,
        debtorCount: 1,
        walletTotal: 120000,
        debtTotal: 55000,
        sessionCount: 2,
        sessionRevenue: 170000
      },
      items: [{
        customerId: 'customer-vip-1',
        code: 'C001',
        username: 'customer_vip',
        name: 'رضا VIP',
        vipTier: 'gold',
        packageName: 'Gold VIP',
        vipActivatedAt: new Date(Date.now() - 86400000).toISOString(),
        vipExpiresAt: new Date(Date.now() + 86400000 * 29).toISOString(),
        vipDailyMinutes: 180,
        vipTotalMinutes: 3000,
        vipDiscountPercent: 10,
        usedTodayMinutes: 45,
        usedTotalMinutes: 120,
        remainingTodayMinutes: 135,
        remainingTotalMinutes: 2880,
        walletBalance: 120000,
        debt: 55000,
        sessionCount: 2,
        sessionRevenue: 170000,
        lastSessionAt: new Date(Date.now() - 3600000).toISOString(),
        status: 'vip-active',
        notes: 'VIP تست'
      }]
    })
  }));
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'گزارش‌ها' }).click();
  await page.locator('.report-categories').getByRole('button', { name: 'مشتری و VIP' }).click();

  await expect(page.getByTestId('customer-vip-report')).toBeVisible();
  await expect(page.getByTestId('customer-vip-row')).toHaveCount(1);
  await expect(page.getByTestId('customer-vip-report')).toContainText('رضا VIP');
  await expect(page.getByTestId('customer-vip-report')).toContainText('Gold VIP');
  await expect(page.getByRole('columnheader', { name: 'درآمد جلسات' })).toBeVisible();
  await expect(page.locator('.summary-grid').getByText('۱۷۰٬۰۰۰ تومان')).toBeVisible();
});

test('dashboard exposes real Users and Shift report', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-users-shift-report',
      fullName: 'مدیر شیفت',
      userName: 'users_shift_report',
      email: 'users-shift-report@gamenet.local',
      role: 'Admin',
      isActive: true,
      lastLoginAt: new Date().toISOString(),
      permissions: ['shift.manage', 'payroll.view', 'user.manage']
    })
  }));
  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ totalStations: 0, generatedAt: new Date().toISOString(), stations: [] })
  }));
  await page.route('**/api/reports/users-shifts*', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      page: 1,
      pageSize: 50,
      total: 1,
      summary: {
        userCount: 1,
        activeUserCount: 1,
        shiftCount: 2,
        closedShiftCount: 1,
        shiftRevenue: 450000,
        shiftCashSales: 300000,
        shiftExpenses: 50000,
        shiftDifference: 0,
        sessionCount: 4,
        sessionRevenue: 450000,
        payrollPaid: 80000,
        payrollEmployeePayable: 120000
      },
      items: [{
        userId: 'operator-1',
        fullName: 'اپراتور تست',
        userName: 'operator_test',
        role: 'Operator',
        isActive: true,
        payType: 'hourly',
        employeePayable: 120000,
        ownerReceivable: 0,
        paidThisPeriod: 80000,
        bonusThisPeriod: 10000,
        deductionThisPeriod: 0,
        shiftCount: 2,
        closedShiftCount: 1,
        shiftRevenue: 450000,
        shiftCashSales: 300000,
        shiftExpenses: 50000,
        shiftDifference: 0,
        sessionCount: 4,
        sessionRevenue: 450000,
        lastLoginAt: new Date().toISOString()
      }]
    })
  }));
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'گزارش‌ها' }).click();
  await page.locator('.report-categories').getByRole('button', { name: 'کاربران و شیفت' }).click();

  await expect(page.getByTestId('users-shift-report')).toBeVisible();
  await expect(page.getByTestId('users-shift-row')).toHaveCount(1);
  await expect(page.getByTestId('users-shift-report')).toContainText('اپراتور تست');
  await expect(page.getByRole('columnheader', { name: 'فروش شیفت' })).toBeVisible();
  await expect(page.locator('.summary-grid').getByText('۴۵۰٬۰۰۰ تومان')).toBeVisible();
  await expect(page.getByText('حقوق پرداخت‌شده')).toBeVisible();
});

test('dashboard shows server-backed notifications and persists read state', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-notify-user',
      fullName: 'اپراتور اعلان',
      userName: 'notify_operator',
      email: 'notify@gamenet.local',
      role: 'Operator',
      isActive: true,
      lastLoginAt: new Date().toISOString(),
      permissions: ['finance.view', 'buffet.inventory']
    })
  }));
  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ totalStations: 0, generatedAt: new Date().toISOString(), stations: [] })
  }));

  let notificationState = {
    items: [{
      id: 'notification-1',
      appUserId: 'e2e-notify-user',
      category: 'buffet.low-stock',
      title: 'موجودی بوفه کم شد',
      detail: 'موجودی «نوشابه» به ۰ رسید.',
      level: 'Critical',
      entityName: 'Product',
      entityId: 'product-1',
      isRead: false,
      createdAt: new Date().toISOString(),
      readAt: null
    }],
    unreadCount: 1
  };

  await page.route('**/api/notifications?*', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(notificationState)
  }));
  await page.route('**/api/notifications/*/read', async route => {
    notificationState = {
      ...notificationState,
      unreadCount: 0,
      items: notificationState.items.map(item => ({ ...item, isRead: true, readAt: new Date().toISOString() }))
    };
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ read: true }) });
  });
  await page.route('**/api/notifications/read-all', async route => {
    notificationState = {
      ...notificationState,
      unreadCount: 0,
      items: notificationState.items.map(item => ({ ...item, isRead: true, readAt: new Date().toISOString() }))
    };
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ updated: 1 }) });
  });
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  const bell = page.getByRole('button', { name: 'اعلان‌ها' });
  await expect(bell).toContainText('۱');
  await bell.click();
  await expect(page.getByText('موجودی بوفه کم شد')).toBeVisible();
  await expect(page.getByText('موجودی «نوشابه» به ۰ رسید.')).toBeVisible();

  await page.getByText('موجودی بوفه کم شد').click();
  await expect(bell).toContainText('۰');
  await expect(page.getByText('اعلان‌ها', { exact: true })).toBeVisible();
});

test('dashboard exposes real Audit Explorer', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-audit-operator',
      fullName: 'اپراتور Audit',
      userName: 'audit_operator',
      email: 'audit@gamenet.local',
      role: 'Operator',
      isActive: true,
      lastLoginAt: new Date().toISOString(),
      permissions: ['audit.view']
    })
  }));

  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ totalStations: 0, generatedAt: new Date().toISOString(), stations: [] })
  }));

  await page.route('**/api/audit*', route => {
    const url = new URL(route.request().url());
    const action = url.searchParams.get('action');
    const allRows = [
      {
        id: 'audit-1',
        createdAt: new Date().toISOString(),
        appUserId: 'e2e-audit-operator',
        operator: 'اپراتور Audit',
        action: 'SessionStarted',
        entityName: 'Session',
        entityId: 'session-01',
        details: 'شروع جلسه PC ۰۱',
      },
      {
        id: 'audit-2',
        createdAt: new Date(Date.now() - 60000).toISOString(),
        appUserId: 'e2e-audit-operator',
        operator: 'اپراتور Audit',
        action: 'TariffUpdated',
        entityName: 'Tariff',
        entityId: 'tariff-01',
        details: 'ویرایش تعرفه PC',
      },
    ];
    const items = action ? allRows.filter(row => row.action.includes(action)) : allRows;
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ page: 1, pageSize: 50, total: items.length, items })
    });
  });
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'گزارش‌ها' }).click();
  await expect(page.getByTestId('audit-explorer')).toBeVisible();
  await expect(page.getByTestId('audit-row')).toHaveCount(2);
  await page.getByLabel('عملیات').fill('TariffUpdated');
  await expect(page.getByTestId('audit-row')).toHaveCount(1);
  await expect(page.getByTestId('audit-row')).toContainText('TariffUpdated');
  await expect(page.getByText(/۱ مورد در این صفحه/)).toBeVisible();
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

test('settings information architecture supports search and category navigation', async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-settings-admin',
      fullName: 'مدیر تنظیمات',
      userName: 'settings_admin',
      email: 'settings@gamenet.local',
      role: 'Admin',
      isActive: true,
      lastLoginAt: new Date().toISOString(),
      permissions: ['user.manage']
    })
  }));
  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ totalStations: 0, generatedAt: new Date().toISOString(), stations: [] })
  }));
  await page.route('**/api/notifications?*', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ items: [], unreadCount: 0 })
  }));
  let settingsPutBody = null;
  let backupItems = [];
  await page.route('**/api/backup', async route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: backupItems,
          canManage: true,
          canRestore: true
        })
      });
    }
    return route.fallback();
  });
  await page.route('**/api/backup/create', async route => {
    if (route.request().method() !== 'POST') return route.fallback();
    const item = {
      fileName: 'gamenet-20261005-040000-000.gnbackup',
      createdAt: new Date().toISOString(),
      sizeBytes: 1024,
      sha256: 'E2E-BACKUP-HASH',
      verified: true,
      verificationMessage: 'نسخهٔ پشتیبان معتبر است.'
    };
    backupItems = [item, ...backupItems];
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(item) });
  });
  await page.route('**/api/backup/*/verify', async route => {
    if (route.request().method() !== 'POST') return route.fallback();
    backupItems = backupItems.map(item => ({ ...item, verified: true, verificationMessage: 'نسخهٔ پشتیبان معتبر است.' }));
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ valid: true, message: 'نسخهٔ پشتیبان سالم و قابل‌بازیابی است.' }) });
  });
  await page.route('**/api/backup/*/restore', async route => {
    if (route.request().method() !== 'POST') return route.fallback();
    return route.fulfill({ status: 202, contentType: 'application/json', body: JSON.stringify({ pending: true, message: 'بازیابی آماده شد و در راه‌اندازی بعدی Server اعمال می‌شود.' }) });
  });
  await page.route('**/api/settings', async route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          scope: 'global',
          values: {
            operatorDiscount: 12,
            serverAddress: '192.168.0.9:5080',
            sessionMode: 'settle'
          },
          definitions: []
        })
      });
    }
    settingsPutBody = route.request().postDataJSON();
    const values = settingsPutBody?.values ?? {};
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        scope: 'global',
        values,
        changedKeys: Object.keys(values)
      })
    });
  });
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'تنظیمات', exact: true }).click();

  const settingsNav = page.getByTestId('settings-category-nav');
  await expect(settingsNav).toBeVisible();
  await expect(page.getByLabel('جست‌وجوی تنظیمات')).toBeVisible();
  await expect(page.getByTestId('settings-server-sync')).toContainText('متصل و قابل ذخیره روی Server');
  const saveServerButton = page.getByRole('button', { name: '💾 ذخیره روی سرور' });
  await expect(saveServerButton).toBeEnabled();
  await saveServerButton.click();
  await expect(page.getByText('تنظیمات عملیاتی روی سرور ذخیره شد')).toBeVisible();
  expect(settingsPutBody.values.operatorDiscount).toBe(12);
  expect(settingsPutBody.values.serverAddress).toBe('192.168.0.9:5080');
  await expect(page.locator('#settings-section-sessions')).toHaveCount(1);
  await expect(page.locator('#settings-section-network')).toHaveCount(1);
  await page.getByRole('button', { name: /جلسه و تسویه/ }).click();
  await expect(page.locator('#settings-section-sessions')).toBeVisible();
  await page.getByRole('button', { name: /شبکه و اتصال/ }).click();
  await expect(page.locator('#settings-section-network')).toBeVisible();

  await page.getByLabel('جست‌وجوی تنظیمات').fill('بکاپ');
  const backupCategory = page.getByRole('button', { name: /داده و پشتیبان‌گیری/ });
  await expect(backupCategory).toHaveCount(1);
  await backupCategory.click();

  const backupSection = page.locator('#settings-section-backup');
  await expect(backupSection).toBeVisible();
  const manualBackup = backupSection.getByRole('button', { name: /📦 بکاپ دستی الان/ });
  await expect(manualBackup).toBeEnabled();
  await manualBackup.click();
  await expect(page.getByText('بکاپ واقعی با موفقیت ایجاد شد')).toBeVisible();
  await expect(backupSection.getByText(/gamenet-20261005/)).toBeVisible();
  const verifyBackup = backupSection.getByRole('button', { name: '✅ بررسی' });
  await verifyBackup.click();
  await expect(page.getByText('نسخهٔ پشتیبان سالم و قابل‌بازیابی است.')).toBeVisible();
  page.once('dialog', dialog => dialog.accept());
  await backupSection.getByRole('button', { name: /آماده‌سازی Restore/ }).click();
  await expect(page.getByText(/بازیابی آماده شد و در راه‌اندازی بعدی Server اعمال می‌شود/)).toBeVisible();
  await expect(backupSection).toContainText('دیتابیس SQLite و DataProtection Keys');

  await page.getByLabel('جست‌وجوی تنظیمات').fill('هات‌کی');
  await expect(page.getByRole('button', { name: /میانبرها/ })).toHaveCount(1);
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


test('client experience consumes server-backed catalog and customer state', async ({ page }) => {
  let launchCalls = 0;
  let stopCalls = 0;

  await page.route('**/api/client/identity', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      deviceId: 'agent-device-e2e-01',
      stationId: 'station-e2e-01',
      stationName: 'PC ۰۱',
      isOnline: true
    })
  }));

  await page.route('**/api/client/catalog', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      deviceId: 'agent-device-e2e-01',
      stationId: 'station-e2e-01',
      stationName: 'PC ۰۱',
      games: [{
        id: 'game-e2e-cs2',
        name: 'Counter-Strike 2',
        category: 'FPS',
        genre: 'FPS',
        version: '1.0',
        status: 'online',
        cover: '',
        trailer: '',
        connectionType: 'آنلاین',
        icon: '🎮',
        description: 'FPS · آنلاین',
        hasPoolAccount: true
      }],
      buffet: [{
        id: 'buffet-e2e-cola',
        name: 'نوشابه واقعی',
        category: 'نوشیدنی',
        price: 35000,
        unit: 'عدد',
        available: true,
        icon: '🥤'
      }]
    })
  }));

  await page.route('**/api/customer-auth/login', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      authenticated: true,
      customerId: 'customer-e2e-01',
      username: 'e2e_customer',
      fullName: 'مشتری تست',
      loginId: 'login-e2e-01',
      activeCount: 1,
      limit: 1,
      balance: 250000,
      freeMoney: 0,
      freeTimeMinutes: 0,
      vipTier: 'Normal'
    })
  }));

  await page.route('**/api/client/game/launch', async route => {
    launchCalls += 1;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        commandId: 'command-launch-e2e-01',
        status: 'Sent',
        message: 'اجرای بازی برای Agent ارسال شد.'
      })
    });
  });

  await page.route('**/api/client/game/stop', async route => {
    stopCalls += 1;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        commandId: 'command-stop-e2e-01',
        status: 'Sent',
        message: 'توقف بازی برای Agent ارسال شد.'
      })
    });
  });

  await page.route('**/api/customer-auth/state?*', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      authenticated: true,
      customerId: 'customer-e2e-01',
      loginId: 'login-e2e-01',
      username: 'e2e_customer',
      fullName: 'مشتری تست',
      balance: 250000,
      freeMoney: 0,
      freeTimeMinutes: 0,
      vipTier: 'Normal',
      isLocked: false,
      session: {
        id: 'session-e2e-01',
        state: 'Active',
        startAt: new Date().toISOString(),
        endAt: new Date(Date.now() + 3600000).toISOString(),
        stationName: 'PC ۰۱'
      }
    })
  }));
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/client', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'ورود به سیستم' }).waitFor();
  await page.locator('#client-login-id').fill('e2e_customer');
  await page.locator('input[type="password"]').fill('secret');
  await page.getByRole('button', { name: 'ورود به سیستم' }).click();

  await expect(page.getByText('مشتری تست')).toBeVisible();
  await expect(page.getByText('Counter-Strike 2')).toBeVisible();

  await page.getByRole('button', { name: /Counter-Strike 2/ }).click();
  await expect(page.locator('.client-running-badge').filter({ hasText: 'در حال اجرا' }).first()).toBeVisible();
  await expect.poll(() => launchCalls).toBe(1);

  await page.getByRole('button', { name: '■ توقف بازی' }).click();
  await expect.poll(() => stopCalls).toBe(1);

  await page.getByText('منوی بوفه').first().click();
  await expect(page.getByText('نوشابه واقعی')).toBeVisible();
  await expect(page.getByText('۳۵٬۰۰۰ ت')).toBeVisible();

  await expect(page.getByText('۰۳:')).not.toBeVisible();
});

 
test('pending payment customer card consolidates charges and buffet with three dense views', async ({ page }) => {
  const invoiceId = 'pending-invoice-1';
  const pending = {
    invoiceId,
    sessionId: 'pending-session-1',
    customerId: 'pending-customer-1',
    customerName: 'علی رضایی',
    customerCode: '1006',
    username: 'ali123',
    stationName: 'PC ۱۲',
    closedAt: new Date(Date.now() - 12 * 60000).toISOString(),
    waitingMinutes: 12,
    timeAmount: 250000,
    buffetTotal: 140000,
    otherAmount: 0,
    grossAmount: 390000,
    prepaidTotal: 160000,
    prepaidApplied: 160000,
    prepaidRemaining: 0,
    creditOrBenefitReduction: 0,
    amountDue: 230000,
    charges: [
      { id: 'charge-1', amount: 90000, method: 'cash', createdAt: new Date(Date.now() - 40 * 60000).toISOString() },
      { id: 'charge-2', amount: 20000, method: 'card', createdAt: new Date(Date.now() - 30 * 60000).toISOString() },
      { id: 'charge-3', amount: 50000, method: 'cash', createdAt: new Date(Date.now() - 20 * 60000).toISOString() },
    ],
    buffetItems: [
      { productId: 'cola', productName: 'نوشابه', quantity: 1, amount: 50000 },
      { productId: 'cake', productName: 'کیک', quantity: 1, amount: 90000 },
    ],
  };

  let pendingVisible = true;

  await page.route('**/api/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'e2e-pending',
      fullName: 'مدیر پرداخت',
      userName: 'pending_admin',
      email: 'pending@gamenet.local',
      role: 'Admin',
      isActive: true,
      permissions: ['session.settle', 'session.manage'],
    }),
  }));
  await page.route('**/api/customers', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify([]),
  }));
  await page.route('**/api/dashboard', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ totalStations: 0, generatedAt: new Date().toISOString(), stations: [] }),
  }));
  await page.route('**/api/dashboard/pending-settlements', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(pendingVisible ? [pending] : []),
  }));
  await page.route('**/api/pending-settlements/' + invoiceId + '/settle', async route => {
    pendingVisible = false;
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        invoiceId,
        sessionId: pending.sessionId,
        totalAmount: pending.amountDue,
        parts: [{ method: 'cash', amount: pending.amountDue }],
        walletBalanceAfter: 350000,
        freeMoneyBalanceAfter: 0,
        freeTimeMinutesAfter: 0,
        invoiceStatus: 'Paid',
        paidAt: new Date().toISOString(),
      }),
    });
  });
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await expect(page.getByText('علی رضایی')).toBeVisible();
  await expect(page.getByText('۲۳۰٬۰۰۰ تومان')).toBeVisible();
  await expect(page.getByText('شارژ ۳ مورد')).toBeVisible();
  await expect(page.getByText('بوفه ۲ عدد / ۲ قلم')).toBeVisible();

  await page.getByRole('button', { name: 'شارژ ۳ مورد' }).click();
  await expect(page.getByText('۹۰٬۰۰۰ تومان')).toBeVisible();

  await page.getByRole('button', { name: 'فشرده' }).last().click();
  await expect(page.locator('.pending-payment-list.v-compact')).toBeVisible();

  await page.getByRole('button', { name: 'لیست' }).last().click();
  await expect(page.locator('.pending-payment-list.v-list')).toBeVisible();

  await page.getByRole('button', { name: 'نقد' }).last().click();
  await expect(page.getByText('حساب علی رضایی تسویه شد.')).toBeVisible();
  await expect(page.getByText('علی رضایی')).toHaveCount(0);
});

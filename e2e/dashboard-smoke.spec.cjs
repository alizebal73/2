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
  await page.route('**/hubs/**', route => route.abort());

  await page.goto('http://127.0.0.1:4173/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'تنظیمات' }).click();

  const settingsNav = page.getByTestId('settings-category-nav');
  await expect(settingsNav).toBeVisible();
  await expect(page.getByLabel('جست‌وجوی تنظیمات')).toBeVisible();
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
  await expect(backupSection.getByRole('button', { name: '📦 بکاپ دستی الان' })).toBeDisabled();
  await expect(backupSection).toContainText('پشتیبان واقعی');

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

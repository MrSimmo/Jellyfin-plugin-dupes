// Live UI checks: AC-8, AC-12, AC-13, Review Focus 4, and the DF-R1 route guard for non-admins.
// Configuration only from environment variables, never written to disk (DF-C4):
//   JF_URL, JF_ADMIN_USER, JF_ADMIN_PW (optional), JF_NONADMIN_USER (optional), JF_NONADMIN_PW (optional),
//   OUT_DIR (optional, default /tmp/dupefinder-e2e; screenshots and the CSV land here)
import { mkdirSync } from 'node:fs';
import { chromium } from 'playwright';

const base = (process.env.JF_URL ?? '').replace(/\/$/, '');
const outDir = process.env.OUT_DIR ?? '/tmp/dupefinder-e2e';
if (!base || !process.env.JF_ADMIN_USER) {
    console.error('Set JF_URL and JF_ADMIN_USER');
    process.exit(2);
}
mkdirSync(outDir, { recursive: true });

// The view cache can keep an older copy of the page hidden in the DOM; always target the visible one.
const P = '#dupeFinderPage:not(.hide)';
const pluginUrl = `${base}/web/#/configurationpage?name=dupefinder`;
const failures = [];

function check(ok, message) {
    console.log(`${ok ? 'PASS' : 'FAIL'} ${message}`);
    if (!ok) failures.push(message);
}

async function login(page, user, password) {
    await page.goto(`${base}/web/#/login`);
    const name = page.locator('#txtManualName');
    await name.waitFor({ state: 'attached', timeout: 30000 });
    if (!(await name.isVisible())) {
        await page.locator('.btnManual').click();
    }
    await name.fill(user);
    await page.locator('#txtManualPassword').fill(password);
    await page.locator('.manualLoginForm button[type="submit"]').click();
    await page.waitForURL(/#\/home/, { timeout: 30000 });
}

async function waitForScanToFinish(page) {
    await page.waitForFunction((sel) => {
        const button = document.querySelector(`${sel} #dfScan`);
        return button && !button.disabled;
    }, P, { timeout: 600000 });
}

const browser = await chromium.launch();
try {
    const context = await browser.newContext({ acceptDownloads: true, viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();
    let scanRequests = 0;
    page.on('request', (request) => {
        if (request.url().includes('/DupeFinder/Scan')) scanRequests++;
    });

    await login(page, process.env.JF_ADMIN_USER, process.env.JF_ADMIN_PW ?? '');

    // AC-8: the dashboard sidebar link opens the page.
    await page.goto(`${base}/web/#/dashboard`);
    const menuLink = page.getByText('Duplicate & Unmatched Finder', { exact: true }).first();
    await menuLink.waitFor({ timeout: 30000 });
    await menuLink.click();
    await page.waitForURL(/configurationpage\?name=dupefinder/, { timeout: 30000 });
    const libraryBoxes = page.locator(`${P} #dfLibraries input[type="checkbox"]`);
    await libraryBoxes.first().waitFor({ state: 'attached', timeout: 30000 });
    const libraryCount = await libraryBoxes.count();
    check(libraryCount > 0, `AC-8 page opened from the sidebar with ${libraryCount} libraries`);
    await page.screenshot({ path: `${outDir}/01-form.png`, fullPage: true });

    // AC-12: every check on, all libraries, scan.
    for (const box of await page.locator(`${P} #dfDuplicateChecks input, ${P} #dfUnmatchedChecks input`).all()) {
        if (!(await box.isChecked())) await box.check({ force: true });
    }
    await page.click(`${P} #dfScan`);
    await page.locator(`${P} #dfResults`).waitFor({ state: 'visible', timeout: 600000 });
    await waitForScanToFinish(page);
    const stats = (await page.textContent(`${P} #dfStats`)).trim();
    check(/^Scanned \d+ items in \d+\.\d s$/.test(stats), `AC-12 stats line "${stats}"`);
    const summary = await page.locator(`${P} #dfSummary li`).allTextContents();
    check(summary.length === 9, `AC-12 nine summary lines: ${summary.join(' | ')}`);
    const rows = page.locator(`${P} #dfRows tr`);
    const rowCount = await rows.count();
    console.log(`TABLE_ROWS ${rowCount}`);
    await page.screenshot({ path: `${outDir}/02-results.png` });

    if (rowCount > 0) {
        const [download] = await Promise.all([page.waitForEvent('download'), page.click(`${P} #dfDownload`)]);
        const fileName = download.suggestedFilename();
        check(/^dupefinder-\d{8}-\d{4}\.csv$/.test(fileName), `AC-12 CSV file name ${fileName}`);
        await download.saveAs(`${outDir}/${fileName}`);
        console.log(`CSV ${outDir}/${fileName}`);
    }

    // AC-13: Identify opens Jellyfin's dialog; cancelling leaves the row unchanged.
    const identify = page.locator(`${P} #dfRows button.df-identify`).first();
    if ((await identify.count()) === 0) {
        check(false, 'AC-13 no Identify button in the results (needs a Movie, Series, album or artist row)');
    } else {
        await identify.click();
        const lookup = page.locator('#txtLookupName');
        const opened = await lookup.waitFor({ state: 'visible', timeout: 30000 }).then(() => true, () => false);
        check(opened, 'AC-13 Identify opens Jellyfin\'s Identify dialog');
        await page.screenshot({ path: `${outDir}/03-identify.png` });
        await page.keyboard.press('Escape');
        if (await lookup.isVisible()) await page.locator('.dialog .btnCancel').first().click();
        await lookup.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
        const identified = await page.locator(`${P} #dfRows .df-status`, { hasText: 'Identified' }).count();
        check(identified === 0, 'AC-13 cancelling Identify leaves rows unchanged');
    }

    if (rowCount > 0) {
        const firstRow = rows.first();
        const itemId = await firstRow.getAttribute('data-item-id');
        const [editor] = await Promise.all([context.waitForEvent('page'), firstRow.locator('a.df-edit').click()]);
        const editorShown = await editor.locator('.editItemMetadataForm').first()
            .waitFor({ state: 'visible', timeout: 30000 }).then(() => true, () => false);
        check(editorShown && editor.url().includes(`#/metadata?id=${itemId}`), 'AC-13 Edit metadata opens the metadata editor for the item');
        await editor.screenshot({ path: `${outDir}/04-metadata.png` });
        await editor.close();

        const [details] = await Promise.all([context.waitForEvent('page'), firstRow.locator('a.df-open').click()]);
        await details.waitForLoadState('domcontentloaded');
        check(details.url().includes(`#/details?id=${itemId}`), 'AC-13 Open opens the item details page');
        await details.close();
    }

    // Review Focus 4: leave and come back; nothing duplicated, one click = one request.
    await page.goto(`${base}/web/#/dashboard`);
    await page.waitForTimeout(2000);
    await page.goto(pluginUrl);
    await page.locator(`${P} #dfScan`).waitFor({ timeout: 30000 });
    check((await libraryBoxes.count()) === libraryCount, 'Review Focus 4: library list not duplicated after revisit');
    const before = scanRequests;
    await page.click(`${P} #dfScan`);
    await waitForScanToFinish(page);
    check(scanRequests - before === 1, `Review Focus 4: one click sent ${scanRequests - before} scan request(s)`);

    // DF-R1: Jellyfin's dashboard route guard keeps non-admins off the page.
    if (process.env.JF_NONADMIN_USER) {
        const other = await browser.newContext();
        const userPage = await other.newPage();
        await login(userPage, process.env.JF_NONADMIN_USER, process.env.JF_NONADMIN_PW ?? '');
        await userPage.goto(pluginUrl);
        await userPage.waitForTimeout(5000);
        check((await userPage.locator('#dfScan').count()) === 0, 'DF-R1 a non-admin cannot open the plugin page');
        await other.close();
    }
} finally {
    await browser.close();
}

console.log(`${failures.length} failure(s)`);
process.exit(failures.length ? 1 : 0);

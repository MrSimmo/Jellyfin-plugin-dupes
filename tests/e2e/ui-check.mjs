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

// DF-C4: Playwright error messages carry page URLs (the browser may normalise the host, so every form
// of the address is listed), and a failed fill() logs the typed value as `- fill("…")`, so user names
// and passwords too. The call log quotes values JSON-style, so the escaped form is listed as well.
function addressForms(url) {
    try {
        const { origin, host, hostname } = new URL(url);
        return [url, origin, host, hostname];
    } catch {
        return [url];
    }
}

const secretForms = [
    ...addressForms(base).map((value) => [value, '<JF_URL>']),
    [process.env.JF_ADMIN_USER, '<admin>'],
    [process.env.JF_NONADMIN_USER, '<non-admin>'],
    [process.env.JF_ADMIN_PW, '<password>'],
    [process.env.JF_NONADMIN_PW, '<password>']
]
    .filter(([value]) => value)
    .flatMap(([value, placeholder]) => [[value, placeholder], [JSON.stringify(value).slice(1, -1), placeholder]])
    // Longest first, in one pass, so a short value never rewrites part of a longer one or a placeholder.
    .sort((a, b) => b[0].length - a[0].length);
const secretPattern = new RegExp(secretForms.map(([value]) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|'), 'gi');

function redact(text) {
    return String(text)
        .replace(/\x1b\[[0-9;]*m/g, '')
        .replace(secretPattern, (match) =>
            secretForms.find(([value]) => value.toLowerCase() === match.toLowerCase())?.[1] ?? '<redacted>')
        .replace(/\s*\n\s*/g, ' ')
        .trim();
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

async function run(browser) {
    const context = await browser.newContext({ acceptDownloads: true, viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();
    let scanRequests = 0;
    let libraryRequests = 0;
    page.on('request', (request) => {
        if (request.url().includes('/DupeFinder/Scan')) scanRequests++;
        if (request.url().includes('/DupeFinder/Libraries')) libraryRequests++;
    });

    await login(page, process.env.JF_ADMIN_USER, process.env.JF_ADMIN_PW ?? '');

    // AC-8: the dashboard sidebar link opens the page. Every later check needs the page, so stop without it.
    await page.goto(`${base}/web/#/dashboard`);
    const menuLink = page.getByText('Duplicate & Unmatched Finder', { exact: true }).first();
    const inSidebar = await menuLink.waitFor({ timeout: 30000 }).then(() => true, () => false);
    check(inSidebar, 'AC-8 the dashboard sidebar shows "Duplicate & Unmatched Finder"');
    if (!inSidebar) return;
    await menuLink.click();
    const libraryBoxes = page.locator(`${P} #dfLibraries input[type="checkbox"]`);
    const pageOpened = await page.waitForURL(/configurationpage\?name=dupefinder/, { timeout: 30000 })
        .then(() => libraryBoxes.first().waitFor({ state: 'attached', timeout: 30000 }))
        .then(() => true, () => false);
    const libraryCount = await libraryBoxes.count();
    check(pageOpened && libraryCount > 0, `AC-8 page opened from the sidebar with ${libraryCount} libraries`);
    if (!pageOpened) return;
    await page.screenshot({ path: `${outDir}/01-form.png`, fullPage: true });

    // AC-12: every check on, all libraries, scan.
    for (const box of await page.locator(`${P} #dfDuplicateChecks input, ${P} #dfUnmatchedChecks input`).all()) {
        if (!(await box.isChecked())) await box.check({ force: true });
    }
    await page.click(`${P} #dfScan`);
    // Scan re-enables when the request ends either way; a failed scan leaves the results hidden. The rest of
    // the run needs results, so stop without them.
    await waitForScanToFinish(page);
    const resultsShown = await page.locator(`${P} #dfResults`).waitFor({ state: 'visible', timeout: 5000 })
        .then(() => true, () => false);
    check(resultsShown, 'AC-12 the scan finished and the results are shown');
    if (!resultsShown) return;
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

    // AC-13: Identify opens Jellyfin's dialog for the clicked item; cancelling leaves the rows unchanged.
    // Prefer the AC's case (an unmatched movie), then any movie, then any row with Identify.
    const identifyRows = rows.filter({ has: page.locator('button.df-identify') });
    const movieRows = identifyRows.filter({ has: page.locator('td:nth-child(4)', { hasText: /^Movie$/ }) });
    const unmatchedMovieRows = movieRows.filter({ has: page.locator('td:nth-child(1)', { hasText: /^Unmatched movie\/series$/ }) });
    let identifyRow = identifyRows.first();
    for (const candidates of [unmatchedMovieRows, movieRows]) {
        if ((await candidates.count()) > 0) {
            identifyRow = candidates.first();
            break;
        }
    }
    if ((await identifyRows.count()) === 0) {
        check(false, 'AC-13 no Identify button in the results (needs a Movie, Series, album or artist row)');
    } else {
        const rowType = (await identifyRow.locator('td:nth-child(4)').textContent()).trim();
        const rowPath = (await identifyRow.locator('td.df-path').textContent()).trim();
        await identifyRow.locator('button.df-identify').click();
        const dialog = page.locator('.identifyDialog');
        const dialogOpened = await dialog.locator('#txtLookupName').waitFor({ state: 'visible', timeout: 30000 })
            .then(() => true, () => false);
        check(dialogOpened, `AC-13 Identify opens Jellyfin's Identify dialog (${rowType} row)`);
        await page.screenshot({ path: `${outDir}/03-identify.png` });
        if (dialogOpened) {
            // The dialog shows the item's path (itemidentifier.js); paths are not printed (they can name accounts).
            const dialogPath = ((await dialog.locator('.txtPath').textContent()) ?? '').trim();
            check(dialogPath === rowPath, rowPath
                ? 'AC-13 the Identify dialog is for the clicked item (its path matches the row)'
                : 'AC-13 the Identify dialog is for the clicked item (the row has no path, so only an empty dialog path could be compared)');
            // Escape only closes dialogs on the TV layout (keyboardNavigation.js), so use the dialog's own control.
            await dialog.locator('.btnCancel').click();
            const dialogClosed = await dialog.waitFor({ state: 'detached', timeout: 15000 }).then(() => true, () => false);
            check(dialogClosed, 'AC-13 Cancel closes the Identify dialog');
            // dialogHelper detaches the dialog first and fires `close`, which settles show(), after history.back().
            await page.waitForTimeout(1000);
            const identified = await page.locator(`${P} #dfRows .df-status`, { hasText: 'Identified' }).count();
            check(dialogClosed && identified === 0, 'AC-13 cancelling Identify leaves rows unchanged');
        }
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
        const detailsShown = await details.locator('#itemDetailPage:not(.hide)').first()
            .waitFor({ state: 'visible', timeout: 30000 }).then(() => true, () => false);
        const detailsUrl = details.url();
        check(detailsShown && detailsUrl.includes(`#/details?id=${itemId}`) && detailsUrl.includes('serverId='),
            'AC-13 Open opens the item details page');
        await details.close();
    }

    // Review Focus 4: leave and come back; the cached view is restored, nothing is rebuilt or duplicated,
    // and one click sends one request.
    await page.goto(`${base}/web/#/dashboard`);
    await page.waitForTimeout(2000);
    const libraryRequestsBefore = libraryRequests;
    await page.goto(pluginUrl);
    await page.locator(`${P} #dfScan`).waitFor({ timeout: 30000 });
    // A rebuilt view starts with the results hidden, so visible results prove the restore.
    check(await page.locator(`${P} #dfResults`).isVisible(), 'Review Focus 4: revisit restored the cached page (results still shown)');
    // A regression that reloads on viewshow would append libraries only after its request resolves.
    await page.waitForTimeout(2000);
    check(libraryRequests === libraryRequestsBefore,
        `Review Focus 4: revisit sent ${libraryRequests - libraryRequestsBefore} library request(s)`);
    check((await libraryBoxes.count()) === libraryCount, 'Review Focus 4: library list not duplicated after revisit');
    const duplicateChecks = await page.locator(`${P} #dfDuplicateChecks input`).count();
    const unmatchedChecks = await page.locator(`${P} #dfUnmatchedChecks input`).count();
    check(duplicateChecks === 5 && unmatchedChecks === 4,
        `Review Focus 4: check lists not duplicated (${duplicateChecks} duplicate + ${unmatchedChecks} unmatched, expected 5 + 4)`);
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
}

let browser;
let errored = false;
try {
    browser = await chromium.launch();
    await run(browser);
} catch (err) {
    // DF-C4: one redacted line on stdout instead of Node's uncaught-error dump (message and stack) on stderr.
    console.log(`ERROR ${redact(err?.message ?? err)}`);
    errored = true;
} finally {
    await browser?.close();
}

if (errored) process.exit(1);
console.log(`${failures.length} failure(s)`);
process.exit(failures.length ? 1 : 0);

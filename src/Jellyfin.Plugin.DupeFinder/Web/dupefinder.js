// Duplicate & Unmatched Finder page controller.
// ADR-0007: jellyfin-web imports this as an ES module via data-controller="__plugin/dupefinderjs" and
// calls `new default(view)`. Template literals are safe here; inside the page HTML they would be
// rewritten by Jellyfin's translation pass.

const CHECKS = [
    { id: 'DuplicateMovies', section: 'duplicates', label: 'Duplicate movies', title: 'Movies', on: true,
        description: 'Same TMDb or IMDb id, or same title and year when one copy is unmatched.' },
    { id: 'DuplicateSeries', section: 'duplicates', label: 'Duplicate series', title: 'TV series', on: true,
        description: 'Same TVDb, TMDb or IMDb id, or same title and year when one copy is unmatched.' },
    { id: 'DuplicateEpisodes', section: 'duplicates', label: 'Duplicate episodes', title: 'TV episodes', on: true,
        description: 'Same series, season and episode number.' },
    { id: 'DuplicateAlbums', section: 'duplicates', label: 'Duplicate albums', title: 'Music albums', on: true,
        description: 'Same MusicBrainz release, or same album artist and album name.' },
    { id: 'MergedVersions', section: 'duplicates', label: 'Merged versions', title: 'Merged versions', on: true,
        description: 'Movies or episodes Jellyfin has merged into one item with several versions.' },
    { id: 'UnmatchedMoviesSeries', section: 'unmatched', label: 'Unmatched movie/series', title: 'Movies and series', on: true,
        description: 'No metadata provider id at all.' },
    { id: 'UnmatchedEpisodes', section: 'unmatched', label: 'Unmatched episode', title: 'Episodes (often noisy)', on: false,
        description: 'No metadata provider id. Many correctly matched shows have no episode ids.' },
    { id: 'UnmatchedMusic', section: 'unmatched', label: 'Unmatched music', title: 'Music albums and album artists', on: true,
        description: 'No metadata provider id at all.' },
    { id: 'IncompleteMetadata', section: 'unmatched', label: 'Incomplete metadata', title: 'Incomplete metadata', on: true,
        description: 'Matched, but missing an overview or primary image.' }
];

const GROUPED = new Set(['DuplicateMovies', 'DuplicateSeries', 'DuplicateEpisodes', 'DuplicateAlbums', 'MergedVersions']);

// DF-R7.2: the scanned types jellyfin-web itself offers Identify for (itemHelper.canIdentify).
const IDENTIFIABLE = new Set(['Movie', 'Series', 'MusicAlbum', 'MusicArtist']);

const CSV_HEADER = ['Check', 'Group', 'Library', 'Type', 'Name', 'Year', 'Season', 'Episode', 'Series', 'Path',
    'SizeBytes', 'ProviderIds', 'Reason', 'ItemId'];

export function checkLabel(checkId) {
    const check = CHECKS.find((c) => c.id === checkId);
    return check ? check.label : checkId;
}

export function formatSize(bytes) {
    if (bytes === null || bytes === undefined) return '';
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let value = bytes;
    let unit = 0;
    while (value >= 1024 && unit < units.length - 1) {
        value /= 1024;
        unit++;
    }
    return `${unit === 0 ? value : value.toFixed(1)} ${units[unit]}`;
}

export function episodeCode(finding) {
    if (finding.ItemType !== 'Episode' || finding.Season == null || finding.Episode == null) return '';
    return `S${String(finding.Season).padStart(2, '0')}E${String(finding.Episode).padStart(2, '0')}`;
}

export function providerIdsText(providerIds) {
    return Object.entries(providerIds || {}).map(([key, value]) => `${key}=${value}`).join('; ');
}

// DF-R8.4/R8.5: RFC 4180 quoting; text cells that a spreadsheet would treat as a formula get a leading '.
export function csvField(value, isText) {
    if (value === null || value === undefined) return '';
    let text = String(value);
    if (isText && /^[=+\-@\t\r]/.test(text)) text = `'${text}`;
    if (/[",\r\n]/.test(text)) text = `"${text.replace(/"/g, '""')}"`;
    return text;
}

export function toCsv(findings) {
    const lines = [CSV_HEADER.join(',')];
    for (const f of findings) {
        lines.push([
            csvField(checkLabel(f.Check), true),
            csvField(f.Group, false),
            csvField(f.LibraryName, true),
            csvField(f.ItemType, true),
            csvField(f.Name, true),
            csvField(f.Year, false),
            csvField(f.Season, false),
            csvField(f.Episode, false),
            csvField(f.SeriesName, true),
            csvField(f.Path, true),
            csvField(f.SizeBytes, false),
            csvField(providerIdsText(f.ProviderIds), true),
            csvField(f.Reason, true),
            csvField(f.ItemId, true)
        ].join(','));
    }
    // The BOM makes Excel read the file as UTF-8 (Review Focus 5).
    return `﻿${lines.join('\r\n')}\r\n`;
}

export function csvFileName(date) {
    const pad = (n) => String(n).padStart(2, '0');
    return `dupefinder-${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}-${pad(date.getHours())}${pad(date.getMinutes())}.csv`;
}

// ApiClient rejects with the fetch Response on HTTP errors. Our 400s are problem details (ADR-0008);
// ASP.NET's own model-binding 400s carry only a title.
export async function errorMessage(err) {
    if (err && typeof err.text === 'function') {
        const text = await err.text();
        try {
            const body = JSON.parse(text);
            return body.detail || body.title || text;
        } catch {
            return text || `HTTP ${err.status}`;
        }
    }
    return String(err);
}

export default function (view) {
    const state = { loaded: false, scanning: false, result: null };
    const $ = (selector) => view.querySelector(selector);
    const boxes = (kind) => [...view.querySelectorAll(`input[data-kind="${kind}"]`)];
    const selected = (kind) => boxes(kind).filter((box) => box.checked).map((box) => box.value);

    function updateButtons() {
        const libraries = boxes('library');
        $('#dfSelectAll').checked = libraries.length > 0 && libraries.every((box) => box.checked);
        $('#dfScan').disabled = state.scanning || selected('library').length === 0 || selected('check').length === 0;
        $('#dfDownload').disabled = state.scanning || !state.result || state.result.Findings.length === 0;
    }

    function checkboxRow(value, title, description, checked, kind) {
        const wrapper = document.createElement('div');
        wrapper.className = 'checkboxContainer';
        // Static markup only; every piece of data goes in through value/textContent (Review Focus 3).
        wrapper.innerHTML = '<label class="emby-checkbox-label"><input is="emby-checkbox" type="checkbox"><span></span></label>';
        const input = wrapper.querySelector('input');
        input.value = value;
        input.checked = checked;
        input.dataset.kind = kind;
        input.addEventListener('change', updateButtons);
        wrapper.querySelector('span').textContent = title;
        if (description) {
            const help = document.createElement('div');
            help.className = 'fieldDescription checkboxFieldDescription';
            help.textContent = description;
            wrapper.appendChild(help);
        }
        return wrapper;
    }

    function renderChecks() {
        for (const [section, selector] of [['duplicates', '#dfDuplicateChecks'], ['unmatched', '#dfUnmatchedChecks']]) {
            const fragment = document.createDocumentFragment();
            for (const check of CHECKS.filter((c) => c.section === section)) {
                fragment.appendChild(checkboxRow(check.id, check.title, check.description, check.on, 'check'));
            }
            $(selector).replaceChildren(fragment);
        }
    }

    async function loadLibraries() {
        Dashboard.showLoadingMsg();
        try {
            const libraries = await ApiClient.getJSON(ApiClient.getUrl('DupeFinder/Libraries'));
            const fragment = document.createDocumentFragment();
            for (const library of libraries) {
                const title = library.CollectionType ? `${library.Name} (${library.CollectionType})` : library.Name;
                fragment.appendChild(checkboxRow(library.Id, title, '', true, 'library'));
            }
            $('#dfLibraries').replaceChildren(fragment);
        } catch (err) {
            Dashboard.alert({ title: 'Could not load libraries', message: await errorMessage(err) });
        } finally {
            Dashboard.hideLoadingMsg();
            updateButtons();
        }
    }

    function cell(text, className) {
        const td = document.createElement('td');
        td.className = className ? `detailTableBodyCell ${className}` : 'detailTableBodyCell';
        td.textContent = text === null || text === undefined ? '' : String(text);
        return td;
    }

    function link(text, href, className) {
        const anchor = document.createElement('a');
        anchor.href = href;
        anchor.target = '_blank';
        anchor.rel = 'noopener';
        anchor.className = `emby-button raised ${className}`;
        anchor.textContent = text;
        return anchor;
    }

    function actionsCell(finding) {
        const td = cell('', 'df-actions');
        td.appendChild(link('Open', `#/details?id=${finding.ItemId}&serverId=${ApiClient.serverId()}`, 'df-open'));
        td.appendChild(link('Edit metadata', `#/metadata?id=${finding.ItemId}`, 'df-edit'));
        // ADR-0003: undocumented global; hide the button if a future jellyfin-web removes it.
        if (IDENTIFIABLE.has(finding.ItemType) && typeof Dashboard.itemIdentifier?.show === 'function') {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'emby-button raised df-identify';
            button.textContent = 'Identify';
            const status = document.createElement('span');
            status.className = 'df-status';
            button.addEventListener('click', () => {
                Dashboard.itemIdentifier.show(finding.ItemId, ApiClient.serverId())
                    .then(() => {
                        status.textContent = 'Identified — rescan to refresh';
                        button.disabled = true;
                    })
                    .catch(() => {
                        // Cancelled or nothing applied: DF-R7.2 leaves the row unchanged.
                    });
            });
            td.append(button, status);
        }
        return td;
    }

    function buildRows(findings) {
        const fragment = document.createDocumentFragment();
        let band = false;
        let lastGroup = null;
        for (const finding of findings) {
            const group = finding.Group ?? null;
            if (group !== null && group !== lastGroup) band = !band;
            lastGroup = group;
            const row = document.createElement('tr');
            row.dataset.itemId = finding.ItemId;
            if (group !== null && band) row.classList.add('df-band');
            row.append(
                cell(checkLabel(finding.Check)),
                cell(group ?? ''),
                cell(finding.LibraryName),
                cell(finding.ItemType),
                cell(finding.Name),
                cell(finding.Year ?? ''),
                cell(episodeCode(finding)),
                cell(finding.Path ?? '', 'df-path'),
                cell(formatSize(finding.SizeBytes)),
                cell(providerIdsText(finding.ProviderIds)),
                cell(finding.Reason),
                actionsCell(finding)
            );
            fragment.appendChild(row);
        }
        return fragment;
    }

    function renderResults(result) {
        $('#dfResults').hidden = false;
        $('#dfStats').textContent = `Scanned ${result.ScannedItemCount} items in ${(result.DurationMs / 1000).toFixed(1)} s`;
        const summary = document.createDocumentFragment();
        for (const check of CHECKS.filter((c) => c.id in result.Summary)) {
            const count = result.Summary[check.id];
            const noun = GROUPED.has(check.id) ? (count === 1 ? 'group' : 'groups') : (count === 1 ? 'item' : 'items');
            const li = document.createElement('li');
            li.textContent = `${check.label}: ${count} ${noun}`;
            summary.appendChild(li);
        }
        $('#dfSummary').replaceChildren(summary);
        const hasFindings = result.Findings.length > 0;
        $('#dfEmpty').hidden = hasFindings;
        $('#dfTableWrap').hidden = !hasFindings;
        $('#dfRows').replaceChildren(buildRows(result.Findings));
    }

    async function scan() {
        state.scanning = true;
        updateButtons();
        Dashboard.showLoadingMsg();
        try {
            state.result = await ApiClient.ajax({
                type: 'POST',
                url: ApiClient.getUrl('DupeFinder/Scan'),
                data: JSON.stringify({ LibraryIds: selected('library'), Checks: selected('check') }),
                contentType: 'application/json',
                dataType: 'json'
            });
            renderResults(state.result);
        } catch (err) {
            Dashboard.alert({ title: 'Scan failed', message: await errorMessage(err) });
        } finally {
            state.scanning = false;
            Dashboard.hideLoadingMsg();
            updateButtons();
        }
    }

    function downloadCsv() {
        const blob = new Blob([toCsv(state.result.Findings)], { type: 'text/csv;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = csvFileName(new Date());
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }

    $('#dfSelectAll').addEventListener('change', (event) => {
        for (const box of boxes('library')) box.checked = event.target.checked;
        updateButtons();
    });
    $('#dfScan').addEventListener('click', scan);
    $('#dfDownload').addEventListener('click', downloadCsv);

    view.addEventListener('viewshow', () => {
        // viewshow fires on every visit, including restores from the view cache: build once (Review Focus 4).
        if (state.loaded) return;
        state.loaded = true;
        renderChecks();
        loadLibraries();
    });
}

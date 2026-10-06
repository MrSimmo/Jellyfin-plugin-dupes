import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    checkLabel, csvField, csvFileName, episodeCode, errorMessage, formatSize, providerIdsText, toCsv
} from '../../src/Jellyfin.Plugin.DupeFinder/Web/dupefinder.js';

const HEADER = 'Check,Group,Library,Type,Name,Year,Season,Episode,Series,Path,SizeBytes,ProviderIds,Reason,ItemId';

function finding(overrides = {}) {
    return {
        Check: 'DuplicateMovies', Group: 1, ItemId: 'abc123', LibraryName: 'Films', ItemType: 'Movie',
        Name: 'Heat', Year: 1995, Path: '/media/Heat.mkv', SizeBytes: 1500000000,
        ProviderIds: { Tmdb: '949' }, Reason: 'Same TMDb id 949', ...overrides
    };
}

test('CSV has a UTF-8 BOM, the DF-R8.3 header, CRLF line endings and one line per finding', () => {
    const csv = toCsv([finding(), finding({ Group: 2 })]);
    assert.ok(csv.startsWith('﻿' + HEADER + '\r\n'));
    assert.ok(csv.endsWith('\r\n'));
    assert.equal(csv.split('\r\n').length, 4);
    assert.ok(csv.includes('\r\nDuplicate movies,1,Films,Movie,Heat,1995,,,,/media/Heat.mkv,1500000000,Tmdb=949,Same TMDb id 949,abc123\r\n'));
});

test('absent optional fields become empty cells', () => {
    const csv = toCsv([finding({ Group: undefined, Year: undefined, Path: undefined, SizeBytes: undefined, Check: 'UnmatchedMoviesSeries', Reason: 'No provider ids', ProviderIds: {} })]);
    assert.ok(csv.includes('\r\nUnmatched movie/series,,Films,Movie,Heat,,,,,,,,No provider ids,abc123\r\n'));
});

test('fields with a comma or quote are quoted and inner quotes doubled (AC-14)', () => {
    assert.equal(csvField('Say "Hi", Bob', true), '"Say ""Hi"", Bob"');
    assert.equal(csvField('two\nlines', true), '"two\nlines"');
});

test('text starting with a formula character gets a leading apostrophe (AC-14)', () => {
    assert.equal(csvField('=SUM(A1)', true), "'=SUM(A1)");
    assert.equal(csvField('+1', true), "'+1");
    assert.equal(csvField('-ish', true), "'-ish");
    assert.equal(csvField('@home', true), "'@home");
    assert.equal(csvField('\tx', true), "'\tx");
});

test('numeric columns are never prefixed', () => {
    assert.equal(csvField(-5, false), '-5');
    assert.equal(csvField(null, false), '');
    assert.equal(csvField(undefined, true), '');
});

test('non-ASCII names survive unchanged (Review Focus 5)', () => {
    assert.ok(toCsv([finding({ Name: 'Amélie 千と千尋' })]).includes(',Amélie 千と千尋,'));
});

test('file name is dupefinder-YYYYMMDD-HHmm.csv in local time', () => {
    assert.equal(csvFileName(new Date(2026, 9, 6, 9, 5)), 'dupefinder-20261006-0905.csv');
});

test('sizes are human readable', () => {
    assert.equal(formatSize(1503238553), '1.4 GB');
    assert.equal(formatSize(512), '512 B');
    assert.equal(formatSize(undefined), '');
});

test('episode codes only for episodes with both numbers', () => {
    assert.equal(episodeCode({ ItemType: 'Episode', Season: 1, Episode: 3 }), 'S01E03');
    assert.equal(episodeCode({ ItemType: 'Episode', Season: 1 }), '');
    assert.equal(episodeCode({ ItemType: 'Movie', Season: 1, Episode: 3 }), '');
});

test('provider ids render as Key=Value pairs', () => {
    assert.equal(providerIdsText({ Tmdb: '603', Imdb: 'tt0133093' }), 'Tmdb=603; Imdb=tt0133093');
    assert.equal(providerIdsText(undefined), '');
});

test('check labels match the table', () => {
    assert.equal(checkLabel('MergedVersions'), 'Merged versions');
    assert.equal(checkLabel('Unknown'), 'Unknown');
});

test('error messages come from problem details, then text', async () => {
    const response = (body, status = 400) => ({ status, text: async () => body });
    assert.equal(await errorMessage(response('{"title":"Bad Request","detail":"Select at least one check."}')), 'Select at least one check.');
    assert.equal(await errorMessage(response('{"title":"One or more validation errors occurred."}')), 'One or more validation errors occurred.');
    assert.equal(await errorMessage(response('plain failure')), 'plain failure');
    assert.equal(await errorMessage(response('', 500)), 'HTTP 500');
    assert.equal(await errorMessage('offline'), 'offline');
});

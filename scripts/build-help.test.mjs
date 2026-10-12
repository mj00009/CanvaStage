import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, cp, writeFile, readFile, readdir, symlink, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';
import { buildHelp, validateLinks } from './build-help.mjs';

async function fixture(t) {
  const directory = await mkdtemp(join(tmpdir(), 'canvastage-help-test-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  await cp(resolve('docs/help'), join(directory, 'source'), { recursive: true });
  // Keep these regression fixtures fixed as later published guides are added.
  const manifestPath = join(directory, 'source/manifest.json');
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
  manifest.latest = 'v0.12.0';
  manifest.versions = manifest.versions.filter(entry => entry.version === 'v0.12.0');
  await writeFile(manifestPath, JSON.stringify(manifest));
  return { source: join(directory, 'source'), output: join(directory, 'site') };
}

test('publishes only reviewed help, with working root and versioned guides', async t => {
  const { source, output } = await fixture(t);
  await writeFile(join(source, 'private.log'), 'Not public');
  await writeFile(join(source, 'v0.12.0/images/unreviewed.png'), 'Not public');
  const result = await buildHelp(source, output, 'v0.12.0');
  assert.equal(result.latest, 'v0.12.0');
  assert.equal(result.files, 22);
  assert.ok(!(await readdir(output)).includes('private.log'));
  assert.ok(!(await readdir(join(output, 'images'))).includes('unreviewed.png'));
  assert.match(await readFile(join(output, 'v0.12.0/quick-start.html'), 'utf8'), /Following Canva/);
});
test('each published quick start covers Full and Compact views with reviewed screenshots', async () => {
  const source = resolve('docs/help');
  const manifest = JSON.parse(await readFile(join(source, 'manifest.json'), 'utf8'));
  for (const entry of manifest.versions) {
    const guide = await readFile(join(source, entry.version, 'quick-start.html'), 'utf8');
    assert.match(guide, /<h3 id="full-view">Full view<\/h3>/);
    assert.match(guide, /<h3 id="compact-view">Compact view<\/h3>/);
    for (const image of ['full-view.png', 'compact.png']) {
      assert.ok(entry.images[image], `${entry.version} reviews ${image}`);
      assert.ok(guide.includes(`src="images/${image}"`), `${entry.version} shows ${image}`);
    }
  }
});
test('rejects release/content version mismatch', async t => {
  const { source, output } = await fixture(t);
  await assert.rejects(buildHelp(source, output, 'v0.13.0'), /does not match/);
});
test('retains older version routes when a new guide becomes latest', async t => {
  const { source, output } = await fixture(t);
  await cp(join(source, 'v0.12.0'), join(source, 'v0.13.0'), { recursive: true });
  const manifest = JSON.parse(await readFile(join(source, 'manifest.json'), 'utf8'));
  manifest.versions.push({ ...manifest.versions[0], version: 'v0.13.0' });
  manifest.latest = 'v0.13.0';
  await writeFile(join(source, 'manifest.json'), JSON.stringify(manifest));
  await buildHelp(source, output, 'v0.13.0');
  assert.match(await readFile(join(output, 'index.html'), 'utf8'), /Guide for v0.13.0/);
  assert.match(await readFile(join(output, 'v0.12.0/index.html'), 'utf8'), /Guide for v0.12.0/);
});
test('rejects changed or unreviewed screenshot bytes', async t => {
  const { source, output } = await fixture(t);
  await writeFile(join(source, 'v0.12.0/images/compact.png'), 'secret bytes');
  await assert.rejects(buildHelp(source, output), /changed without review/);
});
test('rejects symlinked publication files', async t => {
  const { source, output } = await fixture(t);
  await rm(join(source, 'v0.12.0/quick-start.html'));
  await symlink(join(source, 'v0.12.0/advanced.html'), join(source, 'v0.12.0/quick-start.html'));
  await assert.rejects(buildHelp(source, output), /ordinary, reviewed/);
});
test('rejects active HTML content', async t => {
  const { source, output } = await fixture(t);
  await writeFile(join(source, 'v0.12.0/index.html'), '<h1>Test</h1><script>alert(1)</script>');
  await assert.rejects(buildHelp(source, output), /Active content/);
});
test('refuses nonempty output rather than uploading stale private files', async t => {
  const { source, output } = await fixture(t);
  await buildHelp(source, output);
  await assert.rejects(buildHelp(source, output), /new or empty/);
});
test('detects broken links, missing anchors, and unsafe link schemes', () => {
  assert.throws(() => validateLinks(new Map([['index.html', Buffer.from('<a href="missing.html">')] ])), /Broken link/);
  assert.throws(() => validateLinks(new Map([['index.html', Buffer.from('<a href="#missing">')] ])), /Missing anchor/);
  assert.throws(() => validateLinks(new Map([['index.html', Buffer.from('<a href="javascript:alert(1)">')] ])), /Unsafe link/);
});

import { createHash } from 'node:crypto';
import { readFile, readdir, lstat, mkdir, writeFile } from 'node:fs/promises';
import { resolve, join, dirname, posix } from 'node:path';
import { fileURLToPath } from 'node:url';

export const pages = {
  'index.html': 'Welcome',
  'quick-start.html': 'Sunday quick start',
  'advanced.html': 'First-time & advanced setup',
  'troubleshooting.html': 'Troubleshooting'
};
const stableVersion = /^v(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)$/;
const escape = value => String(value).replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character]);

async function regularFile(path) {
  const stat = await lstat(path);
  if (!stat.isFile() || stat.isSymbolicLink()) throw new Error(`Only ordinary, reviewed files can be published: ${path}`);
  return readFile(path);
}

function render(content, page, version, versions, root) {
  const prefix = root ? '' : '../';
  const navigation = Object.entries(pages).map(([name, title]) => `<li><a href="${name}"${name === page ? ' aria-current="page"' : ''}>${escape(title)}</a></li>`).join('');
  const archives = versions.map(entry => `<li><a href="${prefix}${entry.version}/">${entry.version}</a></li>`).join('');
  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'self'; img-src 'self'; base-uri 'none'; form-action 'none'">
<meta name="description" content="CanvaStage ${version.version} operator help: Sunday quick start, Canva setup, displays, and troubleshooting.">
<title>${escape(pages[page])} · CanvaStage Help ${version.version}</title><link rel="stylesheet" href="${prefix}styles.css"></head>
<body><a class="skip" href="#content">Skip to guide</a><header><a class="brand" href="index.html">CanvaStage Help</a><span class="version">Guide for ${version.version} · Updated ${version.updated}</span></header>
<div class="layout"><nav aria-label="Help guides"><ul>${navigation}</ul><p class="nav-label">Versions</p><ul><li><a href="${prefix}index.html">Latest published guide</a></li>${archives}</ul></nav>
<main id="content" tabindex="-1">${content}</main></div>
<footer><p>Independent software. Not affiliated with, endorsed by, or sponsored by Canva Pty Ltd. Provided without warranties under <a href="https://github.com/mj00009/CanvaStage/blob/main/LICENSE">Apache-2.0</a>; see <a href="https://github.com/mj00009/CanvaStage/blob/main/TRADEMARKS.md">trademark notices</a>.</p>
<p>Screenshots use synthetic demo content from the <a href="https://github.com/mj00009/CanvaStage/actions/runs/${version.screenshotRun}">${version.version} Windows verification run</a>, not a real account or presentation. CI has one physical display; simulated Extended-display choices do not prove hardware behavior.</p>
<p>No analytics, embedded third-party media, or credentials are sent by this site. GitHub provides the hosting. <a href="https://github.com/mj00009/CanvaStage/issues">Report a documentation problem</a> without sharing private content.</p></footer></body></html>\n`;
}

export function validateLinks(files) {
  for (const [path, buffer] of files) {
    if (!path.endsWith('.html')) continue;
    const content = buffer.toString();
    for (const match of content.matchAll(/\b(?:href|src)="([^"]+)"/g)) {
      const link = match[1].replaceAll('&amp;', '&');
      if (link.startsWith('https://')) continue;
      if (/^[a-z][a-z\d+.-]*:/i.test(link) || link.startsWith('//') || link.startsWith('/')) throw new Error(`Unsafe link in ${path}: ${link}`);
      const [target, fragment] = link.split('#');
      const destination = target ? posix.normalize(posix.join(posix.dirname(path), decodeURIComponent(target))) : path;
      const normalized = destination.endsWith('/') || !posix.extname(destination) ? posix.join(destination, 'index.html') : destination;
      if (!files.has(normalized)) throw new Error(`Broken link in ${path}: ${link}`);
      if (fragment && !files.get(normalized).toString().includes(`id="${decodeURIComponent(fragment)}"`)) throw new Error(`Missing anchor in ${path}: ${link}`);
    }
  }
}

export async function buildHelp(source, output, releaseTag) {
  const manifest = JSON.parse(await regularFile(join(source, 'manifest.json')));
  if (!Array.isArray(manifest.versions) || manifest.versions.length === 0) throw new Error('No versioned help content');
  if (releaseTag && manifest.latest !== releaseTag) throw new Error(`Help version ${manifest.latest} does not match release ${releaseTag}`);
  const seen = new Set();
  const files = new Map([['styles.css', await regularFile(join(source, 'styles.css'))]]);
  for (const version of manifest.versions) {
    if (!stableVersion.test(version.version) || seen.has(version.version)) throw new Error('Invalid or duplicate guide version');
    seen.add(version.version);
    if (!/^\d{4}-\d{2}-\d{2}$/.test(version.updated) || !/^\d+$/.test(version.screenshotRun)
      || !/^[a-f0-9]{40}$/.test(version.screenshotCommit) || version.synthetic !== true) throw new Error('Missing reviewed synthetic screenshot provenance');
    if (!version.images || Object.keys(version.images).length === 0) throw new Error('No reviewed screenshots');
    const sourceDirectory = join(source, version.version);
    if ((await lstat(sourceDirectory)).isSymbolicLink() || (await lstat(join(sourceDirectory, 'images'))).isSymbolicLink()) throw new Error('Source directories cannot be symlinks');
    for (const [name, checksum] of Object.entries(version.images)) {
      if (!/^[a-z0-9-]+\.png$/.test(name) || !/^[a-f0-9]{64}$/.test(checksum)) throw new Error('Invalid screenshot allowlist');
      const image = await regularFile(join(sourceDirectory, 'images', name));
      if (!image.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))
        || createHash('sha256').update(image).digest('hex') !== checksum) throw new Error(`Screenshot changed without review: ${name}`);
      files.set(`${version.version}/images/${name}`, image);
      if (version.version === manifest.latest) files.set(`images/${name}`, image);
    }
    for (const page of Object.keys(pages)) {
      const content = (await regularFile(join(sourceDirectory, page))).toString();
      if (/<\s*(?:script|iframe|object|embed|form|base)\b|\bon\w+\s*=|\bstyle\s*=/i.test(content)) throw new Error(`Active content is not allowed in ${page}`);
      if ((content.match(/<h1>/g) ?? []).length !== 1) throw new Error(`Expected one main heading in ${page}`);
      files.set(`${version.version}/${page}`, Buffer.from(render(content, page, version, manifest.versions, false)));
      if (version.version === manifest.latest) files.set(page, Buffer.from(render(content, page, version, manifest.versions, true)));
    }
  }
  if (!seen.has(manifest.latest)) throw new Error('Latest guide does not exist');
  files.set('404.html', Buffer.from('<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Guide not found · CanvaStage</title></head><body><h1>Guide not found</h1><p>This app version may not have an archived guide yet.</p><p><a href="https://mj00009.github.io/CanvaStage/">Open the latest CanvaStage help</a></p></body></html>'));
  validateLinks(files);
  // An empty/fresh destination prevents unrelated files from leaking into the Pages artifact.
  try {
    if ((await lstat(output)).isSymbolicLink() || (await readdir(output)).length) throw new Error('Help output must be a new or empty directory');
  } catch (error) { if (error.code !== 'ENOENT') throw error; }
  await mkdir(output, { recursive: true });
  for (const [path, buffer] of files) {
    await mkdir(dirname(join(output, path)), { recursive: true });
    await writeFile(join(output, path), buffer);
  }
  return { latest: manifest.latest, files: files.size, versions: [...seen] };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const argumentsList = process.argv.slice(2);
  const option = key => argumentsList.includes(key) ? argumentsList[argumentsList.indexOf(key) + 1] : undefined;
  const repository = resolve(dirname(fileURLToPath(import.meta.url)), '..');
  const result = await buildHelp(join(repository, 'docs/help'), resolve(option('--output') ?? join(repository, '_site')), option('--release-tag'));
  console.log(`Validated ${result.files} public help files for ${result.versions.join(', ')}; latest ${result.latest}.`);
}

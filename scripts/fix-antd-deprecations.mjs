/**
 * Safe Ant Design 6 deprecation migrator for frontend-admin.
 *
 * Unlike a blind global replace, this only rewrites props inside matching
 * opening tags (Space / Modal / Drawer / …) so Modal width, Form message,
 * ConfirmDialog message, etc. stay intact.
 *
 * Usage:
 *   node scripts/fix-antd-deprecations.mjs           # apply
 *   node scripts/fix-antd-deprecations.mjs --dry-run  # report only
 */
import fs from 'node:fs';
import path from 'node:path';

const DRY_RUN = process.argv.includes('--dry-run');
const ROOT = path.join('frontend-admin', 'src');

function walk(dir, out = []) {
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    if (ent.name === 'node_modules' || ent.name === '.next') continue;
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) walk(p, out);
    else if (/\.(tsx|jsx)$/.test(ent.name)) out.push(p);
  }
  return out;
}

/** Find end index (exclusive) of a JSX opening tag starting at `start`. */
function findTagEnd(src, start) {
  let j = start;
  let inS = null;
  let brace = 0;
  while (j < src.length) {
    const c = src[j];
    if (inS) {
      if (c === '\\') {
        j += 2;
        continue;
      }
      if (c === inS) inS = null;
      j++;
      continue;
    }
    if (c === '"' || c === "'" || c === '`') {
      inS = c;
      j++;
      continue;
    }
    if (c === '{') {
      brace++;
      j++;
      continue;
    }
    if (c === '}') {
      brace = Math.max(0, brace - 1);
      j++;
      continue;
    }
    if (brace === 0 && c === '>') {
      return j + 1;
    }
    j++;
  }
  return -1;
}

/**
 * Rewrite props inside tags whose name matches `tagNameRe` (e.g. /^(Space|Space\.Compact)$/).
 * `rewriter(tag) => { tag, count }`
 */
function rewriteTags(src, tagNameRe, rewriter) {
  let i = 0;
  let out = '';
  let total = 0;

  while (i < src.length) {
    const idx = src.indexOf('<', i);
    if (idx === -1) {
      out += src.slice(i);
      break;
    }

    // Skip closing tags and comments
    if (src[idx + 1] === '/' || src.startsWith('<!--', idx)) {
      out += src.slice(i, idx + 1);
      i = idx + 1;
      continue;
    }

    const nameMatch = src.slice(idx + 1).match(/^([A-Za-z][\w.]*)/);
    if (!nameMatch || !tagNameRe.test(nameMatch[1])) {
      out += src.slice(i, idx + 1);
      i = idx + 1;
      continue;
    }

    const end = findTagEnd(src, idx + 1 + nameMatch[1].length);
    if (end === -1) {
      out += src.slice(i);
      break;
    }

    out += src.slice(i, idx);
    const tag = src.slice(idx, end);
    const { tag: next, count } = rewriter(tag);
    out += next;
    total += count;
    i = end;
  }

  return { src: out, count: total };
}

function fixSpaceDirection(tag) {
  let count = 0;
  const next = tag.replace(/\bdirection=/g, () => {
    count++;
    return 'orientation=';
  });
  return { tag: next, count };
}

/**
 * maskClosable={expr} / maskClosable → mask={{ closable: expr }}
 * Skips tags that already have both `mask=` and `maskClosable` (manual review).
 */
function fixMaskClosable(tag) {
  if (/\bmask=/.test(tag) && /\bmaskClosable/.test(tag)) {
    return { tag, count: 0, skippedBoth: true };
  }
  if (!/\bmaskClosable/.test(tag)) {
    return { tag, count: 0 };
  }

  let count = 0;
  let next = '';
  let i = 0;

  while (i < tag.length) {
    const rest = tag.slice(i);
    const m = rest.match(/^([\s/]*)maskClosable(?==|\s|\/|>)/);
    if (!m) {
      next += tag[i];
      i++;
      continue;
    }

    const lead = m[1] ?? '';
    i += m[0].length;

    if (tag[i] === '=') {
      i++; // skip =
      if (tag[i] !== '{') {
        // unexpected form — abort this match by writing original fragment
        next += m[0] + '=';
        continue;
      }
      i++; // skip {
      let depth = 1;
      const exprStart = i;
      while (i < tag.length && depth > 0) {
        if (tag[i] === '{') depth++;
        else if (tag[i] === '}') depth--;
        i++;
      }
      const expr = tag.slice(exprStart, i - 1).trim();
      next += `${lead}mask={{ closable: ${expr} }}`;
      count++;
    } else {
      // bare maskClosable
      next += `${lead}mask={{ closable: true }}`;
      count++;
    }
  }

  return { tag: next, count };
}

function scanDeprecated(src) {
  const hits = {
    drawerWidth: [...src.matchAll(/<Drawer[\s\S]{0,500}?\bwidth=/g)].length,
    alertMessage: [...src.matchAll(/<Alert[\s\S]{0,800}?\bmessage=/g)].length,
    destroyOnClose: (src.match(/\bdestroyOnClose\b/g) || []).length,
    dropdownRender: (src.match(/\bdropdownRender\b/g) || []).length,
    cardBorderedFalse: [...src.matchAll(/<Card[\s\S]{0,300}?\bbordered=\{false\}/g)].length,
    tagBorderedFalse: [...src.matchAll(/<Tag[\s\S]{0,300}?\bbordered=\{false\}/g)].length,
    spaceDirection: [...src.matchAll(/<(?:Space|Space\.Compact)[\s\S]{0,200}?\bdirection=/g)].length,
    maskClosable: [...src.matchAll(/<(?:Modal|Drawer)[\s\S]{0,600}?\bmaskClosable/g)].length,
  };
  return hits;
}

const files = walk(ROOT);
const report = {
  dryRun: DRY_RUN,
  listedPropsAlreadyClean: true,
  fixed: [],
  skippedMaskBoth: [],
  remainingAfter: null,
};

let spaceFixed = 0;
let maskFixed = 0;

for (const file of files) {
  const original = fs.readFileSync(file, 'utf8');
  let src = original;
  let fileFixes = [];

  {
    const r = rewriteTags(src, /^(Space|Space\.Compact)$/, fixSpaceDirection);
    if (r.count > 0) {
      src = r.src;
      spaceFixed += r.count;
      fileFixes.push(`Space.direction→orientation×${r.count}`);
    }
  }

  {
    const skipped = [];
    const r = rewriteTags(src, /^(Modal|Drawer)$/, (tag) => {
      const res = fixMaskClosable(tag);
      if (res.skippedBoth) skipped.push(true);
      return res;
    });
    if (skipped.length) {
      report.skippedMaskBoth.push(path.relative(process.cwd(), file).replace(/\\/g, '/'));
    }
    if (r.count > 0) {
      src = r.src;
      maskFixed += r.count;
      fileFixes.push(`maskClosable→mask.closable×${r.count}`);
    }
  }

  if (src !== original) {
    const rel = path.relative(process.cwd(), file).replace(/\\/g, '/');
    report.fixed.push({ file: rel, fixes: fileFixes });
    if (!DRY_RUN) fs.writeFileSync(file, src);
  }
}

// Final scan
const remaining = {
  drawerWidth: 0,
  alertMessage: 0,
  destroyOnClose: 0,
  dropdownRender: 0,
  cardBorderedFalse: 0,
  tagBorderedFalse: 0,
  spaceDirection: 0,
  maskClosable: 0,
  collapseBorderedFalse: 0,
};
for (const file of files) {
  const content = DRY_RUN && report.fixed.some((f) => f.file.replace(/\\/g, '/') === path.relative(process.cwd(), file).replace(/\\/g, '/'))
    ? null
    : fs.readFileSync(file, 'utf8');
  // After apply we re-read; for dry-run approximate by scanning original remaining
  const src = content ?? fs.readFileSync(file, 'utf8');
  const h = scanDeprecated(src);
  for (const k of Object.keys(remaining)) {
    if (k in h) remaining[k] += h[k];
  }
  remaining.collapseBorderedFalse += [...src.matchAll(/<Collapse[\s\S]{0,200}?\bbordered=\{false\}/g)].length;
}

report.spaceFixed = spaceFixed;
report.maskFixed = maskFixed;
report.remainingAfter = remaining;
report.listedPropsAlreadyClean =
  remaining.drawerWidth === 0 &&
  remaining.alertMessage === 0 &&
  remaining.destroyOnClose === 0 &&
  remaining.dropdownRender === 0 &&
  remaining.cardBorderedFalse === 0 &&
  remaining.tagBorderedFalse === 0;

console.log(JSON.stringify(report, null, 2));

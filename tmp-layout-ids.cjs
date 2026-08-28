const { execSync } = require('child_process');

function extractLayoutIds(src) {
  const ids = [];
  const re = /catalogIds:\s*\[([\s\S]*?)\]/g;
  let m;
  while ((m = re.exec(src))) {
    for (const id of m[1].matchAll(/'([a-zA-Z0-9]+)'/g)) ids.push(id[1]);
  }
  return [...new Set(ids)].sort();
}

const old = execSync('git show HEAD:frontend-admin/src/shared/adminSidebarRegistry.ts', {
  encoding: 'utf8',
});
process.stdout.write(JSON.stringify(extractLayoutIds(old), null, 2));

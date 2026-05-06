import { mkdir, copyFile, cp } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// Node 18 doesn't support `import.meta.dirname`, so derive it.
const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..');
const nm = path.join(root, 'node_modules');
const out = path.join(root, 'wwwroot', 'vendor');

async function ensureDir(dir) {
  await mkdir(dir, { recursive: true });
}

async function copy(src, dest) {
  await ensureDir(path.dirname(dest));
  await copyFile(src, dest);
}

async function copyDir(src, dest) {
  await ensureDir(dest);
  await cp(src, dest, { recursive: true });
}

async function main() {
  // DataTables (jQuery plugin) + Bootstrap 5 integration.
  await copy(
    path.join(nm, 'datatables.net', 'js', 'dataTables.min.js'),
    path.join(out, 'datatables', 'jquery.dataTables.min.js'),
  );
  await copy(
    path.join(nm, 'datatables.net-bs5', 'js', 'dataTables.bootstrap5.min.js'),
    path.join(out, 'datatables', 'dataTables.bootstrap5.min.js'),
  );
  await copy(
    path.join(nm, 'datatables.net-bs5', 'css', 'dataTables.bootstrap5.min.css'),
    path.join(out, 'datatables', 'dataTables.bootstrap5.min.css'),
  );

  // Chart.js UMD build (keeps global `Chart` usage unchanged).
  await copy(
    path.join(nm, 'chart.js', 'dist', 'chart.umd.min.js'),
    path.join(out, 'chartjs', 'chart.umd.min.js'),
  );

  // Bootstrap Icons (CSS + fonts).
  await copy(
    path.join(nm, 'bootstrap-icons', 'font', 'bootstrap-icons.min.css'),
    path.join(out, 'bootstrap-icons', 'bootstrap-icons.min.css'),
  );
  await copyDir(
    path.join(nm, 'bootstrap-icons', 'font', 'fonts'),
    path.join(out, 'bootstrap-icons', 'fonts'),
  );
}

await main();

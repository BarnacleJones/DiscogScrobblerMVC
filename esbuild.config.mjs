import * as esbuild from 'esbuild';
import { readdirSync } from 'node:fs';
import path from 'node:path';

/** Every `Scripts/*.ts` file is a browser bundle entry; shared modules stay under `Scripts/shared/`. */
const scriptsDir = 'Scripts';
const entryPoints = readdirSync(scriptsDir)
    .filter((file) => file.endsWith('.ts'))
    .map((file) => path.join(scriptsDir, file));

const options = {
    entryPoints,
    bundle: true,
    outdir: 'wwwroot/js',
    platform: 'browser',
};

const watch = process.argv.includes('--watch');

if (watch) {
    const ctx = await esbuild.context(options);
    await ctx.watch();
} else {
    await esbuild.build(options);
}

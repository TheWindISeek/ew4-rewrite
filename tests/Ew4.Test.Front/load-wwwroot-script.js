import {readFileSync} from 'node:fs';
import {dirname, join} from 'node:path';
import {fileURLToPath} from 'node:url';
import vm from 'node:vm';

const wwwroot = join(dirname(fileURLToPath(import.meta.url)), '../../src/Ew4.Front.Web/wwwroot');

/** Load a classic wwwroot IIFE script into a fresh global sandbox. */
export function loadWwwrootScript(relativePath) {
  const file = join(wwwroot, relativePath);
  const sandbox = {globalThis: {}};
  sandbox.globalThis = sandbox;
  vm.runInContext(readFileSync(file, 'utf8'), vm.createContext(sandbox), {filename: file});
  return sandbox;
}

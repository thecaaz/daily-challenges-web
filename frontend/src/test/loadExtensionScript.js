import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const EXTENSION_DIR = resolve(dirname(fileURLToPath(import.meta.url)), '../../../extension')

/**
 * Runs one of the extension's classic scripts the way Firefox does: as plain
 * script code against the current globals (window, document, browser), each
 * call a fresh evaluation.
 */
export default function loadExtensionScript(fileName) {
  const path = resolve(EXTENSION_DIR, fileName)
  const source = readFileSync(path, 'utf8')
  new Function(`${source}\n//# sourceURL=${path}`)()
}

/** Runs the content scripts in the order manifest.json injects them. */
export function loadContentScripts() {
  const manifest = JSON.parse(readFileSync(resolve(EXTENSION_DIR, 'manifest.json'), 'utf8'))
  manifest.content_scripts[0].js.forEach(loadExtensionScript)
}

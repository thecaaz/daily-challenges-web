#!/usr/bin/env node
// ScoreBridge release build helper. No dependencies; Node 18+.
//
//   node extension/build.mjs [--out <dir>]
//     Writes a release copy of the extension to <dir> (default extension/dist):
//     - manifest.json without any localhost permission / content-script match
//     - JS files with the `// @dev-only-start` ... `// @dev-only-end` blocks removed
//       (app-origins.js uses this to drop the localhost app origin)
//     - icons/
//     Zip and sign that directory (e.g. `web-ext sign --source-dir extension/dist`).
//
//   node extension/build.mjs --write-updates
//     Writes manifest.json's version into updates.json. Run this only AFTER the
//     signed XPI for that version is deployed (frontend/public/extension.xpi is
//     live), because Firefox installs whatever version updates.json advertises.
import { readFileSync, writeFileSync, mkdirSync, rmSync, cpSync, existsSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const SRC = dirname(fileURLToPath(import.meta.url))
const args = process.argv.slice(2)

function argValue(name) {
  const i = args.indexOf(name)
  return i === -1 ? null : args[i + 1]
}

function isLocalhostPattern(p) {
  return /^https?:\/\/(localhost|127\.0\.0\.1)(:[0-9*]+)?(\/|$)/.test(p)
}

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8'))
}

function writeJson(path, value) {
  writeFileSync(path, JSON.stringify(value, null, 2) + '\n')
}

export function releaseManifest(manifest) {
  const m = structuredClone(manifest)
  if (Array.isArray(m.permissions)) m.permissions = m.permissions.filter(p => !isLocalhostPattern(p))
  for (const cs of m.content_scripts || []) {
    cs.matches = (cs.matches || []).filter(p => !isLocalhostPattern(p))
  }
  return m
}

export function stripDevOnly(source) {
  return source.replace(/^[ \t]*\/\/ @dev-only-start[\s\S]*?\/\/ @dev-only-end[^\n]*\n?/gm, '')
}

function build(outDir) {
  const manifest = readJson(join(SRC, 'manifest.json'))
  const release = releaseManifest(manifest)

  const scripts = new Set([
    ...((release.background && release.background.scripts) || []),
    ...(release.content_scripts || []).flatMap(cs => cs.js || [])
  ])

  rmSync(outDir, { recursive: true, force: true })
  mkdirSync(outDir, { recursive: true })
  writeJson(join(outDir, 'manifest.json'), release)

  for (const file of scripts) {
    const code = stripDevOnly(readFileSync(join(SRC, file), 'utf8'))
    mkdirSync(dirname(join(outDir, file)), { recursive: true })
    writeFileSync(join(outDir, file), code)
  }
  if (existsSync(join(SRC, 'icons'))) cpSync(join(SRC, 'icons'), join(outDir, 'icons'), { recursive: true })

  // Guard: a release must never trust localhost.
  const leaked = [...scripts, 'manifest.json'].filter(f => /\/\/(localhost|127\.0\.0\.1)/.test(readFileSync(join(outDir, f), 'utf8')))
  if (leaked.length) {
    console.error(`localhost still present in release build: ${leaked.join(', ')}`)
    process.exit(1)
  }
  console.log(`ScoreBridge ${release.version} release build written to ${outDir}`)
}

function writeUpdates() {
  const { version, browser_specific_settings: bss } = readJson(join(SRC, 'manifest.json'))
  const id = bss && bss.gecko && bss.gecko.id
  const path = join(SRC, 'updates.json')
  const updates = readJson(path)
  const entry = updates.addons && updates.addons[id]
  if (!entry || !Array.isArray(entry.updates) || entry.updates.length === 0) {
    console.error(`updates.json has no update entry for ${id}`)
    process.exit(1)
  }
  entry.updates[entry.updates.length - 1].version = version
  writeJson(path, updates)
  console.log(`updates.json now advertises ${version}`)
}

const isMain = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) {
  if (args.includes('--write-updates')) writeUpdates()
  else build(resolve(argValue('--out') || join(SRC, 'dist')))
}

const fs = require('fs/promises');
const path = require('path');

async function delay(ms) { return new Promise(r => setTimeout(r, ms)); }

module.exports = async () => {
  const baseUrl = process.env['BASE_URL'] || 'http://localhost:5000';
  const credsPath = path.resolve(__dirname, 'credentials.json');
  const raw = await fs.readFile(credsPath, 'utf8');
  const creds = JSON.parse(raw);

  // Wait for backend to start responding
  const maxAttempts = 30;
  for (let i = 0; i < maxAttempts; i++) {
    try {
      const res = await fetch(baseUrl + '/');
      if (res.ok || res.status === 404) break;
    } catch (e) {
      // ignore
    }
    await delay(1000);
  }

  // Helper to register a user, tolerating already-exists
  const registerUrl = baseUrl + '/api/auth/register';
  async function registerUser(creds, tries = 6) {
    for (let attempt = 0; attempt < tries; attempt++) {
      try {
        const res = await fetch(registerUrl, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ username: creds.username, password: creds.password })
        });
        if ([200, 201, 409].includes(res.status)) return true;
      } catch (e) {
        // ignore and retry
      }
      await delay(1000);
    }
    return false;
  }

  // Register admin
  const okAdmin = await registerUser(creds, 6);
  if (!okAdmin) throw new Error('global-setup: failed to seed admin user');

  // Also register a normal user if credentials file present
  const normalCredsPath = path.resolve(__dirname, 'credentials-normal.json');
  try {
    const normalRaw = await fs.readFile(normalCredsPath, 'utf8');
    const normalCreds = JSON.parse(normalRaw);
    const okNormal = await registerUser(normalCreds, 6);
    if (!okNormal) throw new Error('global-setup: failed to seed normal user');
  } catch (e) {
    // If the file doesn't exist, ignore; otherwise rethrow
    if (e.code && e.code === 'ENOENT') {
      // no normal creds provided; that's fine
    } else {
      throw e;
    }
  }
};

## Deploy to HTTPS (production) — step-by-step

This guide describes a minimal, practical way to expose the app in this repo at a professional HTTPS URL on port 443 using Docker Compose + Caddy (automatic TLS via Let's Encrypt).

Context (your repo)
- `backend` currently listens on container port 5000 and is published as `5000:5000`.
- `frontend` currently exposes container port 80 and is published as `5173:80`.

Goal
- Serve `https://yourdomain.example` on port 443 with a trusted certificate, routing API requests to the backend and the SPA to the frontend.

Prerequisites
- A domain name (example.com) and access to its DNS control panel.
- The domain's A record must point to your Contabo VPS public IPv4.
- Docker and `docker-compose` installed on the server (you already run `docker-compose up -d`).
- Ports 80 and 443 reachable from the public internet (open any provider firewall + server firewall).

Summary of steps
1. Verify DNS and server ports
2. Add `Caddyfile` and `docker-compose.caddy.yml` to the project
3. Start Caddy together with your existing compose
4. Validate TLS and test
5. (Optional, recommended) hide internal host ports and add restart policies

Detailed steps

1) Verify DNS and make ports reachable

- In your DNS panel add an `A` record:

  - Host: `@` or `yourdomain`  
  - Value: `<YOUR_VPS_IPV4>`  
  - TTL: default (or 300)

- Ensure Linux firewall allows 80/443 (example using `ufw`):

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status
```

2) Add a Caddyfile

Create a `Caddyfile` at the repo root (replace `yourdomain.example` with your real domain):

```
yourdomain.example {
  # gzip/encoding and minimal logging
  encode gzip

  # Route API paths to backend service
  @api path /api/*
  reverse_proxy @api backend:5000

  # Websocket or other backend endpoints (add more handles if needed)
  @ws path /socket/*
  reverse_proxy @ws backend:5000

  # Everything else → frontend (SPA)
  reverse_proxy /* frontend:80
}

# Optional: also serve www subdomain; Caddy will get certs for both
www.yourdomain.example {
  redir https://yourdomain.example{uri}
}
```

Notes:
- This expects the `frontend` and `backend` services to be resolvable by Docker Compose service name (they will be when you start all compose files together).

3) Add a small compose file that runs Caddy

Create `docker-compose.caddy.yml` in the project root with:

```yaml
version: '3.8'

services:
  caddy:
    image: caddy:2
    restart: unless-stopped
    depends_on:
      - frontend
      - backend
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config

volumes:
  caddy_data:
  caddy_config:
```

4) Start the stack (build images and run)

Run from the project root where `docker-compose.yml` and `docker-compose.caddy.yml` live:

```bash
# build images and bring everything up
docker-compose -f docker-compose.yml -f docker-compose.caddy.yml up -d --build

# follow Caddy logs while certs are obtained
docker-compose logs -f caddy
```

What to watch for:
- Caddy will contact Let's Encrypt to obtain certs; logs will show ACME challenges and then a success message when certs are issued.
- If you see ACME errors, confirm port 80 is reachable publicly and DNS A record points to your server (use `dig`/`nslookup`).

5) Verify TLS and site

```bash
curl -I https://yourdomain.example
```

You should see `HTTP/2 200` (or `301` for a redirect) and a valid certificate. Also open the domain in a browser and check for the lock icon.

6) Optional: hide internal host ports (recommended)

Once HTTPS is working and you confirm routing, remove the host port mappings from `docker-compose.yml` for `backend` and `frontend` so they are not directly exposed to the public internet. Remove or comment the `ports:` entries:

```yaml
# backend:
#  ports:
#    - "5000:5000"

# frontend:
#  ports:
#    - "5173:80"
```

Then restart the stack with the combined compose files so only Caddy exposes ports 80/443:

```bash
docker-compose -f docker-compose.yml -f docker-compose.caddy.yml up -d --build
```

7) Add restart policy for services (recommended)

Add `restart: unless-stopped` to `backend` and `frontend` services in `docker-compose.yml` so they recover after reboots:

```yaml
services:
  backend:
    restart: unless-stopped
    ...
  frontend:
    restart: unless-stopped
    ...
```

8) Optional improvements
- Serve pre-built static frontend files
  - If `frontend` currently runs a dev server, replace it with a production static build (build into `dist` and serve via nginx/Caddy) for speed and stability.
- Add monitoring: use `docker-compose ps`, container logs, or a simple healthcheck/probe.
- Use DNS-01 (DNS challenge) if HTTP-01 is not possible (Caddy supports DNS providers with credentials).

9) Troubleshooting
- ACME/Let's Encrypt errors: check port 80, check DNS, wait for DNS propagation.
- Caddy cannot resolve `frontend`/`backend`: ensure you ran the combined compose command (`-f docker-compose.yml -f docker-compose.caddy.yml`) so the services share a network.
- If Cert issuance hits rate limits, wait and use staging ACME for testing.

10) Quick rollback

```bash
docker-compose -f docker-compose.yml -f docker-compose.caddy.yml down
```

Checklist (quick)
- [ ] Domain A record → VPS IP
- [ ] Ports 80 & 443 open
- [ ] `Caddyfile` created and domain replaced
- [ ] `docker-compose.caddy.yml` added
- [ ] `docker-compose -f docker-compose.yml -f docker-compose.caddy.yml up -d --build` ran successfully
- [ ] `curl -I https://yourdomain.example` returns 200 and shows valid cert

If you want, I can also:
- produce ready `Caddyfile` + `docker-compose.caddy.yml` files in the repo (I can create them for you), or
- generate an nginx + certbot alternative if you prefer `nginx`.

---
End of guide.

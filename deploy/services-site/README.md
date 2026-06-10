# services.bdabharatpur.org — deployment guide

Static hub page listing all BDA digital services. Served directly by Nginx — no app runtime.

## Files in this folder

- `index.html` — the page (self-contained: SEO meta + JSON-LD structured data, inline SVG icons, no CDN)
- `style.css` — all styles (Bootstrap is no longer loaded; the few classes used are ported here)
- `bda-logo.png` — BDA crest / favicon
- `robots.txt` — allows all crawlers, points to the sitemap
- `sitemap.xml` — single-URL sitemap (update `<lastmod>` on content changes)

> **SEO note:** the page no longer pulls Bootstrap/icons from jsDelivr (faster, no third-party
> dependency). After deploying, submit the site in Google Search Console and request indexing —
> see the SEO checklist handed over in chat. Update `sitemap.xml` `<lastmod>` whenever you edit content.

## Prerequisites

- Subdomain `services.bdabharatpur.org` → `A` record pointing to **69.62.80.7** (same VPS as PMS)
- DNS propagated (check with `dig services.bdabharatpur.org +short`)

## One-time VPS setup

SSH to the VPS (`ssh -p 2222 <user>@69.62.80.7`), then:

```bash
# 1. Create web root
sudo mkdir -p /var/www/services-bdabharatpur
sudo chown -R www-data:www-data /var/www/services-bdabharatpur

# 2. Install Nginx config (one-off)
sudo cp /tmp/nginx-services.conf /etc/nginx/sites-available/services-bdabharatpur
sudo ln -s /etc/nginx/sites-available/services-bdabharatpur /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx

# 3. Issue SSL certificate (after DNS is live)
sudo certbot --nginx -d services.bdabharatpur.org
```

Certbot will rewrite `nginx-services.conf` in place to add the 443 block and the 80 → 443 redirect. Don't hand-edit the file after that — re-run certbot to renew.

## Uploading / updating content

From your dev machine, inside `deploy/services-site/`:

```bash
scp -P 2222 index.html style.css bda-logo.png robots.txt sitemap.xml \
    <user>@69.62.80.7:/tmp/services-site/

# Then on the VPS:
sudo cp /tmp/services-site/* /var/www/services-bdabharatpur/
sudo chown -R www-data:www-data /var/www/services-bdabharatpur
```

`robots.txt` and `sitemap.xml` are served as-is by the existing Nginx `try_files` rule —
no config change needed. Verify after upload:
`curl -sI https://services.bdabharatpur.org/robots.txt` and `.../sitemap.xml` should return `200`.

No Nginx reload needed for content changes — just overwrite the files.

## Editing the service list

Edit `index.html`. Each service is one `<div class="col-sm-6 col-lg-3">…</div>` block — copy one as a template to add a new service.

Icon classes use Bootstrap Icons (full list: https://icons.getbootstrap.com). Tile colour classes available: `tile-blue`, `tile-orange`, `tile-green`, `tile-purple`.

## Local preview

Any static server works:

```bash
cd deploy/services-site
python -m http.server 8000
# → http://localhost:8000
```

## Rollback

Everything is in git — `git checkout` the previous version of `index.html` and re-upload.

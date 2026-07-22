# Cloudflare setup

Run bootstrap first, then `tools/deploy-cloudflare.ps1`. Wrangler may open a browser for login. On a new account, initialize the `workers.dev` subdomain in Dashboard → Workers & Pages before deploying. The script recognizes the “You need a workers.dev subdomain in order to proceed” failure and stops with the remedy.

Production secrets are Worker secrets, not `vars`; `.dev.vars` and `generated/` are ignored. `wrangler.jsonc` enables observability without logging token values. Use `npx wrangler tail` to inspect structured errors.


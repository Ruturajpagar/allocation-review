# Web (Angular 22)

The front end of the Allocation Review app. See the root [README](../README.md) for the full setup.

```bash
npm ci
npm start          # http://localhost:4200, proxies /api to http://localhost:5153 (proxy.conf.json)
npm test -- --watch=false
npx ng build       # production build into dist/web/browser
```

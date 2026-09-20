# FlowBB dashboard

Desktop PULSE and organizer surface. The normal path uses only the real ASP.NET
API configured by `VITE_API_URL`; there is no runtime fixture fallback.

```bash
cp .env.example .env
npm ci
npm run dev -- --port 5174
```

The dashboard uses:

- `GET /api/events` and the three PULSE endpoints for aggregate state,
- `/hubs/pulse` and `PulseUpdated` for live reconciliation,
- `POST /api/events` to create an Event, its dedicated Venue, and `HOSTED_AT`,
- MapLibre with the same OpenFreeMap `liberty` style as the resident client.

Verification:

```bash
npm run lint
npm run build
```

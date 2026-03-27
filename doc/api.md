# API (OpenAPI / Swagger)

La especificación OpenAPI (OpenSpec) está en `specs/openapi.yaml`.

## Backend de referencia

Código en `backend/` (Node + Express). Endpoints:
- `GET /health`
- `POST /sessions`
- `GET /sessions/{id}`
- `POST /sessions/{id}/join`
- `GET /profiles/{playerId}`
- `POST /profiles/{playerId}/progression`

## Ejecutar backend

```bash
cd backend
npm install
npm run dev
```


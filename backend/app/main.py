from __future__ import annotations

from fastapi import FastAPI

from app.api.routes import router

app = FastAPI(title="Lan House Manager", version="0.1.0")
app.include_router(router)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}

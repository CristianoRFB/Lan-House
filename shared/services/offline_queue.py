from __future__ import annotations

import json
import sqlite3
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any
from uuid import uuid4


@dataclass(slots=True)
class OfflineEvent:
    event_type: str
    payload: dict[str, Any]
    machine_id: str
    created_at: str
    synced_at: str | None = None
    id: str = ""


class OfflineEventQueue:
    def __init__(self, db_path: str | Path) -> None:
        self.db_path = str(db_path)
        self._init_db()

    def _connect(self) -> sqlite3.Connection:
        return sqlite3.connect(self.db_path)

    def _init_db(self) -> None:
        with self._connect() as conn:
            conn.execute(
                """
                CREATE TABLE IF NOT EXISTS offline_events (
                    id TEXT PRIMARY KEY,
                    event_type TEXT NOT NULL,
                    payload TEXT NOT NULL,
                    machine_id TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    synced_at TEXT NULL
                )
                """
            )
            conn.commit()

    def enqueue(self, event_type: str, payload: dict[str, Any], machine_id: str) -> OfflineEvent:
        event = OfflineEvent(
            id=str(uuid4()),
            event_type=event_type,
            payload=payload,
            machine_id=machine_id,
            created_at=datetime.utcnow().isoformat(),
        )
        with self._connect() as conn:
            conn.execute(
                "INSERT INTO offline_events (id, event_type, payload, machine_id, created_at, synced_at) VALUES (?, ?, ?, ?, ?, NULL)",
                (event.id, event.event_type, json.dumps(event.payload), event.machine_id, event.created_at),
            )
            conn.commit()
        return event

    def list_pending(self) -> list[OfflineEvent]:
        with self._connect() as conn:
            rows = conn.execute(
                "SELECT id, event_type, payload, machine_id, created_at, synced_at FROM offline_events WHERE synced_at IS NULL ORDER BY created_at ASC"
            ).fetchall()
        return [
            OfflineEvent(
                id=row[0],
                event_type=row[1],
                payload=json.loads(row[2]),
                machine_id=row[3],
                created_at=row[4],
                synced_at=row[5],
            )
            for row in rows
        ]

    def mark_synced(self, event_id: str) -> None:
        with self._connect() as conn:
            conn.execute(
                "UPDATE offline_events SET synced_at = ? WHERE id = ?",
                (datetime.utcnow().isoformat(), event_id),
            )
            conn.commit()

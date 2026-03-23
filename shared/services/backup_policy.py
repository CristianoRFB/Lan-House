from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, time, timedelta
from pathlib import Path


@dataclass(slots=True)
class BackupDecision:
    should_run: bool
    reason: str


class BackupPolicy:
    def __init__(self, *, daily_cutoff: time = time(hour=20, minute=30), retention_days: int = 60) -> None:
        self.daily_cutoff = daily_cutoff
        self.retention_days = retention_days

    def should_run_backup(self, now: datetime, last_success: datetime | None) -> BackupDecision:
        cutoff = datetime.combine(now.date(), self.daily_cutoff, tzinfo=now.tzinfo)
        if now < cutoff:
            return BackupDecision(False, "Ainda não passou do horário mínimo de backup diário.")
        if last_success is not None and last_success.date() == now.date() and last_success >= cutoff:
            return BackupDecision(False, "O backup diário de hoje já foi concluído.")
        return BackupDecision(True, "Executar backup diário antes do desligamento do admin.")

    def prune_candidates(self, backup_paths: list[Path], now: datetime) -> list[Path]:
        limit = now - timedelta(days=self.retention_days)
        return [path for path in backup_paths if datetime.fromtimestamp(path.stat().st_mtime, tz=now.tzinfo) < limit]

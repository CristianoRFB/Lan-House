from __future__ import annotations

from dataclasses import asdict, dataclass
from datetime import datetime
from decimal import Decimal
from pathlib import Path

from shared.domain.enums import SessionStatus, UserRole
from shared.domain.models import Session, User
from shared.services.offline_queue import OfflineEventQueue
from shared.services.session_policy import SessionPolicyEngine


@dataclass(slots=True)
class ClientRuntimeState:
    machine_name: str
    connected_to_server: bool
    session_status: SessionStatus
    remaining_minutes: int
    message: str | None = None


class ClientRuntime:
    def __init__(self, machine_name: str, queue_path: str | Path) -> None:
        self.machine_name = machine_name
        self.queue = OfflineEventQueue(queue_path)
        self.session_engine = SessionPolicyEngine()

    def evaluate_local_session(
        self,
        *,
        role: UserRole,
        remaining_minutes: int,
        consumed_minutes: int,
        idle_minutes: int = 0,
        connected_to_server: bool = True,
    ) -> ClientRuntimeState:
        user = User(
            username="local-user",
            pin="0000",
            role=role,
            display_name="Usuário Local",
            balance_minutes=remaining_minutes,
            note_limit=Decimal("0"),
            negative_balance_allowed=role in {UserRole.ADMIN, UserRole.SPECIAL},
        )
        session = Session(
            user_id=user.id,
            machine_id=self.machine_name,
            status=SessionStatus.ACTIVE,
            started_at=datetime.utcnow(),
            remaining_minutes=remaining_minutes,
            consumed_minutes=consumed_minutes,
            idle_minutes=idle_minutes,
        )
        evaluation = self.session_engine.evaluate(
            user,
            session,
            consumed_minutes=consumed_minutes,
            idle_minutes=idle_minutes,
        )
        state = ClientRuntimeState(
            machine_name=self.machine_name,
            connected_to_server=connected_to_server,
            session_status=evaluation.session_status,
            remaining_minutes=evaluation.remaining_minutes,
            message=evaluation.message,
        )
        if not connected_to_server:
            self.queue.enqueue("session_snapshot", asdict(state), self.machine_name)
        return state

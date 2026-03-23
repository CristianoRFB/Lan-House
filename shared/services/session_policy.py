from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime

from shared.domain.enums import NotificationLevel, SessionStatus, UserRole
from shared.domain.models import Notification, Session, User


ALERT_THRESHOLDS = (10, 5, 1)
DEFAULT_BLOCK_MESSAGE = "Seu tempo terminou. Procure o atendimento para liberar a máquina."


@dataclass(slots=True)
class SessionEvaluation:
    session_status: SessionStatus
    remaining_minutes: int
    should_logout: bool
    should_lock_screen: bool
    alert_minutes: int | None
    message: str | None


class SessionPolicyEngine:
    def evaluate(
        self,
        user: User,
        session: Session,
        *,
        consumed_minutes: int,
        idle_minutes: int = 0,
        pause_on_idle: bool = True,
    ) -> SessionEvaluation:
        effective_consumption = consumed_minutes
        if pause_on_idle:
            effective_consumption = max(consumed_minutes - idle_minutes, 0)

        if user.role is UserRole.GHOST:
            return SessionEvaluation(
                session_status=SessionStatus.ACTIVE,
                remaining_minutes=session.remaining_minutes,
                should_logout=False,
                should_lock_screen=False,
                alert_minutes=None,
                message="Modo ghost ativo. Tempo não é contabilizado.",
            )

        remaining = session.remaining_minutes - effective_consumption
        alert = remaining if remaining in ALERT_THRESHOLDS else None

        if user.role in {UserRole.ADMIN, UserRole.SPECIAL}:
            return SessionEvaluation(
                session_status=SessionStatus.ACTIVE,
                remaining_minutes=remaining,
                should_logout=False,
                should_lock_screen=False,
                alert_minutes=alert,
                message=None if remaining >= 0 else "Sessão liberada mesmo com saldo negativo.",
            )

        if remaining <= 0:
            return SessionEvaluation(
                session_status=SessionStatus.BLOCKED,
                remaining_minutes=0,
                should_logout=True,
                should_lock_screen=True,
                alert_minutes=0,
                message=DEFAULT_BLOCK_MESSAGE,
            )

        return SessionEvaluation(
            session_status=SessionStatus.ACTIVE,
            remaining_minutes=remaining,
            should_logout=False,
            should_lock_screen=False,
            alert_minutes=alert,
            message=None,
        )

    def build_alert_notification(self, machine_id: str, remaining_minutes: int, user_id: str | None = None) -> Notification:
        if remaining_minutes <= 1:
            level = NotificationLevel.CRITICAL
        elif remaining_minutes <= 5:
            level = NotificationLevel.WARNING
        else:
            level = NotificationLevel.INFO

        return Notification(
            machine_id=machine_id,
            title="Aviso de tempo",
            message=f"Restam {remaining_minutes} minuto(s) na sessão.",
            level=level,
            created_at=datetime.utcnow(),
            recipient_user_id=user_id,
        )

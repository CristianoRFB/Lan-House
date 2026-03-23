from __future__ import annotations

from datetime import datetime
from decimal import Decimal
from pathlib import Path

from fastapi import APIRouter
from pydantic import BaseModel, Field

from app.domain.enums import DeviceType, MachineStatus, SessionStatus, UserRole
from app.domain.models import LedgerEntry, Machine, Session, User
from app.services.offline_queue import OfflineEventQueue
from app.services.report_service import ReportAggregator
from app.services.session_service import SessionPolicyEngine

router = APIRouter(prefix="/api")
session_engine = SessionPolicyEngine()
queue = OfflineEventQueue(Path("client-offline.db"))
report_aggregator = ReportAggregator()


class SessionEvaluateRequest(BaseModel):
    role: UserRole
    balance_minutes: int = Field(ge=0)
    remaining_minutes: int
    consumed_minutes: int = Field(ge=0)
    idle_minutes: int = Field(default=0, ge=0)


class OfflineEventRequest(BaseModel):
    event_type: str
    payload: dict
    machine_id: str


@router.get("/summary")
def get_summary() -> dict:
    return {
        "app": "lan-house-manager",
        "mode": "bootstrap",
        "features": [
            "offline-queue",
            "session-policy",
            "reports",
            "backup-policy",
        ],
    }


@router.post("/session/evaluate")
def evaluate_session(payload: SessionEvaluateRequest) -> dict:
    user = User(
        username="demo",
        pin="1234",
        role=payload.role,
        display_name="Usuário Demo",
        balance_minutes=payload.balance_minutes,
        note_limit=Decimal("0"),
        negative_balance_allowed=payload.role in {UserRole.ADMIN, UserRole.SPECIAL},
    )
    session = Session(
        user_id=user.id,
        machine_id="machine-1",
        status=SessionStatus.ACTIVE,
        started_at=datetime.utcnow(),
        remaining_minutes=payload.remaining_minutes,
        idle_minutes=payload.idle_minutes,
    )
    evaluation = session_engine.evaluate(
        user,
        session,
        consumed_minutes=payload.consumed_minutes,
        idle_minutes=payload.idle_minutes,
    )
    return evaluation.__dict__


@router.post("/offline/events")
def create_offline_event(payload: OfflineEventRequest) -> dict:
    event = queue.enqueue(payload.event_type, payload.payload, payload.machine_id)
    return event.__dict__


@router.get("/offline/events")
def list_pending_events() -> list[dict]:
    return [event.__dict__ for event in queue.list_pending()]


@router.get("/reports/usage")
def get_usage_report() -> dict:
    pc = Machine(name="PC-01", ip_address="192.168.0.10", status=MachineStatus.ONLINE)
    ps = Machine(name="PS-01", ip_address="192.168.0.50", device_type=DeviceType.PLAYSTATION, status=MachineStatus.ONLINE)
    sessions = [
        Session(user_id="u1", machine_id=pc.id, status=SessionStatus.ACTIVE, started_at=datetime.utcnow(), remaining_minutes=30, idle_minutes=5),
        Session(user_id="u2", machine_id=ps.id, status=SessionStatus.ENDED, started_at=datetime.utcnow(), remaining_minutes=90, idle_minutes=0),
    ]
    ledger = [
        LedgerEntry(user_id="u1", amount=Decimal("20.00"), reason="Anotação", created_at=datetime.utcnow()),
        LedgerEntry(user_id="u2", amount=Decimal("30.00"), reason="Promessa", created_at=datetime.utcnow(), promised_payment_date=datetime.utcnow()),
    ]
    report = report_aggregator.build_usage_report(sessions, [pc, ps], ledger)
    return {
        "total_pc_minutes": report.total_pc_minutes,
        "total_playstation_minutes": report.total_playstation_minutes,
        "average_session_minutes": report.average_session_minutes,
        "machine_usage": report.machine_usage,
        "pending_notes_total": str(report.pending_notes_total),
        "promised_payments_total": str(report.promised_payments_total),
        "active_sessions": report.active_sessions,
    }

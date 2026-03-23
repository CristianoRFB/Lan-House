from __future__ import annotations

from datetime import datetime, timedelta
from decimal import Decimal
from pathlib import Path

from fastapi import FastAPI
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field

from shared.domain.enums import CommandType, DeviceType, MachineStatus, SessionStatus, UserRole
from shared.domain.models import LedgerEntry, Machine, RemoteCommand, Session, User
from shared.services.offline_queue import OfflineEventQueue
from shared.services.report_service import ReportAggregator
from shared.services.session_policy import SessionPolicyEngine

BASE_DIR = Path(__file__).resolve().parent
WEB_DIR = BASE_DIR / "web"
app = FastAPI(title="Lan House Manager Admin", version="0.2.0")
app.mount("/assets", StaticFiles(directory=WEB_DIR), name="assets")

session_engine = SessionPolicyEngine()
report_aggregator = ReportAggregator()
queue = OfflineEventQueue(BASE_DIR / "admin-offline.db")


@app.get("/")
def admin_index() -> FileResponse:
    return FileResponse(WEB_DIR / "index.html")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "surface": "admin-web"}


@app.get("/api/dashboard")
def dashboard() -> dict:
    pc1 = Machine(name="PC-01", ip_address="192.168.0.10", status=MachineStatus.ONLINE)
    pc2 = Machine(name="PC-02", ip_address="192.168.0.11", status=MachineStatus.BLOCKED)
    ps1 = Machine(name="PS-01", ip_address="192.168.0.50", device_type=DeviceType.PLAYSTATION, status=MachineStatus.ONLINE)
    sessions = [
        Session(user_id="u1", machine_id=pc1.id, status=SessionStatus.ACTIVE, started_at=datetime.utcnow(), remaining_minutes=45, consumed_minutes=75, idle_minutes=3),
        Session(user_id="u2", machine_id=pc2.id, status=SessionStatus.BLOCKED, started_at=datetime.utcnow(), remaining_minutes=0, consumed_minutes=120),
        Session(user_id="u3", machine_id=ps1.id, status=SessionStatus.ACTIVE, started_at=datetime.utcnow(), remaining_minutes=90, consumed_minutes=60),
    ]
    ledger = [
        LedgerEntry(user_id="u1", amount=Decimal("12.00"), reason="Anotação", created_at=datetime.utcnow()),
        LedgerEntry(user_id="u3", amount=Decimal("25.00"), reason="Promessa", created_at=datetime.utcnow(), promised_payment_date=datetime.utcnow() + timedelta(days=2)),
    ]
    report = report_aggregator.build_usage_report(sessions, [pc1, pc2, ps1], ledger)
    return {
        "summary": {
            "machines_active": 2,
            "machines_blocked": 1,
            "pending_notes_total": str(report.pending_notes_total),
            "promised_payments_total": str(report.promised_payments_total),
            "total_pc_minutes": report.total_pc_minutes,
            "total_playstation_minutes": report.total_playstation_minutes,
        },
        "machines": [
            {
                "name": pc1.name,
                "ip": pc1.ip_address,
                "status": pc1.status.value,
                "user": "joao",
                "remaining_minutes": 45,
            },
            {
                "name": pc2.name,
                "ip": pc2.ip_address,
                "status": pc2.status.value,
                "user": "maria",
                "remaining_minutes": 0,
            },
            {
                "name": ps1.name,
                "ip": ps1.ip_address,
                "status": ps1.status.value,
                "user": "visitante-ps",
                "remaining_minutes": 90,
            },
        ],
        "notifications": [
            {"title": "Bem-vindo, joao", "message": "Sessão iniciada com sucesso no PC-01."},
            {"title": "Tempo encerrado", "message": "PC-02 bloqueado aguardando atendimento."},
        ],
        "quick_commands": [command.value for command in CommandType],
    }


class SessionEvaluateRequest(BaseModel):
    role: UserRole
    balance_minutes: int = Field(ge=0)
    remaining_minutes: int
    consumed_minutes: int = Field(ge=0)
    idle_minutes: int = Field(default=0, ge=0)


@app.post("/api/session/evaluate")
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
        machine_id="pc-demo",
        status=SessionStatus.ACTIVE,
        started_at=datetime.utcnow(),
        remaining_minutes=payload.remaining_minutes,
        consumed_minutes=payload.consumed_minutes,
        idle_minutes=payload.idle_minutes,
    )
    return session_engine.evaluate(
        user,
        session,
        consumed_minutes=payload.consumed_minutes,
        idle_minutes=payload.idle_minutes,
    ).__dict__


class CommandRequest(BaseModel):
    machine_name: str
    command: CommandType
    message: str | None = None


@app.post("/api/commands")
def send_command(payload: CommandRequest) -> dict:
    command = RemoteCommand(
        machine_id=payload.machine_name,
        command_type=payload.command,
        payload={"message": payload.message},
        created_at=datetime.utcnow(),
    )
    queue.enqueue("remote_command", command.payload | {"command": command.command_type.value}, payload.machine_name)
    return {"queued": True, "machine": payload.machine_name, "command": payload.command.value}

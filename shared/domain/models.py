from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from decimal import Decimal
from typing import Any
from uuid import uuid4

from .enums import CommandType, DeviceType, MachineStatus, NotificationLevel, SessionStatus, UserRole


@dataclass(slots=True)
class User:
    username: str
    pin: str
    role: UserRole
    display_name: str
    balance_minutes: int = 0
    note_limit: Decimal = Decimal("0")
    negative_balance_allowed: bool = False
    can_self_register: bool = False
    id: str = field(default_factory=lambda: str(uuid4()))


@dataclass(slots=True)
class Machine:
    name: str
    ip_address: str
    device_type: DeviceType = DeviceType.PC
    status: MachineStatus = MachineStatus.OFFLINE
    current_user_id: str | None = None
    last_seen_at: datetime | None = None
    id: str = field(default_factory=lambda: str(uuid4()))


@dataclass(slots=True)
class Session:
    user_id: str
    machine_id: str
    status: SessionStatus
    started_at: datetime
    remaining_minutes: int
    consumed_minutes: int = 0
    is_idle: bool = False
    idle_minutes: int = 0
    ended_at: datetime | None = None
    id: str = field(default_factory=lambda: str(uuid4()))


@dataclass(slots=True)
class LedgerEntry:
    user_id: str
    amount: Decimal
    reason: str
    created_at: datetime
    promised_payment_date: datetime | None = None
    id: str = field(default_factory=lambda: str(uuid4()))


@dataclass(slots=True)
class Notification:
    machine_id: str
    title: str
    message: str
    level: NotificationLevel
    created_at: datetime
    recipient_user_id: str | None = None
    id: str = field(default_factory=lambda: str(uuid4()))


@dataclass(slots=True)
class RemoteCommand:
    machine_id: str
    command_type: CommandType
    payload: dict[str, Any]
    created_at: datetime
    executed: bool = False
    id: str = field(default_factory=lambda: str(uuid4()))

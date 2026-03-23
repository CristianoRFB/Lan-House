from __future__ import annotations

from collections import Counter, defaultdict
from dataclasses import dataclass
from decimal import Decimal

from shared.domain.enums import DeviceType, SessionStatus
from shared.domain.models import LedgerEntry, Machine, Session


@dataclass(slots=True)
class UsageReport:
    total_pc_minutes: int
    total_playstation_minutes: int
    average_session_minutes: float
    machine_usage: dict[str, int]
    pending_notes_total: Decimal
    promised_payments_total: Decimal
    active_sessions: int


class ReportAggregator:
    def build_usage_report(
        self,
        sessions: list[Session],
        machines: list[Machine],
        ledger_entries: list[LedgerEntry],
    ) -> UsageReport:
        machine_map = {machine.id: machine for machine in machines}
        machine_usage_counter: Counter[str] = Counter()
        total_pc = 0
        total_ps = 0
        session_lengths: list[int] = []
        active_sessions = 0

        for session in sessions:
            consumed = max(session.consumed_minutes, 0)
            machine = machine_map.get(session.machine_id)
            if machine is None:
                continue

            machine_usage_counter[machine.name] += consumed
            session_lengths.append(consumed)
            if session.status is SessionStatus.ACTIVE:
                active_sessions += 1

            if machine.device_type is DeviceType.PLAYSTATION:
                total_ps += consumed
            else:
                total_pc += consumed

        pending_notes_total = Decimal("0")
        promised_payments_total = Decimal("0")
        for entry in ledger_entries:
            if entry.amount > 0:
                pending_notes_total += entry.amount
            if entry.promised_payment_date is not None:
                promised_payments_total += entry.amount

        average = sum(session_lengths) / len(session_lengths) if session_lengths else 0.0
        return UsageReport(
            total_pc_minutes=total_pc,
            total_playstation_minutes=total_ps,
            average_session_minutes=average,
            machine_usage=dict(machine_usage_counter),
            pending_notes_total=pending_notes_total,
            promised_payments_total=promised_payments_total,
            active_sessions=active_sessions,
        )

    def promised_payments_calendar(self, ledger_entries: list[LedgerEntry]) -> dict[str, list[str]]:
        calendar: dict[str, list[str]] = defaultdict(list)
        for entry in ledger_entries:
            if entry.promised_payment_date is None:
                continue
            day = entry.promised_payment_date.date().isoformat()
            calendar[day].append(f"{entry.user_id}: R$ {entry.amount}")
        return dict(calendar)

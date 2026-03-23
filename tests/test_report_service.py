from __future__ import annotations

from datetime import datetime, timedelta
from decimal import Decimal
import unittest

from shared.domain.enums import DeviceType, MachineStatus, SessionStatus
from shared.domain.models import LedgerEntry, Machine, Session
from shared.services.report_service import ReportAggregator


class ReportAggregatorTests(unittest.TestCase):
    def test_build_usage_report_separates_pc_and_playstation(self) -> None:
        aggregator = ReportAggregator()
        pc = Machine(name="PC-01", ip_address="192.168.0.10", status=MachineStatus.ONLINE)
        ps = Machine(name="PS-01", ip_address="192.168.0.20", device_type=DeviceType.PLAYSTATION, status=MachineStatus.ONLINE)
        sessions = [
            Session(user_id="u1", machine_id=pc.id, status=SessionStatus.ACTIVE, started_at=datetime.utcnow(), remaining_minutes=30, consumed_minutes=35),
            Session(user_id="u2", machine_id=ps.id, status=SessionStatus.ENDED, started_at=datetime.utcnow(), remaining_minutes=60, consumed_minutes=60),
        ]
        ledger = [
            LedgerEntry(user_id="u1", amount=Decimal("10.00"), reason="Anotação", created_at=datetime.utcnow()),
            LedgerEntry(user_id="u2", amount=Decimal("25.00"), reason="Promessa", created_at=datetime.utcnow(), promised_payment_date=datetime.utcnow() + timedelta(days=1)),
        ]

        report = aggregator.build_usage_report(sessions, [pc, ps], ledger)
        calendar = aggregator.promised_payments_calendar(ledger)

        self.assertEqual(report.total_pc_minutes, 35)
        self.assertEqual(report.total_playstation_minutes, 60)
        self.assertEqual(report.pending_notes_total, Decimal("35.00"))
        self.assertEqual(report.promised_payments_total, Decimal("25.00"))
        self.assertIn((datetime.utcnow() + timedelta(days=1)).date().isoformat(), calendar)


if __name__ == "__main__":
    unittest.main()

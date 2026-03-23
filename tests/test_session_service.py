from __future__ import annotations

import unittest
from datetime import datetime
from decimal import Decimal

from shared.domain.enums import SessionStatus, UserRole
from shared.domain.models import Session, User
from shared.services.session_policy import SessionPolicyEngine


class SessionPolicyEngineTests(unittest.TestCase):
    def setUp(self) -> None:
        self.engine = SessionPolicyEngine()

    def test_regular_user_is_blocked_when_time_ends(self) -> None:
        user = User(username="joao", pin="1234", role=UserRole.REGULAR, display_name="João", note_limit=Decimal("10.00"))
        session = Session(
            user_id=user.id,
            machine_id="pc-01",
            status=SessionStatus.ACTIVE,
            started_at=datetime.utcnow(),
            remaining_minutes=5,
        )

        result = self.engine.evaluate(user, session, consumed_minutes=5)

        self.assertEqual(result.session_status, SessionStatus.BLOCKED)
        self.assertTrue(result.should_logout)
        self.assertTrue(result.should_lock_screen)
        self.assertEqual(result.remaining_minutes, 0)

    def test_special_user_can_continue_with_negative_balance(self) -> None:
        user = User(username="vip", pin="9999", role=UserRole.SPECIAL, display_name="VIP", negative_balance_allowed=True)
        session = Session(
            user_id=user.id,
            machine_id="pc-02",
            status=SessionStatus.ACTIVE,
            started_at=datetime.utcnow(),
            remaining_minutes=2,
        )

        result = self.engine.evaluate(user, session, consumed_minutes=10)

        self.assertEqual(result.session_status, SessionStatus.ACTIVE)
        self.assertFalse(result.should_logout)
        self.assertLess(result.remaining_minutes, 0)

    def test_idle_time_can_pause_consumption(self) -> None:
        user = User(username="maria", pin="2222", role=UserRole.REGULAR, display_name="Maria")
        session = Session(
            user_id=user.id,
            machine_id="pc-03",
            status=SessionStatus.ACTIVE,
            started_at=datetime.utcnow(),
            remaining_minutes=20,
        )

        result = self.engine.evaluate(user, session, consumed_minutes=10, idle_minutes=4, pause_on_idle=True)

        self.assertEqual(result.remaining_minutes, 14)
        self.assertEqual(result.session_status, SessionStatus.ACTIVE)


if __name__ == "__main__":
    unittest.main()

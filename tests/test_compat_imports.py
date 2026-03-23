from __future__ import annotations

import unittest

from backend.app.domain.enums import UserRole
from backend.app.services.offline_queue import OfflineEventQueue
from backend.app.services.session_service import SessionPolicyEngine


class CompatibilityLayerTests(unittest.TestCase):
    def test_legacy_backend_namespace_still_exports_expected_symbols(self) -> None:
        self.assertEqual(SessionPolicyEngine.__name__, "SessionPolicyEngine")
        self.assertEqual(OfflineEventQueue.__name__, "OfflineEventQueue")
        self.assertEqual(UserRole.ADMIN.value, "admin")


if __name__ == "__main__":
    unittest.main()

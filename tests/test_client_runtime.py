from __future__ import annotations

from pathlib import Path
from tempfile import TemporaryDirectory
import unittest

from client.agent.runtime import ClientRuntime
from shared.domain.enums import SessionStatus, UserRole


class ClientRuntimeTests(unittest.TestCase):
    def test_runtime_queues_snapshot_when_offline(self) -> None:
        with TemporaryDirectory() as temp_dir:
            runtime = ClientRuntime("PC-07", Path(temp_dir) / "client.db")
            state = runtime.evaluate_local_session(
                role=UserRole.REGULAR,
                remaining_minutes=15,
                consumed_minutes=5,
                connected_to_server=False,
            )

            self.assertEqual(state.session_status, SessionStatus.ACTIVE)
            pending = runtime.queue.list_pending()
            self.assertEqual(len(pending), 1)
            self.assertEqual(pending[0].event_type, "session_snapshot")


if __name__ == "__main__":
    unittest.main()

from __future__ import annotations

from pathlib import Path
from tempfile import TemporaryDirectory
import unittest

from shared.services.offline_queue import OfflineEventQueue


class OfflineEventQueueTests(unittest.TestCase):
    def test_enqueue_list_and_mark_synced(self) -> None:
        with TemporaryDirectory() as temp_dir:
            queue = OfflineEventQueue(Path(temp_dir) / "offline.db")
            created = queue.enqueue("login_request", {"username": "joao"}, "pc-01")

            pending = queue.list_pending()
            self.assertEqual(len(pending), 1)
            self.assertEqual(pending[0].id, created.id)
            self.assertEqual(pending[0].payload["username"], "joao")

            queue.mark_synced(created.id)
            self.assertEqual(queue.list_pending(), [])


if __name__ == "__main__":
    unittest.main()

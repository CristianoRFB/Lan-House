from __future__ import annotations

from enum import Enum


class UserRole(str, Enum):
    ADMIN = "admin"
    GHOST = "ghost"
    SPECIAL = "special"
    REGULAR = "regular"


class MachineStatus(str, Enum):
    ONLINE = "online"
    OFFLINE = "offline"
    BLOCKED = "blocked"
    MAINTENANCE = "maintenance"


class SessionStatus(str, Enum):
    PENDING = "pending"
    ACTIVE = "active"
    PAUSED = "paused"
    BLOCKED = "blocked"
    ENDED = "ended"


class NotificationLevel(str, Enum):
    INFO = "info"
    WARNING = "warning"
    CRITICAL = "critical"


class CommandType(str, Enum):
    LOCK = "lock"
    RESTART = "restart"
    LOGOUT = "logout"
    SCREENSHOT = "screenshot"
    CLEAR_TEMP = "clear_temp"
    MESSAGE = "message"


class DeviceType(str, Enum):
    PC = "pc"
    PLAYSTATION = "playstation"

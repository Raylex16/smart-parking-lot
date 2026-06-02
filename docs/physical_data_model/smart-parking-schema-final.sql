-- ============================================================
--  Smart Parking Lot — Modelo físico de datos
--  Motor: SQLite (compatible con EF Core migrations)
--  Alcance: sin Vehicle, sin VehiclePlate, sin cámara LPR
-- ============================================================

PRAGMA foreign_keys = ON;

-- ──────────────────────────────────────────────
-- 0. Referencia: tabla ya existente (no recrear)
-- ──────────────────────────────────────────────
-- CREATE TABLE IF NOT EXISTS ParkingLots (
--     Id   TEXT NOT NULL PRIMARY KEY,
--     Name TEXT NOT NULL,
--     Mode INTEGER NOT NULL DEFAULT 0
-- );

-- ──────────────────────────────────────────────
-- 1. Spots
-- ──────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS Spots (
    Id           TEXT    NOT NULL PRIMARY KEY,
    IsOccupied   INTEGER NOT NULL DEFAULT 0          -- 0 = libre, 1 = ocupado
                         CHECK (IsOccupied IN (0, 1)),
    LastChanged  TEXT    NOT NULL                    -- ISO-8601: '2026-06-02T10:30:00Z'
                         DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
    LotId        TEXT    NOT NULL
                         REFERENCES ParkingLots(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_spots_lot      ON Spots(LotId);
CREATE INDEX IF NOT EXISTS idx_spots_occupied ON Spots(IsOccupied);

-- ──────────────────────────────────────────────
-- 2. Gates
-- ──────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS Gates (
    Id     TEXT NOT NULL PRIMARY KEY,
    Type   TEXT NOT NULL
                CHECK (Type IN ('ENTRY', 'EXIT', 'BOTH')),
    State  TEXT NOT NULL DEFAULT 'CLOSED'
                CHECK (State IN ('OPEN', 'CLOSED', 'FAULT')),
    LotId  TEXT NOT NULL
                REFERENCES ParkingLots(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_gates_lot   ON Gates(LotId);
CREATE INDEX IF NOT EXISTS idx_gates_state ON Gates(State);

-- ──────────────────────────────────────────────
-- 3. EventLog  (ocupación + acceso, inmutable)
-- ──────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS EventLog (
    Id         TEXT    NOT NULL PRIMARY KEY,
    EventType  TEXT    NOT NULL
                       CHECK (EventType IN ('OCCUPIED', 'RELEASED', 'ENTRY', 'EXIT')),
    Timestamp  TEXT    NOT NULL
                       DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
    Approved   INTEGER,                              -- NULL para eventos de ocupación
    SpotId     TEXT    REFERENCES Spots(Id) ON DELETE SET NULL,
    GateId     TEXT    REFERENCES Gates(Id) ON DELETE SET NULL,
    LotId      TEXT    NOT NULL
                       REFERENCES ParkingLots(Id) ON DELETE CASCADE,

    -- Al menos uno de SpotId o GateId debe estar presente
    CHECK (SpotId IS NOT NULL OR GateId IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS idx_eventlog_lot       ON EventLog(LotId);
CREATE INDEX IF NOT EXISTS idx_eventlog_timestamp ON EventLog(Timestamp);
CREATE INDEX IF NOT EXISTS idx_eventlog_spot      ON EventLog(SpotId);
CREATE INDEX IF NOT EXISTS idx_eventlog_gate      ON EventLog(GateId);

-- ──────────────────────────────────────────────
-- 4. Datos semilla mínimos
--    (usa el lot ya sembrado por SeedInitialDataAsync)
-- ──────────────────────────────────────────────
INSERT OR IGNORE INTO Spots VALUES ('S-01', 0, strftime('%Y-%m-%dT%H:%M:%SZ','now'), 'LOT-01');
INSERT OR IGNORE INTO Spots VALUES ('S-02', 0, strftime('%Y-%m-%dT%H:%M:%SZ','now'), 'LOT-01');
INSERT OR IGNORE INTO Gates VALUES ('G-01', 'ENTRY', 'CLOSED', 'LOT-01');
INSERT OR IGNORE INTO Gates VALUES ('G-02', 'EXIT',  'CLOSED', 'LOT-01');

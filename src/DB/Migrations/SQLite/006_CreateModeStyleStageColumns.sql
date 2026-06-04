BEGIN TRANSACTION;

CREATE TABLE PlayerStageTimes_new
(
    MapName       TEXT,
    SteamID       TEXT,
    PlayerName    TEXT,
    Stage         INT,
    TimerTicks    INT,
    FormattedTime TEXT,
    Velocity      TEXT,
    Style         INT  DEFAULT 0,
    Mode          TEXT DEFAULT 'None',
    PRIMARY KEY (MapName, SteamID, Stage, Style, Mode)
);

INSERT INTO PlayerStageTimes_new
SELECT MapName,
       SteamID,
       PlayerName,
       Stage,
       TimerTicks,
       FormattedTime,
       Velocity,
       0,
       'None'
FROM PlayerStageTimes;

DROP TABLE PlayerStageTimes;
ALTER TABLE PlayerStageTimes_new RENAME TO PlayerStageTimes;

COMMIT;

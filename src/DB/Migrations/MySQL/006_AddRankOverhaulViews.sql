CREATE VIEW IF NOT EXISTS cs2_surf.MapCompletions AS
SELECT `PlayerRecords`.`MapName`            AS `MapName`,
       SUM(`PlayerRecords`.`TimesFinished`) AS `Completions`
FROM `PlayerRecords`
GROUP BY `PlayerRecords`.`MapName`;

CREATE TABLE IF NOT EXISTS MapTiers
(
    MapName VARCHAR(255)  NOT NULL
        PRIMARY KEY,
    Tier    INT DEFAULT 1 NOT NULL
);

CREATE VIEW IF NOT EXISTS cs2_surf.MapWRs AS
SELECT `Map`.`MapName`                                      AS `MapName`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`))                 AS `WR`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.8           AS `Rank2`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.75          AS `Rank3`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.7           AS `Rank4`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.65          AS `Rank5`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.6           AS `Rank6`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.55          AS `Rank7`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.5           AS `Rank8`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.45          AS `Rank9`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.4           AS `Rank10`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.25          AS `Group1`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.25 / 1.5    AS `Group2`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.25 / 2.25   AS `Group3`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.25 / 3.375  AS `Group4`,
       GREATEST(`TP`.`MaxPoints`,
                COALESCE(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.25 / 5.0625 AS `Group5`
FROM ((`MapCompletions` `Map` JOIN `MapTiers` `MT`
       ON (`Map`.`MapName` = `MT`.`MapName`)) JOIN `TierPoints` `TP` ON (`MT`.`Tier` = `TP`.`Tier`));

CREATE VIEW IF NOT EXISTS cs2_surf.PlayerRanks AS
SELECT `PlayerRecords`.`MapName`                                                                                      AS `MapName`,
       `PlayerRecords`.`SteamID`                                                                                      AS `SteamID`,
       RANK() OVER ( PARTITION BY `PlayerRecords`.`MapName` ORDER BY `PlayerRecords`.`TimerTicks`)         AS `Rank`,
       PERCENT_RANK() OVER ( PARTITION BY `PlayerRecords`.`MapName` ORDER BY `PlayerRecords`.`TimerTicks`) AS `Percentile`
FROM `PlayerRecords`
WHERE `PlayerRecords`.`Style` = 0;



CREATE VIEW IF NOT EXISTS cs2_surf.PlayerPoints AS
SELECT `Ranks`.`SteamID`                                      AS `SteamID`,
       `MT`.`MapName`                                         AS `MapName`,
       `Ranks`.`Percentile`                                   AS `Percentile`,
       CASE
           WHEN `Ranks`.`Percentile` < 0.5 THEN CASE `Ranks`.`Rank`
                                                    WHEN 1 THEN `MWR`.`WR`
                                                    WHEN 2 THEN `MWR`.`Rank2`
                                                    WHEN 3 THEN `MWR`.`Rank3`
                                                    WHEN 4 THEN `MWR`.`Rank4`
                                                    WHEN 5 THEN `MWR`.`Rank5`
                                                    WHEN 6 THEN `MWR`.`Rank6`
                                                    WHEN 7 THEN `MWR`.`Rank7`
                                                    WHEN 8 THEN `MWR`.`Rank8`
                                                    WHEN 9 THEN `MWR`.`Rank9`
                                                    WHEN 10 THEN `MWR`.`Rank10` END
           ELSE CASE
                    WHEN `Ranks`.`Percentile` < 0.03125 THEN `MWR`.`Group1`
                    WHEN `Ranks`.`Percentile` < 0.06250 THEN `MWR`.`Group2`
                    WHEN `Ranks`.`Percentile` < 0.12500 THEN `MWR`.`Group3`
                    WHEN `Ranks`.`Percentile` < 0.25000 THEN `MWR`.`Group4`
                    WHEN `Ranks`.`Percentile` < 0.50000 THEN `MWR`.`Group5`
                    ELSE 0 END END + CASE `MT`.`Tier`
                                         WHEN 1 THEN 25
                                         WHEN 2 THEN 50
                                         WHEN 3 THEN 100
                                         WHEN 4 THEN 200
                                         WHEN 5 THEN 400
                                         WHEN 6 THEN 600
                                         WHEN 7 THEN 800
                                         WHEN 8 THEN 1000 END AS `Points`
FROM ((`PlayerRanks` `Ranks` JOIN `MapWRs` `MWR`
       ON (`Ranks`.`MapName` = `MWR`.`MapName`)) JOIN `MapTiers` `MT` ON (`MWR`.`MapName` = `MT`.`MapName`));

CREATE VIEW IF NOT EXISTS cs2_surf.PlayerLeaderboard AS
SELECT `PP`.`SteamID` AS `SteamID`, `PR`.`PlayerName` AS `PlayerName`, SUM(`PP`.`Points`) AS `GlobalPoints`
FROM (`PlayerPoints` `PP` JOIN `PlayerRecords` `PR` ON (`PP`.`SteamID` = `PR`.`SteamID`))
GROUP BY `PP`.`SteamID`;


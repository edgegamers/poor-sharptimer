CREATE VIEW IF NOT EXISTS MapCompletions AS
select `PlayerRecords`.`MapName`            AS `MapName`,
       sum(`PlayerRecords`.`TimesFinished`) AS `Completions`
from `PlayerRecords`
group by `PlayerRecords`.`MapName`;

CREATE VIEW IF NOT EXISTS MapWRs AS
select `Map`.`MapName`                                      AS `MapName`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`))                 AS `WR`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.8           AS `Rank2`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.75          AS `Rank3`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.7           AS `Rank4`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.65          AS `Rank5`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.6           AS `Rank6`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.55          AS `Rank7`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.5           AS `Rank8`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.45          AS `Rank9`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.4           AS `Rank10`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.25          AS `Group1`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.25 / 1.5    AS `Group2`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.25 / 2.25   AS `Group3`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.25 / 3.375  AS `Group4`,
       greatest(`TP`.`MaxPoints`,
                coalesce(`TP`.`BasePoints` + `TP`.`CompletionPoints` * `Map`.`Completions` / `TP`.`Divider`,
                         `TP`.`MaxPoints`)) * 0.25 / 5.0625 AS `Group5`
from ((`MapCompletions` `Map` join `MapTiers` `MT`
       on (`Map`.`MapName` = `MT`.`MapName`)) join `TierPoints` `TP` on (`MT`.`Tier` = `TP`.`Tier`));

CREATE VIEW IF NOT EXISTS PlayerPoints AS
select `Ranks`.`SteamID`                                      AS `SteamID`,
       `MT`.`MapName`                                         AS `MapName`,
       `Ranks`.`Percentile`                                   AS `Percentile`,
       case
           when `Ranks`.`Percentile` < 0.5 then case `Ranks`.`Rank`
                                                    when 1 then `MWR`.`WR`
                                                    when 2 then `MWR`.`Rank2`
                                                    when 3 then `MWR`.`Rank3`
                                                    when 4 then `MWR`.`Rank4`
                                                    when 5 then `MWR`.`Rank5`
                                                    when 6 then `MWR`.`Rank6`
                                                    when 7 then `MWR`.`Rank7`
                                                    when 8 then `MWR`.`Rank8`
                                                    when 9 then `MWR`.`Rank9`
                                                    when 10 then `MWR`.`Rank10` end
           else case
                    when `Ranks`.`Percentile` < 0.03125 then `MWR`.`Group1`
                    when `Ranks`.`Percentile` < 0.06250 then `MWR`.`Group2`
                    when `Ranks`.`Percentile` < 0.12500 then `MWR`.`Group3`
                    when `Ranks`.`Percentile` < 0.25000 then `MWR`.`Group4`
                    when `Ranks`.`Percentile` < 0.50000 then `MWR`.`Group5`
                    else 0 end end + case `MT`.`Tier`
                                         when 1 then 25
                                         when 2 then 50
                                         when 3 then 100
                                         when 4 then 200
                                         when 5 then 400
                                         when 6 then 600
                                         when 7 then 800
                                         when 8 then 1000 end AS `Points`
from ((`PlayerRanks` `Ranks` join `MapWRs` `MWR`
       on (`Ranks`.`MapName` = `MWR`.`MapName`)) join `MapTiers` `MT` on (`MWR`.`MapName` = `MT`.`MapName`));

CREATE VIEW IF NOT EXISTS PlayerRanks AS
select `PlayerRecords`.`MapName`                                                                           AS `MapName`,
       `PlayerRecords`.`SteamID`                                                                           AS `SteamID`,
       rank() over ( partition by `PlayerRecords`.`MapName` order by `PlayerRecords`.`TimerTicks`)         AS `Rank`,
       percent_rank() over ( partition by `PlayerRecords`.`MapName` order by `PlayerRecords`.`TimerTicks`) AS `Percentile`
from `PlayerRecords`
where `PlayerRecords`.`Style` = 0;

CREATE VIEW IF NOT EXISTS PlayerLeaderboard AS
select `PP`.`SteamID` AS `SteamID`, `PR`.`PlayerName` AS `PlayerName`, sum(`PP`.`Points`) AS `GlobalPoints`
from (`PlayerPoints` `PP` join `PlayerRecords` `PR` on (`PP`.`SteamID` = `PR`.`SteamID`))
group by `PP`.`SteamID`;


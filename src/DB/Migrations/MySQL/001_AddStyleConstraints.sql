ALTER TABLE PlayerRecords DROP PRIMARY KEY;
ALTER TABLE PlayerRecords
    ADD CONSTRAINT pk_Records PRIMARY KEY (MapName, SteamID, Style);
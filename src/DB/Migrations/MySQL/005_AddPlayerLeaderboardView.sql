CREATE TABLE IF NOT EXISTS TierPoints
(
    Tier             INT   NOT NULL
        PRIMARY KEY,
    Points           INT   NULL,
    CompletionPoints FLOAT NULL,
    BasePoints       FLOAT NULL,
    Divider          FLOAT NULL,
    MaxPoints        INT   NULL
);


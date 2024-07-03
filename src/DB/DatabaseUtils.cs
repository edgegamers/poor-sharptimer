/*
Copyright (C) 2024 Dea Brcka

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Text.Json;
using MySqlConnector;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Data;
using Npgsql;
using System.Data.Common;

namespace SharpTimer
{
    public enum DatabaseType
    {
        MySQL,
        PostgreSQL
    }
    partial class SharpTimer
    {
        private async Task<IDbConnection> OpenConnectionAsync()
        {
            IDbConnection connection = null;
            switch (dbType)
            {
                case DatabaseType.MySQL:
                    connection = new MySqlConnection(await GetConnectionStringFromConfigFile());
                    await (connection as MySqlConnection).OpenAsync();
                    break;
                case DatabaseType.PostgreSQL:
                    connection = new NpgsqlConnection(await GetConnectionStringFromConfigFile());
                    await (connection as NpgsqlConnection).OpenAsync();
                    break;
            }
            if (connection.State != ConnectionState.Open)
            {
                useMySQL = false;
                usePostgres = false;
            }
            return connection;
        }
        private async Task<string> GetConnectionStringFromConfigFile()
        {
            try
            {
                using (JsonDocument? jsonConfig = await LoadJson(dbPath)!)
                {
                    if (jsonConfig != null)
                    {
                        JsonElement root = jsonConfig.RootElement;

                        string host = root.TryGetProperty("Host", out var hostProperty) ? hostProperty.GetString()! : "localhost";
                        string database = root.TryGetProperty("Database", out var databaseProperty) ? databaseProperty.GetString()! : "database";
                        string username = root.TryGetProperty("Username", out var usernameProperty) ? usernameProperty.GetString()! : "root";
                        string password = root.TryGetProperty("Password", out var passwordProperty) ? passwordProperty.GetString()! : "root";
                        int port = root.TryGetProperty("Port", out var portProperty) ? portProperty.GetInt32()! : 3306;
                        string tableprefix = root.TryGetProperty("TablePrefix", out var tableprefixProperty) ? tableprefixProperty.GetString()! : "";

                        PlayerStatsTable = $"{(tableprefix != "" ? $"PlayerStats_{tableprefix}" : "PlayerStats")}";

                        if (dbType.Equals(DatabaseType.MySQL))
                        {
                            int timeout = root.TryGetProperty("Timeout", out var timeoutProperty) ? timeoutProperty.GetInt32()! : 30;
                            return $"Server={host};Database={database};User ID={username};Password={password};Port={port};CharSet=utf8mb4;Connection Timeout={timeout};";
                        }
                        else if (dbType.Equals(DatabaseType.PostgreSQL))
                        {
                            return $"Server={host};Database={database};User ID={username};Password={password};Port={port};SslMode=Disable";
                        }
                        else
                        {
                            SharpTimerError($"Database type not supported");
                        }
                    }
                    else
                    {
                        SharpTimerError($"Database json was null");
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in GetConnectionString: {ex.Message}");
            }
            return "Server=localhost;Database=database;User ID=root;Password=root;Port=3306;";
        }

        private async Task CheckTablesAsync()
        {
            string[] playerRecords;
            string[] playerStats;

            switch (dbType)
            {
                case DatabaseType.MySQL:
                    playerRecords = [       "MapName VARCHAR(255) DEFAULT ''",
                                                    "SteamID VARCHAR(20) DEFAULT ''",
                                                    "PlayerName VARCHAR(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT ''",
                                                    "TimerTicks INT DEFAULT 0",
                                                    "FormattedTime VARCHAR(255) DEFAULT ''",
                                                    "UnixStamp INT DEFAULT 0",
                                                    "LastFinished INT DEFAULT 0",
                                                    "TimesFinished INT DEFAULT 0",
                                                    "Style INT DEFAULT 0"
                                                ];
                    playerStats = [         "SteamID VARCHAR(20) DEFAULT ''",
                                                    "PlayerName VARCHAR(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT ''",
                                                    "TimesConnected INT DEFAULT 0",
                                                    "LastConnected INT DEFAULT 0",
                                                    "GlobalPoints INT DEFAULT 0",
                                                    "HideTimerHud BOOL DEFAULT false",
                                                    "HideKeys BOOL DEFAULT false",
                                                    "HideJS BOOL DEFAULT false",
                                                    "SoundsEnabled BOOL DEFAULT false",
                                                    "PlayerFov INT DEFAULT 0",
                                                    "IsVip BOOL DEFAULT false",
                                                    "BigGifID VARCHAR(16) DEFAULT 'x'"
                                                ];
                    break;
                case DatabaseType.PostgreSQL:
                    playerRecords = [       @"""MapName"" VARCHAR(255) DEFAULT ''",
                                                    @"""SteamID"" VARCHAR(20) DEFAULT ''",
                                                    @"""PlayerName"" VARCHAR(32) DEFAULT ''",
                                                    @"""TimerTicks"" INT DEFAULT 0",
                                                    @"""FormattedTime"" VARCHAR(255) DEFAULT ''",
                                                    @"""UnixStamp"" INT DEFAULT 0",
                                                    @"""LastFinished"" INT DEFAULT 0",
                                                    @"""TimesFinished"" INT DEFAULT 0",
                                                    @"""Style"" INT DEFAULT 0"
                                                ];
                    playerStats = [         @"""SteamID"" VARCHAR(20) DEFAULT ''",
                                                    @"""PlayerName"" VARCHAR(32) DEFAULT ''",
                                                    @"""TimesConnected"" INT DEFAULT 0",
                                                    @"""LastConnected"" INT DEFAULT 0",
                                                    @"""GlobalPoints"" INT DEFAULT 0",
                                                    @"""HideTimerHud"" BOOL DEFAULT false",
                                                    @"""HideKeys"" BOOL DEFAULT false",
                                                    @"""HideJS"" BOOL DEFAULT false",
                                                    @"""SoundsEnabled"" BOOL DEFAULT false",
                                                    @"""PlayerFov"" INT DEFAULT 0",
                                                    @"""IsVip"" BOOL DEFAULT false",
                                                    @"""BigGifID"" VARCHAR(16) DEFAULT 'x'"
                                                ];
                    break;
                default:
                    playerRecords = null;
                    playerStats = null;
                    SharpTimerError($"Database type not supported");
                    break;
            }
            using (var connection = await OpenConnectionAsync())
            {
                try
                {
                    // Check PlayerRecords
                    SharpTimerDebug($"Checking PlayerRecords Table...");
                    await CreatePlayerRecordsTableAsync(connection);
                    await UpdateTableColumnsAsync(connection, "PlayerRecords", playerRecords);
                    await AddConstraintsToRecordsTableAsync(connection, "PlayerRecords");

                    // Check PlayerStats
                    SharpTimerDebug($"Checking PlayerStats Table...");
                    await CreatePlayerStatsTableAsync(connection);
                    await UpdateTableColumnsAsync(connection, $"{PlayerStatsTable}", playerStats);
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error in CheckTablesAsync: {ex}");
                }
            }
        }
        private async Task CreatePlayerRecordsTableAsync(IDbConnection connection)
        {
            DbCommand createTableCommand;
            string createTableQuery;
            switch (dbType)
            {
                case DatabaseType.MySQL:
                    createTableQuery = @"CREATE TABLE IF NOT EXISTS PlayerRecords (
                                            MapName VARCHAR(255),
                                            SteamID VARCHAR(20),
                                            PlayerName VARCHAR(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
                                            TimerTicks INT,
                                            FormattedTime VARCHAR(255),
                                            UnixStamp INT,
                                            TimesFinished INT,
                                            LastFinished INT,
                                            Style INT,
                                            PRIMARY KEY (MapName, SteamID, Style)
                                        )";
                    createTableCommand = new MySqlCommand(createTableQuery, (MySqlConnection)connection);
                    break;
                case DatabaseType.PostgreSQL:
                    createTableQuery = @"CREATE TABLE IF NOT EXISTS ""PlayerRecords"" (
                                            ""MapName"" VARCHAR(255),
                                            ""SteamID"" VARCHAR(20),
                                            ""PlayerName"" VARCHAR(32),
                                            ""TimerTicks"" INT,
                                            ""FormattedTime"" VARCHAR(255),
                                            ""UnixStamp"" INT,
                                            ""TimesFinished"" INT,
                                            ""LastFinished"" INT,
                                            ""Style"" INT,
                                            PRIMARY KEY (""MapName"", ""SteamID"", ""Style"")
                                        )";
                    createTableCommand = new NpgsqlCommand(createTableQuery, (NpgsqlConnection)connection);
                    break;
                default:
                    createTableCommand = null;
                    break;
            }
            using (createTableCommand)
            {
                try
                {
                    await createTableCommand.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error in CreatePlayerRecordsTableAsync: {ex.Message}");
                }
            }
        }
        private async Task UpdateTableColumnsAsync(IDbConnection connection, string tableName, string[] columns)
        {
            if (await TableExistsAsync(connection, tableName))
            {
                foreach (string columnDefinition in columns)
                {
                    string columnName = columnDefinition.Split(' ')[0];
                    if (!await ColumnExistsAsync(connection, tableName, columnName))
                    {
                        SharpTimerDebug($"Adding column {columnName} to {tableName}...");
                        await AddColumnToTableAsync(connection, tableName, columnDefinition);
                    }
                }
            }
        }
        private async Task<bool> TableExistsAsync(IDbConnection connection, string tableName)
        {
            DbCommand command;
            string query;
            switch (dbType)
            {
                case DatabaseType.MySQL:
                    query = $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '{connection.Database}' AND table_name = '{tableName}'";
                    command = new MySqlCommand(query, (MySqlConnection)connection);
                    break;
                case DatabaseType.PostgreSQL:
                    query = $@"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '""{tableName}""'";
                    command = new NpgsqlCommand(query, (NpgsqlConnection)connection);
                    break;
                default:
                    command = null;
                    break;
            }
            using (command)
            {
                try
                {
                    int count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error in TableExistsAsync: {ex.Message}");
                    return false;
                }
            }
        }
        private async Task<bool> ColumnExistsAsync(IDbConnection connection, string tableName, string columnName)
        {
            DbCommand command;
            string query;
            switch (dbType)
            {
                case DatabaseType.MySQL:
                    query = $"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = '{connection.Database}' AND table_name = '{tableName}' AND column_name = '{columnName}'";
                    command = new MySqlCommand(query, (MySqlConnection)connection);
                    break;
                case DatabaseType.PostgreSQL:
                    query = query = $@"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '{tableName}' AND column_name = '{columnName}'";
                    command = new NpgsqlCommand(query, (NpgsqlConnection)connection);
                    break;
                default:
                    command = null;
                    break;
            }
            using (command)
            {
                try
                {
                    int count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error in ColumnExistsAsync: {ex.Message}");
                    return false;
                }
            }
        }
        private async Task AddColumnToTableAsync(IDbConnection connection, string tableName, string columnDefinition)
        {
            DbCommand command;
            string query;
            switch (dbType)
            {
                case DatabaseType.MySQL:
                    query = $"ALTER TABLE {tableName} ADD COLUMN {columnDefinition}";
                    command = new MySqlCommand(query, (MySqlConnection)connection);
                    break;
                case DatabaseType.PostgreSQL:
                    query = $@"ALTER TABLE ""{tableName}"" ADD ""{columnDefinition}""";
                    command = new NpgsqlCommand(query, (NpgsqlConnection)connection);
                    break;
                default:
                    command = null;
                    break;
            }
            using (command)
            {
                try
                {
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error in AddColumnToTableAsync: {ex.Message}");
                }
            }
        }
        private async Task AddConstraintsToRecordsTableAsync(IDbConnection connection, string tableName)
        {
            DbCommand dropCommand;
            DbCommand command;
            string dropQuery;
            string query;
            switch (dbType)
            {
                case DatabaseType.MySQL:
                    dropQuery = $"ALTER TABLE {tableName} DROP PRIMARY KEY";
                    query = $"ALTER TABLE {tableName} ADD CONSTRAINT pk_Records PRIMARY KEY (MapName, SteamID, Style)";
                    dropCommand = new MySqlCommand(dropQuery, (MySqlConnection)connection);
                    command = new MySqlCommand(query, (MySqlConnection)connection);
                    break;
                case DatabaseType.PostgreSQL:
                    dropQuery = $"ALTER TABLE {tableName} DROP CONSTRAINT pk_Records";
                    query = $@"ALTER TABLE ""{tableName}"" ADD CONSTRAINT pk_Records PRIMARY KEY (""MapName"", ""SteamID"", ""Style"")";
                    dropCommand = new NpgsqlCommand(dropQuery, (NpgsqlConnection)connection);
                    command = new NpgsqlCommand(query, (NpgsqlConnection)connection);
                    break;
                default:
                    dropCommand = null;
                    command = null;
                    break;
            }
            using (dropCommand)
            {
                try
                {
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error removing previous constraints: {ex.Message}");
                }
            }
            using (command)
            {
                try
                {
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error setting constraints: {ex.Message}");
                }
            }
        }
        private async Task CreatePlayerStatsTableAsync(IDbConnection connection)
        {
            DbCommand command;
            string query;
            switch (dbType)
            {
                case DatabaseType.MySQL:
                    query = $@"CREATE TABLE IF NOT EXISTS {PlayerStatsTable} (
                                            SteamID VARCHAR(20),
                                            PlayerName VARCHAR(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
                                            TimesConnected INT,
                                            LastConnected INT,
                                            GlobalPoints INT,
                                            HideTimerHud BOOL,
                                            HideKeys BOOL,
                                            HideJS BOOL,
                                            SoundsEnabled BOOL,
                                            PlayerFov INT,
                                            IsVip BOOL,
                                            BigGifID VARCHAR(16),
                                            PRIMARY KEY (SteamID)
                                        )";
                    command = new MySqlCommand(query, (MySqlConnection)connection);
                    break;
                case DatabaseType.PostgreSQL:
                    query = $@"CREATE TABLE IF NOT EXISTS ""{PlayerStatsTable}"" (
                                            ""SteamID"" VARCHAR(20) UNIQUE,
                                            ""PlayerName"" VARCHAR(32),
                                            ""TimesConnected"" INT,
                                            ""LastConnected"" INT,
                                            ""GlobalPoints"" INT,
                                            ""HideTimerHud"" BOOL,
                                            ""HideKeys"" BOOL,
                                            ""HideJS"" BOOL,
                                            ""SoundsEnabled"" BOOL,
                                            ""PlayerFov"" INT,
                                            ""IsVip"" BOOL,
                                            ""BigGifID"" VARCHAR(16),
                                            PRIMARY KEY (""SteamID"")
                                        )";
                    command = new NpgsqlCommand(query, (NpgsqlConnection)connection);
                    break;
                default:
                    command = null;
                    break;
            }
            using (command)
            {
                try
                {
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error in CreatePlayerStatsTableAsync: {ex.Message}");
                }
            }
        }
        public async Task SavePlayerTimeToDatabase(CCSPlayerController? player, int timerTicks, string steamId, string playerName, int playerSlot, int bonusX = 0, int style = 0)
        {
            SharpTimerDebug($"Trying to save player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} to database for {playerName} {timerTicks}");
            try
            {
                if (!IsAllowedPlayer(player)) return;
                //if ((bonusX == 0 && !playerTimers[playerSlot].IsTimerRunning) || (bonusX != 0 && !playerTimers[playerSlot].IsBonusTimerRunning)) return;
                string currentMapNamee = bonusX == 0 ? currentMapName! : $"{currentMapName}_bonus{bonusX}";

                int timeNowUnix = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                // get player columns
                int dBtimesFinished = 0;
                int dBlastFinished = 0;
                int dBunixStamp = 0;
                int dBtimerTicks = 0;
                string dBFormattedTime;

                // store new value separatley
                int new_dBtimerTicks = 0;
                int playerPoints = 0;
                bool beatPB = false;

                using (var connection = await OpenConnectionAsync())
                {
                    await CreatePlayerRecordsTableAsync(connection);

                    string formattedTime = FormatTime(timerTicks);
                    string selectQuery;
                    DbCommand selectCommand;
                    switch (dbType)
                    {
                        case DatabaseType.MySQL:
                            selectQuery = @"SELECT TimesFinished, LastFinished, FormattedTime, TimerTicks, UnixStamp FROM PlayerRecords WHERE MapName = @MapName AND SteamID = @SteamID AND Style = @Style";
                            selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                            break;
                        case DatabaseType.PostgreSQL:
                            selectQuery = @"SELECT ""TimesFinished"", ""LastFinished"", ""FormattedTime"", ""TimerTicks"", ""UnixStamp"" FROM ""PlayerRecords"" WHERE ""MapName"" = @MapName AND ""SteamID"" = @SteamID AND ""Style"" = @Style";
                            selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                            break;
                        default:
                            selectQuery = null;
                            selectCommand = null;
                            break;
                    }
                    // Check if the record already exists or has a higher timer value
                    selectCommand.AddParameterWithValue("@MapName", currentMapNamee);
                    selectCommand.AddParameterWithValue("@SteamID", steamId);
                    selectCommand.AddParameterWithValue("@Style", style);

                    var row = await selectCommand.ExecuteReaderAsync();

                    if (row.Read())
                    {
                        // get player columns
                        dBtimesFinished = row.GetInt32("TimesFinished");
                        dBlastFinished = row.GetInt32("LastFinished");
                        dBunixStamp = row.GetInt32("UnixStamp");
                        dBtimerTicks = row.GetInt32("TimerTicks");
                        dBFormattedTime = row.GetString("FormattedTime");

                        // Modify the stats
                        dBtimesFinished++;
                        dBlastFinished = timeNowUnix;
                        if (timerTicks < dBtimerTicks)
                        {
                            new_dBtimerTicks = timerTicks;
                            dBunixStamp = timeNowUnix;
                            dBFormattedTime = formattedTime;
                            playerPoints = timerTicks;
                            beatPB = true;
                            if (playerPoints < 32)
                            {
                                beatPB = false;
                                playerPoints = 320000;
                            }
                            if (enableReplays == true && (usePostgres || useMySQL)) _ = Task.Run(async () => await DumpReplayToJson(player!, steamId, playerSlot, bonusX, playerTimers[playerSlot].currentStyle));
                        }
                        else
                        {
                            new_dBtimerTicks = dBtimerTicks;
                            beatPB = false;
                            playerPoints = 320000;
                        }

                        await row.CloseAsync();
                        // Update or insert the record
                        string upsertQuery;
                        DbCommand upsertCommand;
                        switch (dbType)
                        {
                            case DatabaseType.MySQL:
                                upsertQuery = @"
                                                    INSERT INTO PlayerRecords 
                                                    (MapName, SteamID, PlayerName, TimerTicks, LastFinished, TimesFinished, FormattedTime, UnixStamp, Style)
                                                    VALUES 
                                                    (@MapName, @SteamID, @PlayerName, @TimerTicks, @LastFinished, @TimesFinished, @FormattedTime, @UnixStamp, @Style)
                                                    ON DUPLICATE KEY UPDATE
                                                    MapName = VALUES(MapName),
                                                    PlayerName = VALUES(PlayerName),
                                                    TimerTicks = VALUES(TimerTicks),
                                                    LastFinished = VALUES(LastFinished),
                                                    TimesFinished = VALUES(TimesFinished),
                                                    FormattedTime = VALUES(FormattedTime),
                                                    UnixStamp = VALUES(UnixStamp),
                                                    Style = VALUES(Style);
                                                    ";
                                upsertCommand = new MySqlCommand(upsertQuery, (MySqlConnection)connection);
                                break;
                            case DatabaseType.PostgreSQL:
                                upsertQuery = @"
                                                    INSERT INTO ""PlayerRecords"" 
                                                    (""MapName"", ""SteamID"", ""PlayerName"", ""TimerTicks"", ""LastFinished"", ""TimesFinished"", ""FormattedTime"", ""UnixStamp"", ""Style"")
                                                    VALUES 
                                                    (@MapName, @SteamID, @PlayerName, @TimerTicks, @LastFinished, @TimesFinished, @FormattedTime, @UnixStamp, @Style)
                                                    ON CONFLICT (""MapName"", ""SteamID"", ""Style"")
                                                    DO UPDATE SET
                                                    ""MapName"" = EXCLUDED.""MapName"",
                                                    ""PlayerName"" = EXCLUDED.""PlayerName"",
                                                    ""TimerTicks"" = EXCLUDED.""TimerTicks"",
                                                    ""LastFinished"" = EXCLUDED.""LastFinished"",
                                                    ""TimesFinished"" = EXCLUDED.""TimesFinished"",
                                                    ""FormattedTime"" = EXCLUDED.""FormattedTime"",
                                                    ""UnixStamp"" = EXCLUDED.""UnixStamp"",
                                                    ""Style"" = EXCLUDED.""Style"";
                                                    ";
                                upsertCommand = new NpgsqlCommand(upsertQuery, (NpgsqlConnection)connection);
                                break;
                            default:
                                upsertQuery = null;
                                upsertCommand = null;
                                break;
                        }
                        using (upsertCommand)
                        {
                            upsertCommand.AddParameterWithValue("@MapName", currentMapNamee);
                            upsertCommand.AddParameterWithValue("@PlayerName", playerName);
                            upsertCommand.AddParameterWithValue("@TimesFinished", dBtimesFinished);
                            upsertCommand.AddParameterWithValue("@LastFinished", dBlastFinished);
                            upsertCommand.AddParameterWithValue("@TimerTicks", new_dBtimerTicks);
                            upsertCommand.AddParameterWithValue("@FormattedTime", dBFormattedTime);
                            upsertCommand.AddParameterWithValue("@UnixStamp", dBunixStamp);
                            upsertCommand.AddParameterWithValue("@SteamID", steamId);
                            upsertCommand.AddParameterWithValue("@Style", style);
                            if ((usePostgres || useMySQL) && globalRanksEnabled == true && ((dBtimesFinished <= maxGlobalFreePoints && globalRanksFreePointsEnabled == true) || beatPB)) await SavePlayerPoints(steamId, playerName, playerSlot, playerPoints, dBtimerTicks, beatPB, bonusX, style);
                            if ((stageTriggerCount != 0 || cpTriggerCount != 0) && bonusX == 0 && (usePostgres || useMySQL) && timerTicks < dBtimerTicks) Server.NextFrame(() => _ = Task.Run(async () => await DumpPlayerStageTimesToJson(player, steamId, playerSlot)));
                            await upsertCommand.ExecuteNonQueryAsync();
                            Server.NextFrame(() => SharpTimerDebug($"Saved player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} to database for {playerName} {timerTicks} {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"));
                            if ((usePostgres || useMySQL) && IsAllowedPlayer(player)) await RankCommandHandler(player, steamId, playerSlot, playerName, true, style);
                            if (IsAllowedPlayer(player)) Server.NextFrame(() => _ = Task.Run(async () => await PrintMapTimeToChat(player!, steamId, playerName, dBtimerTicks, timerTicks, bonusX, dBtimesFinished, style)));
                        }

                    }
                    else
                    {
                        Server.NextFrame(() => SharpTimerDebug($"No player record yet"));
                        if (enableReplays == true && usePostgres == true) _ = Task.Run(async () => await DumpReplayToJson(player!, steamId, playerSlot, bonusX, playerTimers[playerSlot].currentStyle));
                        await row.CloseAsync();

                        string upsertQuery;
                        DbCommand upsertCommand;
                        switch (dbType)
                        {
                            case DatabaseType.MySQL:
                                upsertQuery = @"REPLACE INTO PlayerRecords (MapName, SteamID, PlayerName, TimerTicks, LastFinished, TimesFinished, FormattedTime, UnixStamp, Style) VALUES (@MapName, @SteamID, @PlayerName, @TimerTicks, @LastFinished, @TimesFinished, @FormattedTime, @UnixStamp, @Style)";
                                upsertCommand = new MySqlCommand(upsertQuery, (MySqlConnection)connection);
                                break;
                            case DatabaseType.PostgreSQL:
                                upsertQuery = @"INSERT INTO ""PlayerRecords"" (""MapName"", ""SteamID"", ""PlayerName"", ""TimerTicks"", ""LastFinished"", ""TimesFinished"", ""FormattedTime"", ""UnixStamp"", ""Style"") VALUES (@MapName, @SteamID, @PlayerName, @TimerTicks, @LastFinished, @TimesFinished, @FormattedTime, @UnixStamp, @Style)";
                                upsertCommand = new NpgsqlCommand(upsertQuery, (NpgsqlConnection)connection);
                                break;
                            default:
                                upsertQuery = null;
                                upsertCommand = null;
                                break;
                        }

                        using (upsertCommand)
                        {
                            upsertCommand.AddParameterWithValue("@MapName", currentMapNamee);
                            upsertCommand.AddParameterWithValue("@PlayerName", playerName);
                            upsertCommand.AddParameterWithValue("@TimesFinished", 1);
                            upsertCommand.AddParameterWithValue("@LastFinished", timeNowUnix);
                            upsertCommand.AddParameterWithValue("@TimerTicks", timerTicks);
                            upsertCommand.AddParameterWithValue("@FormattedTime", formattedTime);
                            upsertCommand.AddParameterWithValue("@UnixStamp", timeNowUnix);
                            upsertCommand.AddParameterWithValue("@SteamID", steamId);
                            upsertCommand.AddParameterWithValue("@Style", style);
                            await upsertCommand.ExecuteNonQueryAsync();
                            if (globalRanksEnabled == true) await SavePlayerPoints(steamId, playerName, playerSlot, timerTicks, dBtimerTicks, beatPB, bonusX, style);
                            if ((stageTriggerCount != 0 || cpTriggerCount != 0) && bonusX == 0) Server.NextFrame(() => _ = Task.Run(async () => await DumpPlayerStageTimesToJson(player, steamId, playerSlot)));
                            Server.NextFrame(() => SharpTimerDebug($"Saved player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} to database for {playerName} {timerTicks} {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"));
                            if (IsAllowedPlayer(player)) await RankCommandHandler(player, steamId, playerSlot, playerName, true, style);
                            if (IsAllowedPlayer(player)) Server.NextFrame(() => _ = Task.Run(async () => await PrintMapTimeToChat(player!, steamId, playerName, dBtimerTicks, timerTicks, bonusX, 1, style)));
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => SharpTimerError($"Error saving player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} to database: {ex.Message}"));
            }
        }

        public async Task GetPlayerStats(CCSPlayerController? player, string steamId, string playerName, int playerSlot, bool fromConnect)
        {
            SharpTimerDebug($"Trying to get player stats from database for {playerName}");
            try
            {
                if (player == null || !player.IsValid || player.IsBot) return;
                if (!(connectedPlayers.ContainsKey(playerSlot) && playerTimers.ContainsKey(playerSlot))) return;

                int timeNowUnix = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                // get player columns
                int timesConnected = 0;
                int lastConnected = 0;
                bool hideTimerHud;
                bool hideKeys;
                bool hideJS;
                bool soundsEnabled;
                int playerFov = 0;
                bool isVip;
                string bigGif;
                int playerPoints;

                using (var connection = await OpenConnectionAsync())
                {
                    await CreatePlayerRecordsTableAsync(connection);

                    string selectQuery;
                    DbCommand selectCommand;
                    switch (dbType)
                    {
                        case DatabaseType.MySQL:
                            selectQuery = @"SELECT PlayerName, TimesConnected, LastConnected, HideTimerHud, HideKeys, HideJS, SoundsEnabled, PlayerFov, IsVip, BigGifID, GlobalPoints FROM PlayerStats WHERE SteamID = @SteamID";
                            selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                            break;
                        case DatabaseType.PostgreSQL:
                            selectQuery = @"SELECT ""PlayerName"", ""TimesConnected"", ""LastConnected"", ""HideTimerHud"", ""HideKeys"", ""HideJS"", ""SoundsEnabled"", ""PlayerFov"", ""IsVip"", ""BigGifID"", ""GlobalPoints"" FROM ""PlayerStats"" WHERE ""SteamID"" = @SteamID";
                            selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                            break;
                        default:
                            selectQuery = null;
                            selectCommand = null;
                            break;
                    }

                    using (selectCommand)
                    {
                        selectCommand.AddParameterWithValue("@SteamID", steamId);

                        var row = await selectCommand.ExecuteReaderAsync();

                        if (row.Read())
                        {
                            // get player columns
                            timesConnected = row.GetInt32("TimesConnected");
                            hideTimerHud = row.GetBoolean("HideTimerHud");
                            hideKeys = row.GetBoolean("HideKeys");
                            hideJS = row.GetBoolean("HideJS");
                            soundsEnabled = row.GetBoolean("SoundsEnabled");
                            playerFov = row.GetInt32("PlayerFov");
                            isVip = row.GetBoolean("IsVip");
                            bigGif = row.GetString("BigGifID");
                            playerPoints = row.GetInt32("GlobalPoints");

                            // Modify the stats
                            timesConnected++;
                            lastConnected = timeNowUnix;
                            Server.NextFrame(() =>
                            {
                                if (playerTimers.TryGetValue(playerSlot, out PlayerTimerInfo? value))
                                {
                                    value.HideTimerHud = hideTimerHud;
                                    value.HideKeys = hideKeys;
                                    value.HideJumpStats = hideJS;
                                    value.SoundsEnabled = soundsEnabled;
                                    value.PlayerFov = playerFov;
                                    value.IsVip = isVip;
                                    value.VipBigGif = bigGif;
                                    value.TimesConnected = timesConnected;
                                }
                                else
                                {
                                    SharpTimerError($"Error getting player stats from database for {playerName}: player was not on the server anymore");
                                    return;
                                }
                            });

                            await row.CloseAsync();
                            // Update or insert the record

                            string upsertQuery;
                            DbCommand upsertCommand;
                            switch (dbType)
                            {
                                case DatabaseType.MySQL:
                                    upsertQuery = @"REPLACE INTO PlayerStats (PlayerName, SteamID, TimesConnected, LastConnected, HideTimerHud, HideKeys, HideJS, SoundsEnabled, PlayerFov, IsVip, BigGifID, GlobalPoints) 
                                                        VALUES (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)";
                                    upsertCommand = new MySqlCommand(upsertQuery, (MySqlConnection)connection);
                                    break;
                                case DatabaseType.PostgreSQL:
                                    upsertQuery = @"
                                                    INSERT INTO ""PlayerStats"" 
                                                    (""PlayerName"", ""SteamID"", ""TimesConnected"", ""LastConnected"", ""HideTimerHud"", ""HideKeys"", ""HideJS"", ""SoundsEnabled"", ""PlayerFov"", ""IsVip"", ""BigGifID"", ""GlobalPoints"")
                                                    VALUES 
                                                    (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)
                                                    ON CONFLICT (""SteamID"")
                                                    DO UPDATE SET
                                                    ""PlayerName"" = EXCLUDED.""PlayerName"",
                                                    ""TimesConnected"" = EXCLUDED.""TimesConnected"",
                                                    ""LastConnected"" = EXCLUDED.""LastConnected"",
                                                    ""HideTimerHud"" = EXCLUDED.""HideTimerHud"",
                                                    ""HideKeys"" = EXCLUDED.""HideKeys"",
                                                    ""HideJS"" = EXCLUDED.""HideJS"",
                                                    ""SoundsEnabled"" = EXCLUDED.""SoundsEnabled"",
                                                    ""PlayerFov"" = EXCLUDED.""PlayerFov"",
                                                    ""IsVip"" = EXCLUDED.""IsVip"",
                                                    ""BigGifID"" = EXCLUDED.""BigGifID"",
                                                    ""GlobalPoints"" = EXCLUDED.""GlobalPoints"";
                                                    ";
                                    upsertCommand = new NpgsqlCommand(upsertQuery, (NpgsqlConnection)connection);
                                    break;
                                default:
                                    upsertQuery = null;
                                    upsertCommand = null;
                                    break;
                            }

                            using (upsertCommand)
                            {
                                upsertCommand.AddParameterWithValue("@PlayerName", playerName);
                                upsertCommand.AddParameterWithValue("@SteamID", steamId);
                                upsertCommand.AddParameterWithValue("@TimesConnected", timesConnected);
                                upsertCommand.AddParameterWithValue("@LastConnected", lastConnected);
                                upsertCommand.AddParameterWithValue("@HideTimerHud", hideTimerHud);
                                upsertCommand.AddParameterWithValue("@HideKeys", hideKeys);
                                upsertCommand.AddParameterWithValue("@HideJS", hideJS);
                                upsertCommand.AddParameterWithValue("@SoundsEnabled", soundsEnabled);
                                upsertCommand.AddParameterWithValue("@PlayerFov", playerFov);
                                upsertCommand.AddParameterWithValue("@IsVip", isVip);
                                upsertCommand.AddParameterWithValue("@BigGifID", bigGif);
                                upsertCommand.AddParameterWithValue("@GlobalPoints", playerPoints);

                                await upsertCommand.ExecuteNonQueryAsync();
                                Server.NextFrame(() => SharpTimerDebug($"Got player stats from database for {playerName}"));
                                if (connectMsgEnabled) Server.NextFrame(() => Server.PrintToChatAll($"{Localizer["prefix"]} {Localizer["connected_message", playerName, FormatOrdinal(timesConnected)]}"));
                            }

                        }
                        else
                        {
                            Server.NextFrame(() => SharpTimerDebug($"No player stats yet"));
                            await row.CloseAsync();

                            string upsertQuery;
                            DbCommand upsertCommand;
                            switch (dbType)
                            {
                                case DatabaseType.MySQL:
                                    upsertQuery = @"REPLACE INTO PlayerStats (PlayerName, SteamID, TimesConnected, LastConnected, HideTimerHud, HideKeys, HideJS, SoundsEnabled, PlayerFov, IsVip, BigGifID, GlobalPoints) VALUES (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)";
                                    upsertCommand = new MySqlCommand(upsertQuery, (MySqlConnection)connection);
                                    break;
                                case DatabaseType.PostgreSQL:
                                    upsertQuery = @"INSERT INTO ""PlayerStats"" (""PlayerName"", ""SteamID"", ""TimesConnected"", ""LastConnected"", ""HideTimerHud"", ""HideKeys"", ""HideJS"", ""SoundsEnabled"", ""PlayerFov"", ""IsVip"", ""BigGifID"", ""GlobalPoints"") VALUES (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)";
                                    upsertCommand = new NpgsqlCommand(upsertQuery, (NpgsqlConnection)connection);
                                    break;
                                default:
                                    upsertQuery = null;
                                    upsertCommand = null;
                                    break;
                            }

                            using (upsertCommand)
                            {
                                upsertCommand.AddParameterWithValue("@PlayerName", playerName);
                                upsertCommand.AddParameterWithValue("@SteamID", steamId);
                                upsertCommand.AddParameterWithValue("@TimesConnected", 1);
                                upsertCommand.AddParameterWithValue("@LastConnected", timeNowUnix);
                                upsertCommand.AddParameterWithValue("@HideTimerHud", false);
                                upsertCommand.AddParameterWithValue("@HideKeys", false);
                                upsertCommand.AddParameterWithValue("@HideJS", false);
                                upsertCommand.AddParameterWithValue("@SoundsEnabled", soundsEnabledByDefault);
                                upsertCommand.AddParameterWithValue("@PlayerFov", 0);
                                upsertCommand.AddParameterWithValue("@IsVip", false);
                                upsertCommand.AddParameterWithValue("@BigGifID", "x");
                                upsertCommand.AddParameterWithValue("@GlobalPoints", 0);

                                await upsertCommand.ExecuteNonQueryAsync();
                                Server.NextFrame(() => SharpTimerDebug($"Got player stats from database for {playerName}"));
                                if (connectMsgEnabled) Server.NextFrame(() => Server.PrintToChatAll($"{Localizer["prefix"]} {Localizer["connected_message_first", playerName]}"));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => SharpTimerError($"Error getting player stats from database for {playerName}: {ex}"));
            }
        }

        public async Task SetPlayerStats(CCSPlayerController? player, string steamId, string playerName, int playerSlot)
        {
            SharpTimerDebug($"Trying to set player stats in database for {playerName}");
            try
            {
                if (!IsAllowedPlayer(player)) return;
                int timeNowUnix = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                // get player columns
                int timesConnected = 0;
                int lastConnected = 0;
                bool hideTimerHud;
                bool hideKeys;
                bool hideJS;
                bool soundsEnabled;
                int playerFov = 0;
                bool isVip;
                string bigGif;
                int playerPoints;

                using (var connection = await OpenConnectionAsync())
                {
                    await CreatePlayerRecordsTableAsync(connection);
                    string selectQuery;
                    DbCommand selectCommand;
                    switch (dbType)
                    {
                        case DatabaseType.MySQL:
                            selectQuery = "SELECT PlayerName, TimesConnected, LastConnected, HideTimerHud, HideKeys, HideJS, SoundsEnabled, PlayerFov, IsVip, BigGifID, GlobalPoints FROM PlayerStats WHERE SteamID = @SteamID";
                            selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                            break;
                        case DatabaseType.PostgreSQL:
                            selectQuery = @"SELECT ""PlayerName"", ""TimesConnected"", ""LastConnected"", ""HideTimerHud"", ""HideKeys"", ""HideJS"", ""SoundsEnabled"", ""PlayerFov"", ""IsVip"", ""BigGifID"", ""GlobalPoints"" FROM ""PlayerStats"" WHERE ""SteamID"" = @SteamID";
                            selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                            break;
                        default:
                            selectQuery = null;
                            selectCommand = null;
                            break;
                    }

                    using (selectCommand)
                    {
                        selectCommand.AddParameterWithValue("@SteamID", steamId);

                        var row = await selectCommand.ExecuteReaderAsync();

                        if (row.Read())
                        {
                            // get player columns
                            timesConnected = row.GetInt32("TimesConnected");
                            lastConnected = row.GetInt32("LastConnected");
                            hideTimerHud = row.GetBoolean("HideTimerHud");
                            hideKeys = row.GetBoolean("HideKeys");
                            hideJS = row.GetBoolean("HideJS");
                            soundsEnabled = row.GetBoolean("SoundsEnabled");
                            playerFov = row.GetInt32("PlayerFov");
                            isVip = row.GetBoolean("IsVip");
                            bigGif = row.GetString("BigGifID");
                            playerPoints = row.GetInt32("GlobalPoints");

                            await row.CloseAsync();
                            // Update or insert the record

                            string upsertQuery;
                            DbCommand upsertCommand;
                            switch (dbType)
                            {
                                case DatabaseType.MySQL:
                                    upsertQuery = "REPLACE INTO PlayerStats (PlayerName, SteamID, TimesConnected, LastConnected, HideTimerHud, HideKeys, HideJS, SoundsEnabled, PlayerFov, IsVip, BigGifID, GlobalPoints) VALUES (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)";
                                    upsertCommand = new MySqlCommand(upsertQuery, (MySqlConnection)connection);
                                    break;
                                case DatabaseType.PostgreSQL:
                                    upsertQuery = @"
                                                    INSERT INTO ""PlayerStats"" 
                                                    (""PlayerName"", ""SteamID"", ""TimesConnected"", ""LastConnected"", ""HideTimerHud"", ""HideKeys"", ""HideJS"", ""SoundsEnabled"", ""PlayerFov"", ""IsVip"", ""BigGifID"", ""GlobalPoints"")
                                                    VALUES 
                                                    (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)
                                                    ON CONFLICT (""SteamID"")
                                                    DO UPDATE SET
                                                    ""PlayerName"" = EXCLUDED.""PlayerName"",
                                                    ""TimesConnected"" = EXCLUDED.""TimesConnected"",
                                                    ""LastConnected"" = EXCLUDED.""LastConnected"",
                                                    ""HideTimerHud"" = EXCLUDED.""HideTimerHud"",
                                                    ""HideKeys"" = EXCLUDED.""HideKeys"",
                                                    ""HideJS"" = EXCLUDED.""HideJS"",
                                                    ""SoundsEnabled"" = EXCLUDED.""SoundsEnabled"",
                                                    ""PlayerFov"" = EXCLUDED.""PlayerFov"",
                                                    ""IsVip"" = EXCLUDED.""IsVip"",
                                                    ""BigGifID"" = EXCLUDED.""BigGifID"",
                                                    ""GlobalPoints"" = EXCLUDED.""GlobalPoints"";
                                                    ";
                                    upsertCommand = new NpgsqlCommand(upsertQuery, (NpgsqlConnection)connection);
                                    break;
                                default:
                                    upsertQuery = null;
                                    upsertCommand = null;
                                    break;
                            }

                            using (upsertCommand)
                            {
                                if (playerTimers.TryGetValue(playerSlot, out PlayerTimerInfo? value))
                                {
                                    upsertCommand.AddParameterWithValue("@PlayerName", playerName);
                                    upsertCommand.AddParameterWithValue("@SteamID", steamId);
                                    upsertCommand.AddParameterWithValue("@TimesConnected", timesConnected);
                                    upsertCommand.AddParameterWithValue("@LastConnected", lastConnected);
                                    upsertCommand.AddParameterWithValue("@HideTimerHud", value.HideTimerHud);
                                    upsertCommand.AddParameterWithValue("@HideKeys", value.HideKeys);
                                    upsertCommand.AddParameterWithValue("@HideJS", value.HideJumpStats);
                                    upsertCommand.AddParameterWithValue("@SoundsEnabled", value.SoundsEnabled);
                                    upsertCommand.AddParameterWithValue("@PlayerFov", value.PlayerFov);
                                    upsertCommand.AddParameterWithValue("@IsVip", isVip);
                                    upsertCommand.AddParameterWithValue("@BigGifID", bigGif);
                                    upsertCommand.AddParameterWithValue("@GlobalPoints", playerPoints);

                                    await upsertCommand.ExecuteNonQueryAsync();
                                    Server.NextFrame(() => SharpTimerDebug($"Set player stats in database for {playerName}"));
                                }
                                else
                                {
                                    SharpTimerError($"Error setting player stats in database for {playerName}: player was not on the server anymore");

                                    return;
                                }
                            }

                        }
                        else
                        {
                            Server.NextFrame(() => SharpTimerDebug($"No player stats yet"));
                            await row.CloseAsync();

                            string upsertQuery;
                            DbCommand upsertCommand;
                            switch (dbType)
                            {
                                case DatabaseType.MySQL:
                                    upsertQuery = "REPLACE INTO PlayerStats (PlayerName, SteamID, TimesConnected, LastConnected, HideTimerHud, HideKeys, HideJS, SoundsEnabled, PlayerFov, IsVip, BigGifID, GlobalPoints) VALUES (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)";
                                    upsertCommand = new MySqlCommand(upsertQuery, (MySqlConnection)connection);
                                    break;
                                case DatabaseType.PostgreSQL:
                                    upsertQuery = @"INSERT INTO ""PlayerStats"" (""PlayerName"", ""SteamID"", ""TimesConnected"", ""LastConnected"", ""HideTimerHud"", ""HideKeys"", ""HideJS"", ""SoundsEnabled"", ""PlayerFov"", ""IsVip"", ""BigGifID"", ""GlobalPoints"") VALUES (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)";
                                    upsertCommand = new NpgsqlCommand(upsertQuery, (NpgsqlConnection)connection);
                                    break;
                                default:
                                    upsertQuery = null;
                                    upsertCommand = null;
                                    break;
                            }

                            using (upsertCommand)
                            {
                                if (playerTimers.TryGetValue(playerSlot, out PlayerTimerInfo? value))
                                {
                                    upsertCommand.AddParameterWithValue("@PlayerName", playerName);
                                    upsertCommand.AddParameterWithValue("@SteamID", steamId);
                                    upsertCommand.AddParameterWithValue("@TimesConnected", 1);
                                    upsertCommand.AddParameterWithValue("@LastConnected", timeNowUnix);
                                    upsertCommand.AddParameterWithValue("@HideTimerHud", playerTimers[playerSlot].HideTimerHud);
                                    upsertCommand.AddParameterWithValue("@HideKeys", playerTimers[playerSlot].HideKeys);
                                    upsertCommand.AddParameterWithValue("@HideJS", playerTimers[playerSlot].HideJumpStats);
                                    upsertCommand.AddParameterWithValue("@SoundsEnabled", playerTimers[playerSlot].SoundsEnabled);
                                    upsertCommand.AddParameterWithValue("@PlayerFov", playerTimers[playerSlot].PlayerFov);
                                    upsertCommand.AddParameterWithValue("@IsVip", false);
                                    upsertCommand.AddParameterWithValue("@BigGifID", "x");
                                    upsertCommand.AddParameterWithValue("@GlobalPoints", 0);

                                    await upsertCommand.ExecuteNonQueryAsync();
                                    Server.NextFrame(() => SharpTimerDebug($"Set player stats in database for {playerName}"));
                                }
                                else
                                {
                                    SharpTimerError($"Error setting player stats in database for {playerName}: player was not on the server anymore");

                                    return;
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => SharpTimerError($"Error setting player stats in database for {playerName}: {ex}"));
            }
        }

        public void GainPointsMessage(string playerName, int newPoints, int playerPoints)
        {
            Server.PrintToChatAll($" {Localizer["prefix"]} {Localizer["gained_points", playerName, Convert.ToInt32(newPoints - playerPoints), newPoints]}");
        }

        public async Task SavePlayerPoints(string steamId, string playerName, int playerSlot, int timerTicks, int oldTicks, bool beatPB = false, int bonusX = 0, int style = 0)
        {
            SharpTimerDebug($"Trying to set player points in database for {playerName}");
            try
            {
                int timeNowUnix = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                // get player columns
                int timesConnected = 0;
                int lastConnected = 0;
                bool hideTimerHud;
                bool hideKeys;
                bool hideJS;
                bool soundsEnabled;
                int playerFov = 0;
                bool isVip;
                string bigGif;
                int playerPoints = 0;
                float mapTier = 0.1f;

                using (var connection = await OpenConnectionAsync())
                {
                    await CreatePlayerRecordsTableAsync(connection);

                    string selectQuery;
                    DbCommand selectCommand;
                    switch (dbType)
                    {
                        case DatabaseType.MySQL:
                            selectQuery = $@"SELECT PlayerName, TimesConnected, LastConnected, HideTimerHud, HideKeys, HideJS, SoundsEnabled, PlayerFov, IsVip, BigGifID, GlobalPoints FROM {PlayerStatsTable} WHERE SteamID = @SteamID";
                            selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                            break;
                        case DatabaseType.PostgreSQL:
                            selectQuery = $@"SELECT ""PlayerName"", ""TimesConnected"", ""LastConnected"", ""HideTimerHud"", ""HideKeys"", ""HideJS"", ""SoundsEnabled"", ""PlayerFov"", ""IsVip"", ""BigGifID"", ""GlobalPoints"" FROM ""{PlayerStatsTable}"" WHERE ""SteamID"" = @SteamID";
                            selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                            break;
                        default:
                            selectQuery = null;
                            selectCommand = null;
                            break;
                    }

                    using (selectCommand)
                    {
                        selectCommand.AddParameterWithValue("@SteamID", steamId);

                        var row = await selectCommand.ExecuteReaderAsync();

                        if (row.Read())
                        {
                            // get player columns
                            timesConnected = row.GetInt32("TimesConnected");
                            lastConnected = row.GetInt32("LastConnected");
                            hideTimerHud = row.GetBoolean("HideTimerHud");
                            hideKeys = row.GetBoolean("HideKeys");
                            hideJS = row.GetBoolean("HideJS");
                            soundsEnabled = row.GetBoolean("SoundsEnabled");
                            playerFov = row.GetInt32("PlayerFov");
                            isVip = row.GetBoolean("IsVip");
                            bigGif = row.GetString("BigGifID");
                            playerPoints = row.GetInt32("GlobalPoints");

                            int newPoints;

                            if (!enableStylePoints && style == 0) newPoints = (int)(beatPB == false ? Convert.ToInt32(CalculatePoints(timerTicks, style)! * globalPointsMultiplier!) + playerPoints
                                                                    : Convert.ToInt32(CalculatePoints(timerTicks, style)! - CalculatePoints(oldTicks, style) * globalPointsMultiplier! + playerPoints + (310 * (bonusX == 0 ? mapTier : mapTier * 0.5))));
                            else if (enableStylePoints) newPoints = (int)(beatPB == false ? Convert.ToInt32(CalculatePoints(timerTicks, style)! * globalPointsMultiplier!) + playerPoints
                                                                    : Convert.ToInt32(CalculatePoints(timerTicks, style)! - CalculatePoints(oldTicks, style) * globalPointsMultiplier! + playerPoints + (310 * (bonusX == 0 ? mapTier : mapTier * 0.5))));
                            else newPoints = playerPoints;

                            await row.CloseAsync();
                            // Update or insert the record

                            string upsertQuery;
                            DbCommand upsertCommand;
                            switch (dbType)
                            {
                                case DatabaseType.MySQL:
                                    upsertQuery = $@"REPLACE INTO {PlayerStatsTable} (PlayerName, SteamID, TimesConnected, LastConnected, HideTimerHud, HideKeys, HideJS, SoundsEnabled, PlayerFov, IsVip, BigGifID, GlobalPoints) VALUES (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)";
                                    upsertCommand = new MySqlCommand(upsertQuery, (MySqlConnection)connection);
                                    break;
                                case DatabaseType.PostgreSQL:
                                    upsertQuery = $@"
                                                    INSERT INTO ""{PlayerStatsTable}"" 
                                                    (""PlayerName"", ""SteamID"", ""TimesConnected"", ""LastConnected"", ""HideTimerHud"", ""HideKeys"", ""HideJS"", ""SoundsEnabled"", ""PlayerFov"", ""IsVip"", ""BigGifID"", ""GlobalPoints"")
                                                    VALUES 
                                                    (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)
                                                    ON CONFLICT (""SteamID"")
                                                    DO UPDATE SET
                                                    ""PlayerName"" = EXCLUDED.""PlayerName"",
                                                    ""TimesConnected"" = EXCLUDED.""TimesConnected"",
                                                    ""LastConnected"" = EXCLUDED.""LastConnected"",
                                                    ""HideTimerHud"" = EXCLUDED.""HideTimerHud"",
                                                    ""HideKeys"" = EXCLUDED.""HideKeys"",
                                                    ""HideJS"" = EXCLUDED.""HideJS"",
                                                    ""SoundsEnabled"" = EXCLUDED.""SoundsEnabled"",
                                                    ""PlayerFov"" = EXCLUDED.""PlayerFov"",
                                                    ""IsVip"" = EXCLUDED.""IsVip"",
                                                    ""BigGifID"" = EXCLUDED.""BigGifID"",
                                                    ""GlobalPoints"" = EXCLUDED.""GlobalPoints"";
                                                    ";
                                    upsertCommand = new NpgsqlCommand(upsertQuery, (NpgsqlConnection)connection);
                                    break;
                                default:
                                    upsertQuery = null;
                                    upsertCommand = null;
                                    break;
                            }

                            using (upsertCommand)
                            {
                                if (playerTimers.TryGetValue(playerSlot, out PlayerTimerInfo? value) || playerSlot == -1)
                                {
                                    upsertCommand.AddParameterWithValue("@PlayerName", playerName);
                                    upsertCommand.AddParameterWithValue("@SteamID", steamId);
                                    upsertCommand.AddParameterWithValue("@TimesConnected", timesConnected);
                                    upsertCommand.AddParameterWithValue("@LastConnected", lastConnected);
                                    upsertCommand.AddParameterWithValue("@HideTimerHud", playerSlot != -1 && value!.HideTimerHud);
                                    upsertCommand.AddParameterWithValue("@HideKeys", playerSlot != -1 && value!.HideKeys);
                                    upsertCommand.AddParameterWithValue("@HideJS", playerSlot != -1 && value!.HideJumpStats);
                                    upsertCommand.AddParameterWithValue("@SoundsEnabled", playerSlot != -1 && value!.SoundsEnabled);
                                    upsertCommand.AddParameterWithValue("@PlayerFov", playerSlot == -1 ? 0 : value!.PlayerFov);
                                    upsertCommand.AddParameterWithValue("@IsVip", isVip);
                                    upsertCommand.AddParameterWithValue("@BigGifID", bigGif);
                                    upsertCommand.AddParameterWithValue("@GlobalPoints", newPoints);

                                    await upsertCommand.ExecuteNonQueryAsync();

                                    Server.NextFrame(() => GainPointsMessage(playerName, newPoints, playerPoints));
                                    Server.NextFrame(() => SharpTimerDebug($"Set points in database for {playerName} from {playerPoints} to {newPoints}"));
                                }
                                else
                                {
                                    SharpTimerError($"Error setting player points to database for {playerName}: player was not on the server anymore");

                                    return;
                                }
                            }

                        }
                        else
                        {
                            Server.NextFrame(() => SharpTimerDebug($"No player stats yet"));


                            int newPoints;

                            if (!enableStylePoints && style == 0) newPoints = (int)(beatPB == false ? Convert.ToInt32(CalculatePoints(timerTicks, style)! * globalPointsMultiplier!) + playerPoints
                                                                    : Convert.ToInt32(CalculatePoints(timerTicks, style)! - CalculatePoints(oldTicks, style) * globalPointsMultiplier! + playerPoints + (310 * (bonusX == 0 ? mapTier : mapTier * 0.5))));
                            else if (enableStylePoints) newPoints = (int)(beatPB == false ? Convert.ToInt32(CalculatePoints(timerTicks, style)! * globalPointsMultiplier!) + playerPoints
                                                                    : Convert.ToInt32(CalculatePoints(timerTicks, style)! - CalculatePoints(oldTicks, style) * globalPointsMultiplier! + playerPoints + (310 * (bonusX == 0 ? mapTier : mapTier * 0.5))));
                            else newPoints = playerPoints;

                            await row.CloseAsync();

                            string upsertQuery;
                            DbCommand upsertCommand;
                            switch (dbType)
                            {
                                case DatabaseType.MySQL:
                                    upsertQuery = $@"REPLACE INTO {PlayerStatsTable} (PlayerName, SteamID, TimesConnected, LastConnected, HideTimerHud, HideKeys, HideJS, SoundsEnabled, PlayerFov, IsVip, BigGifID, GlobalPoints) VALUES (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)";
                                    upsertCommand = new MySqlCommand(upsertQuery, (MySqlConnection)connection);
                                    break;
                                case DatabaseType.PostgreSQL:
                                    upsertQuery = $@"INSERT INTO ""{PlayerStatsTable}"" (""PlayerName"", ""SteamID"", ""TimesConnected"", ""LastConnected"", ""HideTimerHud"", ""HideKeys"", ""HideJS"", ""SoundsEnabled"", ""PlayerFov"", ""IsVip"", ""BigGifID"", ""GlobalPoints"") VALUES (@PlayerName, @SteamID, @TimesConnected, @LastConnected, @HideTimerHud, @HideKeys, @HideJS, @SoundsEnabled, @PlayerFov, @IsVip, @BigGifID, @GlobalPoints)";
                                    upsertCommand = new NpgsqlCommand(upsertQuery, (NpgsqlConnection)connection);
                                    break;
                                default:
                                    upsertQuery = null;
                                    upsertCommand = null;
                                    break;
                            }

                            using (upsertCommand)
                            {
                                if (playerTimers.TryGetValue(playerSlot, out PlayerTimerInfo? value) || playerSlot == -1)
                                {
                                    upsertCommand.AddParameterWithValue("@PlayerName", playerName);
                                    upsertCommand.AddParameterWithValue("@SteamID", steamId);
                                    upsertCommand.AddParameterWithValue("@TimesConnected", 1);
                                    upsertCommand.AddParameterWithValue("@LastConnected", timeNowUnix);
                                    upsertCommand.AddParameterWithValue("@HideTimerHud", playerSlot != -1 && value!.HideTimerHud);
                                    upsertCommand.AddParameterWithValue("@HideKeys", playerSlot != -1 && value!.HideKeys);
                                    upsertCommand.AddParameterWithValue("@HideJS", playerSlot != -1 && value!.HideJumpStats);
                                    upsertCommand.AddParameterWithValue("@SoundsEnabled", playerSlot != -1 && value!.SoundsEnabled);
                                    upsertCommand.AddParameterWithValue("@PlayerFov", playerSlot == -1 ? 0 : value!.PlayerFov);
                                    upsertCommand.AddParameterWithValue("@IsVip", false);
                                    upsertCommand.AddParameterWithValue("@BigGifID", "x");
                                    upsertCommand.AddParameterWithValue("@GlobalPoints", newPoints);

                                    await upsertCommand.ExecuteNonQueryAsync();

                                    Server.NextFrame(() => GainPointsMessage(playerName, newPoints, playerPoints));
                                    Server.NextFrame(() => SharpTimerDebug($"Set points in database for {playerName} from {playerPoints} to {newPoints}"));
                                }
                                else
                                {
                                    SharpTimerError($"Error setting player points to database for {playerName}: player was not on the server anymore");

                                    return;
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => SharpTimerError($"Error getting player stats from database for {playerName}: {ex}"));
            }
        }

        public async Task PrintTop10PlayerPoints(CCSPlayerController player)
        {
            try
            {
                using (IDbConnection connection = await OpenConnectionAsync())
                {
                    try
                    {
                        string query;
                        DbCommand command;
                        switch (dbType)
                        {
                            case DatabaseType.MySQL:
                                query = $@"SELECT PlayerName, GlobalPoints FROM {PlayerStatsTable} ORDER BY GlobalPoints DESC LIMIT 10";
                                command = new MySqlCommand(query, (MySqlConnection)connection);
                                break;
                            case DatabaseType.PostgreSQL:
                                query = $@"SELECT ""PlayerName"", ""GlobalPoints"" FROM ""{PlayerStatsTable}"" ORDER BY ""GlobalPoints"" DESC LIMIT 10";
                                command = new NpgsqlCommand(query, (NpgsqlConnection)connection);
                                break;
                            default:
                                query = null;
                                command = null;
                                break;
                        }

                        using (command)
                        {
                            using (DbDataReader reader = await command.ExecuteReaderAsync())
                            {
                                Server.NextFrame(() =>
                                {
                                    if (IsAllowedClient(player)) player.PrintToChat($" {Localizer["prefix"]} {Localizer["top_10_points"]}");
                                });

                                int rank = 0;

                                while (await reader.ReadAsync())
                                {
                                    string playerName = reader["PlayerName"].ToString()!;
                                    int points = Convert.ToInt32(reader["GlobalPoints"]);

                                    if (points >= minGlobalPointsForRank)
                                    {
                                        int currentRank = ++rank;
                                        Server.NextFrame(() =>
                                        {
                                            if (IsAllowedClient(player)) player.PrintToChat($" {Localizer["prefix"]} {Localizer["top_10_points_list", currentRank, playerName, points]}");
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Server.NextFrame(() => SharpTimerError($"An error occurred in PrintTop10PlayerPoints inside using con: {ex}"));
                    }
                }
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => SharpTimerError($"An error occurred in PrintTop10PlayerPoints: {ex}"));
            }
        }

        public async Task GetReplayVIPGif(string steamId, int playerSlot)
        {
            Server.NextFrame(() => SharpTimerDebug($"Trying to get replay VIP Gif from database"));
            try
            {
                if (await IsSteamIDaTester(steamId))
                {
                    playerTimers[playerSlot].VipReplayGif = await GetTesterBigGif(steamId);
                    return;
                }

                using (var connection = await OpenConnectionAsync())
                {
                    await CreatePlayerRecordsTableAsync(connection);
                    string selectQuery;
                    DbCommand selectCommand;
                    switch (dbType)
                    {
                        case DatabaseType.MySQL:
                            selectQuery = selectQuery = $"SELECT IsVip, BigGifID FROM {PlayerStatsTable} WHERE SteamID = @SteamID";
                            selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                            break;
                        case DatabaseType.PostgreSQL:
                            selectQuery = $@"SELECT ""IsVip"", ""BigGifID"" FROM ""{PlayerStatsTable}"" WHERE ""SteamID"" = @SteamID";
                            selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                            break;
                        default:
                            selectQuery = null;
                            selectCommand = null;
                            break;
                    }

                    using (selectCommand)
                    {
                        selectCommand.AddParameterWithValue("@SteamID", steamId);

                        var row = await selectCommand.ExecuteReaderAsync();

                        if (row.Read() && playerTimers.TryGetValue(playerSlot, out PlayerTimerInfo? value))
                        {
                            // get player columns
                            bool isVip = row.GetBoolean("IsVip");
                            if (isVip)
                            {
                                Server.NextFrame(() => SharpTimerDebug($"Replay is VIP setting gif..."));
                                value.VipReplayGif = $"<br><img src='https://files.catbox.moe/{row.GetString("BigGifID")}.gif'><br>";
                            }
                            else
                            {
                                Server.NextFrame(() => SharpTimerDebug($"Replay is not VIP..."));
                                value.VipReplayGif = "x";
                            }

                            await row.CloseAsync();
                        }
                        else
                        {
                            await row.CloseAsync();
                            Server.NextFrame(() => SharpTimerDebug($"Replay is not VIP... goofy"));
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => SharpTimerError($"Error getting ReplayVIPGif from database: {ex}"));
            }
        }

        public async Task<(string, string, string)> GetMapRecordSteamIDFromDatabase(int bonusX = 0, int top10 = 0, int style = 0)
        {
            SharpTimerDebug($"Trying to get {(bonusX != 0 ? $"bonus {bonusX}" : "map")} record steamid from database");
            try
            {
                using (IDbConnection connection = await OpenConnectionAsync())
                {
                    await CreatePlayerRecordsTableAsync(connection);
                    string selectQuery;
                    DbCommand selectCommand;
                    if (top10 != 0)
                    {
                        switch (dbType)
                        {
                            case DatabaseType.MySQL:
                                // Get the top N records based on TimerTicks
                                selectQuery = "SELECT SteamID, PlayerName, TimerTicks " +
                                                "FROM PlayerRecords " +
                                                "WHERE MapName = @MapName " +
                                                "AND Style = @Style " +
                                                "ORDER BY TimerTicks ASC " +
                                                $"LIMIT 1 OFFSET {top10 - 1};";
                                selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                                break;
                            case DatabaseType.PostgreSQL:
                                // Get the top N records based on TimerTicks
                                selectQuery = @"SELECT ""SteamID"", ""PlayerName"", ""TimerTicks"" " +
                                                @"FROM ""PlayerRecords"" " +
                                                @"WHERE ""MapName"" = @MapName " +
                                                @"AND ""Style"" = @Style " +
                                                @"ORDER BY ""TimerTicks"" ASC " +
                                                $"LIMIT 1 OFFSET {top10 - 1};";
                                selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                                break;
                            default:
                                selectQuery = null;
                                selectCommand = null;
                                break;
                        }
                    }
                    else
                    {
                        // Get the overall top player
                        switch (dbType)
                        {
                            case DatabaseType.MySQL:
                                selectQuery = $"SELECT SteamID, PlayerName, TimerTicks FROM PlayerRecords WHERE MapName = @MapName AND Style = @Style ORDER BY TimerTicks ASC LIMIT 1";
                                selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                                break;
                            case DatabaseType.PostgreSQL:
                                selectQuery = $@"SELECT ""SteamID"", ""PlayerName"", ""TimerTicks"" FROM ""PlayerRecords"" WHERE ""MapName"" = @MapName AND ""Style"" = @Style ORDER BY ""TimerTicks"" ASC LIMIT 1";
                                selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                                break;
                            default:
                                selectQuery = null;
                                selectCommand = null;
                                break;
                        }
                    }

                    using (selectCommand)
                    {
                        selectCommand.AddParameterWithValue("@MapName", bonusX == 0 ? currentMapName : $"{currentMapName}_bonus{bonusX}");
                        selectCommand.AddParameterWithValue("@Style", style);

                        var row = await selectCommand.ExecuteReaderAsync();

                        if (row.Read())
                        {
                            string steamId64 = row.GetString("SteamID");
                            string playerName = row.GetString("PlayerName");
                            string timerTicks = FormatTime(row.GetInt32("TimerTicks"));


                            await row.CloseAsync();

                            return (steamId64, playerName, timerTicks);
                        }
                        else
                        {
                            await row.CloseAsync();

                            return ("null", "null", "null");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Server.NextFrame(() => SharpTimerError($"Error getting GetMapRecordSteamIDFromDatabase from database: {ex}"));
                return ("null", "null", "null");
            }
        }

        public async Task<int> GetPreviousPlayerRecordFromDatabase(CCSPlayerController? player, string steamId, string currentMapName, string playerName, int bonusX = 0, int style = 0)
        {
            SharpTimerDebug($"Trying to get Previous {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} from database for {playerName}");
            try
            {
                if (!IsAllowedClient(player))
                {
                    return 0;
                }

                string currentMapNamee = bonusX == 0 ? currentMapName : $"{currentMapName}_bonus{bonusX}";

                using (IDbConnection connection = await OpenConnectionAsync())
                {
                    await CreatePlayerRecordsTableAsync(connection);
                    string selectQuery;
                    DbCommand selectCommand;

                    // Retrieve the TimerTicks value for the specified player on the current map
                    switch (dbType)
                    {
                        case DatabaseType.MySQL:
                            selectQuery = "SELECT TimerTicks FROM PlayerRecords WHERE MapName = @MapName AND SteamID = @SteamID AND Style = @Style";
                            selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                            break;
                        case DatabaseType.PostgreSQL:
                            selectQuery = @"SELECT ""TimerTicks"" FROM ""PlayerRecords"" WHERE ""MapName"" = @MapName AND ""SteamID"" = @SteamID AND ""Style"" = @Style";
                            selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                            break;
                        default:
                            selectQuery = null;
                            selectCommand = null;
                            break;
                    }

                    using (selectCommand)
                    {
                        selectCommand.AddParameterWithValue("@MapName", currentMapNamee);
                        selectCommand.AddParameterWithValue("@SteamID", steamId);
                        selectCommand.AddParameterWithValue("@Style", style);

                        var result = await selectCommand.ExecuteScalarAsync();

                        // Check for DBNull
                        if (result != null && result != DBNull.Value)
                        {
                            SharpTimerDebug($"Got Previous Time from database for {playerName}");
                            return Convert.ToInt32(result);
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error getting previous player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} from database: {ex.Message}");
            }

            return 0;
        }

        public async Task<int> GetPlayerPointsFromDatabase(CCSPlayerController? player, string steamId, string playerName)
        {
            SharpTimerDebug("Trying GetPlayerPointsFromDatabase");
            int playerPoints = 0;

            try
            {
                if (!IsAllowedClient(player))
                {
                    return playerPoints;
                }

                using (var connection = await OpenConnectionAsync())
                {
                    await CreatePlayerStatsTableAsync(connection);
                    string selectQuery;
                    DbCommand selectCommand;
                    switch (dbType)
                    {
                        case DatabaseType.MySQL:
                            selectQuery = "SELECT GlobalPoints FROM PlayerStats WHERE SteamID = @SteamID";
                            selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                            break;
                        case DatabaseType.PostgreSQL:
                            selectQuery = @"SELECT ""GlobalPoints"" FROM ""PlayerStats"" WHERE ""SteamID"" = @SteamID";
                            selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                            break;
                        default:
                            selectQuery = null;
                            selectCommand = null;
                            break;
                    }

                    using (selectCommand)
                    {
                        selectCommand.AddParameterWithValue("@SteamID", steamId);

                        var result = await selectCommand.ExecuteScalarAsync();

                        // Check for DBNull
                        if (result != null && result != DBNull.Value)
                        {
                            playerPoints = Convert.ToInt32(result);
                            SharpTimerDebug($"Got Player Points from database for {playerName} p: {playerPoints}");
                            return playerPoints;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error getting player points from database: {ex.Message}");
            }
            return playerPoints;
        }

        public async Task<Dictionary<string, PlayerRecord>> GetSortedRecordsFromDatabase(int limit = 0, int bonusX = 0, string mapName = "", int style = 0)
        {
            SharpTimerDebug($"Trying GetSortedRecords {(bonusX != 0 ? $"bonus {bonusX}" : "")} from database");
            using (var connection = await OpenConnectionAsync())
            {
                try
                {
                    string? currentMapNamee;
                    if (string.IsNullOrEmpty(mapName))
                        currentMapNamee = bonusX == 0 ? currentMapName! : $"{currentMapName}_bonus{bonusX}";
                    else
                        currentMapNamee = mapName;

                    await CreatePlayerRecordsTableAsync(connection);

                    // Retrieve and sort records for the current map
                    string selectQuery;
                    DbCommand selectCommand;
                    if (limit != 0)
                    {
                        switch (dbType)
                        {
                            case DatabaseType.MySQL:
                                selectQuery = $@"SELECT SteamID, PlayerName, TimerTicks FROM PlayerRecords WHERE MapName = @MapName AND Style = @Style ORDER BY TimerTicks ASC LIMIT {limit}";
                                selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                                break;
                            case DatabaseType.PostgreSQL:
                                selectQuery = $@"SELECT ""SteamID"", ""PlayerName"", ""TimerTicks"" FROM ""PlayerRecords"" WHERE ""MapName"" = @MapName AND ""Style"" = @Style ORDER BY ""TimerTicks"" ASC LIMIT {limit}";
                                selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                                break;
                            default:
                                selectQuery = null;
                                selectCommand = null;
                                break;
                        }
                    }
                    else
                    {
                        switch (dbType)
                        {
                            case DatabaseType.MySQL:
                                selectQuery = @"SELECT SteamID, PlayerName, TimerTicks FROM PlayerRecords WHERE MapName = @MapName AND Style = @Style";
                                selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                                break;
                            case DatabaseType.PostgreSQL:
                                selectQuery = @"SELECT ""SteamID"", ""PlayerName"", ""TimerTicks"" FROM ""PlayerRecords"" WHERE ""MapName"" = @MapName AND ""Style"" = @Style";
                                selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                                break;
                            default:
                                selectQuery = null;
                                selectCommand = null;
                                break;
                        }
                    }
                    using (selectCommand)
                    {
                        selectCommand.AddParameterWithValue("@MapName", currentMapNamee);
                        selectCommand.AddParameterWithValue("@Style", style);
                        using (var reader = await selectCommand.ExecuteReaderAsync())
                        {
                            var sortedRecords = new Dictionary<string, PlayerRecord>();
                            while (await reader.ReadAsync())
                            {
                                string steamId = reader.GetString(0);
                                string playerName = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
                                int timerTicks = reader.GetInt32(2);
                                sortedRecords.Add(steamId, new PlayerRecord
                                {
                                    PlayerName = playerName,
                                    TimerTicks = timerTicks
                                });
                            }

                            // Sort the records by TimerTicks
                            sortedRecords = sortedRecords.OrderBy(record => record.Value.TimerTicks)
                                                        .ToDictionary(record => record.Key, record => record.Value);

                            SharpTimerDebug($"Got GetSortedRecords {(bonusX != 0 ? $"bonus {bonusX}" : "")} from database");

                            return sortedRecords;
                        }
                    }
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error getting sorted records from database: {ex.Message}");
                }
            }
            return [];
        }

        public async Task<Dictionary<string, PlayerPoints>> GetSortedPointsFromDatabase()
        {
            SharpTimerDebug("Trying GetSortedPoints from database");
            using (var connection = await OpenConnectionAsync())
            {
                try
                {
                    await CreatePlayerStatsTableAsync(connection);
                    string selectQuery;
                    DbCommand selectCommand;
                    switch (dbType)
                    {
                        case DatabaseType.MySQL:
                            selectQuery = $@"SELECT SteamID, PlayerName, GlobalPoints FROM {PlayerStatsTable}";
                            selectCommand = new MySqlCommand(selectQuery, (MySqlConnection)connection);
                            break;
                        case DatabaseType.PostgreSQL:
                            selectQuery = $@"SELECT ""SteamID"", ""PlayerName"", ""GlobalPoints"" FROM ""{PlayerStatsTable}""";
                            selectCommand = new NpgsqlCommand(selectQuery, (NpgsqlConnection)connection);
                            break;
                        default:
                            selectQuery = null;
                            selectCommand = null;
                            break;
                    }

                    using (selectCommand)
                    {
                        using (var reader = await selectCommand.ExecuteReaderAsync())
                        {
                            var sortedPoints = new Dictionary<string, PlayerPoints>();
                            while (await reader.ReadAsync())
                            {
                                string steamId = reader.GetString(0);
                                string playerName = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
                                int globalPoints = reader.GetInt32(2);

                                if (globalPoints >= minGlobalPointsForRank) // Only add if GlobalPoints is above or equal to minGlobalPointsForRank
                                {
                                    sortedPoints.Add(steamId, new PlayerPoints
                                    {
                                        PlayerName = playerName,
                                        GlobalPoints = globalPoints
                                    });
                                }
                            }

                            sortedPoints = sortedPoints.OrderByDescending(record => record.Value.GlobalPoints)
                                                        .ToDictionary(record => record.Key, record => record.Value);



                            return sortedPoints;
                        }
                    }
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error getting GetSortedPoints from database: {ex.Message}");
                }
            }
            return [];
        }

        [ConsoleCommand("css_importpoints", " ")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void ImportPlayerPointsCommand(CCSPlayerController? player, CommandInfo command)
        {
            _ = Task.Run(ImportPlayerPoints);
        }

        public async Task ImportPlayerPoints()
        {
            try
            {
                var sortedRecords = await GetSortedRecordsFromDatabase();

                foreach (var kvp in sortedRecords)
                {
                    string playerSteamID = kvp.Key;
                    string playerName = kvp.Value.PlayerName!;
                    int timerTicks = kvp.Value.TimerTicks;

                    if ((usePostgres || useMySQL) && globalRanksEnabled == true)
                    {
                        _ = Task.Run(async () => await SavePlayerPoints(playerSteamID, playerName, -1, timerTicks, 0, false, 0, 0));
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error ImportPlayerPoints to the database: {ex.Message}");
            }
        }

        [ConsoleCommand("css_jsontodatabase", " ")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void AddJsonTimesToDatabaseCommand(CCSPlayerController? player, CommandInfo command)
        {
            _ = Task.Run(AddJsonTimesToDatabaseAsync);
        }

        public async Task AddJsonTimesToDatabaseAsync()
        {
            try
            {
                string recordsDirectoryNamee = "SharpTimer/PlayerRecords";
                string playerRecordsPathh = Path.Combine(gameDir!, "csgo", "cfg", recordsDirectoryNamee);

                if (!Directory.Exists(playerRecordsPathh))
                {
                    SharpTimerDebug($"Error: Directory not found at {playerRecordsPathh}");
                    return;
                }

                string connectionString = await GetConnectionStringFromConfigFile();

                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Check if the table exists, and create it if necessary
                    string createTableQuery = @"CREATE TABLE IF NOT EXISTS PlayerRecords (
                                            MapName VARCHAR(255),
                                            SteamID VARCHAR(255),
                                            PlayerName VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
                                            TimerTicks INT,
                                            FormattedTime VARCHAR(255),
                                            UnixStamp INT,
                                            TimesFinished INT,
                                            LastFinished INT,
                                            PRIMARY KEY (MapName, SteamID)
                                        )";

                    using (var createTableCommand = new MySqlCommand(createTableQuery, connection))
                    {
                        await createTableCommand.ExecuteNonQueryAsync();
                    }

                    foreach (var filePath in Directory.EnumerateFiles(playerRecordsPathh, "*.json"))
                    {
                        string json = await File.ReadAllTextAsync(filePath);
                        var records = JsonSerializer.Deserialize<Dictionary<string, PlayerRecord>>(json);

                        if (records == null)
                        {
                            SharpTimerDebug($"Error: Failed to deserialize JSON data from {filePath}");
                            continue;
                        }

                        foreach (var recordEntry in records)
                        {
                            string steamId = recordEntry.Key;
                            PlayerRecord playerRecord = recordEntry.Value;

                            // Extract MapName from the filename (remove extension)
                            string mapName = Path.GetFileNameWithoutExtension(filePath);

                            // Check if the player is already in the database
                            string insertOrUpdateQuery = @"
                                INSERT INTO PlayerRecords (SteamID, PlayerName, TimerTicks, FormattedTime, MapName, UnixStamp, TimesFinished, LastFinished)
                                VALUES (@SteamID, @PlayerName, @TimerTicks, @FormattedTime, @MapName, @UnixStamp, @TimesFinished, @LastFinished)
                                ON DUPLICATE KEY UPDATE
                                TimerTicks = IF(@TimerTicks < TimerTicks, @TimerTicks, TimerTicks),
                                FormattedTime = IF(@TimerTicks < TimerTicks, @FormattedTime, FormattedTime)";

                            using (var insertOrUpdateCommand = new MySqlCommand(insertOrUpdateQuery, connection))
                            {
                                insertOrUpdateCommand.Parameters.AddWithValue("@SteamID", steamId);
                                insertOrUpdateCommand.Parameters.AddWithValue("@PlayerName", playerRecord.PlayerName);
                                insertOrUpdateCommand.Parameters.AddWithValue("@TimerTicks", playerRecord.TimerTicks);
                                insertOrUpdateCommand.Parameters.AddWithValue("@FormattedTime", FormatTime(playerRecord.TimerTicks));
                                insertOrUpdateCommand.Parameters.AddWithValue("@MapName", mapName);
                                insertOrUpdateCommand.Parameters.AddWithValue("@UnixStamp", 0);
                                insertOrUpdateCommand.Parameters.AddWithValue("@TimesFinished", 0);
                                insertOrUpdateCommand.Parameters.AddWithValue("@LastFinished", 0);

                                await insertOrUpdateCommand.ExecuteNonQueryAsync();
                            }
                        }

                        SharpTimerDebug($"JSON times from {Path.GetFileName(filePath)} successfully added to the database.");
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error adding JSON times to the database: {ex.Message}");
            }
        }

        [ConsoleCommand("css_databasetojson", " ")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void ExportDatabaseToJsonCommand(CCSPlayerController? player, CommandInfo command)
        {
            _ = Task.Run(ExportDatabaseToJsonAsync);
        }

        public async Task ExportDatabaseToJsonAsync()
        {
            string recordsDirectoryNamee = "SharpTimer/PlayerRecords";
            string playerRecordsPathh = Path.Combine(gameDir!, "csgo", "cfg", recordsDirectoryNamee);

            try
            {
                string connectionString = await GetConnectionStringFromConfigFile();

                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = "SELECT SteamID, PlayerName, TimerTicks, MapName FROM PlayerRecords";
                    using (var selectCommand = new MySqlCommand(selectQuery, connection))
                    {
                        using (var reader = await selectCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string steamId = reader.GetString(0);
                                string playerName = reader.GetString(1);
                                int timerTicks = reader.GetInt32(2);
                                string mapName = reader.GetString(3);

                                Directory.CreateDirectory(playerRecordsPathh);

                                Dictionary<string, PlayerRecord> records;
                                string filePath = Path.Combine(playerRecordsPathh, $"{mapName}.json");
                                if (File.Exists(filePath))
                                {
                                    string existingJson = await File.ReadAllTextAsync(filePath);
                                    records = JsonSerializer.Deserialize<Dictionary<string, PlayerRecord>>(existingJson) ?? [];
                                }
                                else
                                {
                                    records = [];
                                }

                                records[steamId] = new PlayerRecord
                                {
                                    PlayerName = playerName,
                                    TimerTicks = timerTicks
                                };

                                string updatedJson = JsonSerializer.Serialize(records, jsonSerializerOptions);

                                await File.WriteAllTextAsync(filePath, updatedJson);

                                SharpTimerDebug($"Player records for map {mapName} successfully exported to JSON.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error exporting player records to JSON: {ex.Message}");
            }
        }
    }
}
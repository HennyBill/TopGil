using Microsoft.Data.Sqlite;

using System;
using System.Collections.Generic;
using System.IO;

namespace TopGil;

internal class GilStorageManager : IDisposable
{
    private static int DATABASE_VERSION = 1;
    private string DatabaseFile = null;

    internal GilStorageManager()
    {
        OpenDatabase();
    }

    private void OpenDatabase()
    {
        string dbPath = Plugin.PluginInterface.ConfigDirectory.FullName;

        // Ensure the database path exists
        if (!Directory.Exists(dbPath))
        {
            Directory.CreateDirectory(dbPath);
        }

        this.DatabaseFile = Path.Join(dbPath, "gilchest-sqlite.db");

        //if (!File.Exists(this.DatabaseFile))
        {
            // Create a new database - not anymore, we add any missing tables - need rework for future versions
            using (var connection = new SqliteConnection($"Data Source={this.DatabaseFile}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS SystemInfo (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Version INTEGER NOT NULL,
                        ApplicationName TEXT NOT NULL,
                        Notes TEXT,
                        LastUpdateTimestamp TEXT DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS Characters (
                        UniqueId TEXT PRIMARY KEY,
                        Name TEXT NOT NULL,
                        WorldId INTEGER NOT NULL,
                        Gil INTEGER NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS Retainers (
                        RetainerId INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Gil INTEGER NOT NULL,
                        OwnerCharacterId TEXT NOT NULL,
                        FOREIGN KEY(OwnerCharacterId) REFERENCES Characters(UniqueId)
                    );

                    CREATE TABLE IF NOT EXISTS GilRecords (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        CharacterId TEXT NOT NULL,
                        RetainerId INTEGER,
                        Gil INTEGER NOT NULL,
                        Timestamp TEXT,
                        FOREIGN KEY(CharacterId) REFERENCES Characters(UniqueId),
                        FOREIGN KEY(RetainerId) REFERENCES Retainers(RetainerId)
                    );

                    CREATE TABLE IF NOT EXISTS FirstDailyGilRecords (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        CharacterId TEXT NOT NULL,
                        RetainerId INTEGER,
                        Gil INTEGER NOT NULL,
                        Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS DailyGilSummary (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        CharacterId TEXT,
                        RetainerId INTEGER,
                        TotalGil INTEGER,
                        Date TEXT,
                        FOREIGN KEY(CharacterId) REFERENCES Characters(UniqueId),
                        FOREIGN KEY(RetainerId) REFERENCES Retainers(RetainerId)
                    );

                    CREATE TABLE IF NOT EXISTS GilRecordsArchive (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        CharacterId TEXT,
                        RetainerId INTEGER,
                        Gil INTEGER,
                        Timestamp TEXT,
                        FOREIGN KEY(CharacterId) REFERENCES Characters(UniqueId),
                        FOREIGN KEY(RetainerId) REFERENCES Retainers(RetainerId)
                    );


                    CREATE VIEW IF NOT EXISTS GilRecordsView AS
                    SELECT
                        gr.Id,
                        c.Name AS CharacterName,
                        r.Name AS RetainerName,
                        gr.Gil,
                        gr.Timestamp
                    FROM
                        GilRecords gr
                    LEFT JOIN
                        Characters c ON gr.CharacterId = c.UniqueId
                    LEFT JOIN
                        Retainers r ON gr.RetainerId = r.RetainerId;

                    CREATE VIEW IF NOT EXISTS DailyGilSummaryView AS
                    SELECT
                        dgs.Id,
                        dgs.CharacterId,
                        c.Name AS CharacterName,
                        c.WorldId,
                        dgs.RetainerId,
                        r.Name AS RetainerName,
                        dgs.TotalGil,
                        dgs.Date
                    FROM
                        DailyGilSummary dgs
                    LEFT JOIN
                        Characters c ON dgs.CharacterId = c.UniqueId
                    LEFT JOIN
                        Retainers r ON dgs.RetainerId = r.RetainerId;

                    CREATE VIEW IF NOT EXISTS FirstDailyGilRecordsView AS
                    SELECT
                        fdr.Id,
                        c.Name AS CharacterName,
                        r.Name AS RetainerName,
                        fdr.Gil,
                        fdr.Timestamp
                    FROM
                        FirstDailyGilRecords fdr
                    LEFT JOIN
                        Characters c ON fdr.CharacterId = c.UniqueId
                    LEFT JOIN
                        Retainers r ON fdr.RetainerId = r.RetainerId;

                    CREATE VIEW IF NOT EXISTS FirstDailyGilRecordsTotalView AS
                    SELECT
                        c.Name AS CharacterName,
                        SUM(fdr.Gil) AS TotalGil,
                        fdr.Timestamp
                    FROM
                        FirstDailyGilRecords fdr
                    LEFT JOIN
                        Characters c ON fdr.CharacterId = c.UniqueId
                    GROUP BY fdr.CharacterId

                ";

                command.ExecuteNonQuery();

                // Initialize the database with the current plugin version
                command.CommandText = @"
                    INSERT INTO SystemInfo (Version, ApplicationName, Notes)
                    SELECT @Version, @ApplicationName, @Notes
                    WHERE NOT EXISTS (SELECT 1 FROM SystemInfo);
                ";
                command.Parameters.AddWithValue("@Version", DATABASE_VERSION);
                command.Parameters.AddWithValue("@ApplicationName", Plugin.PluginName);
                command.Parameters.AddWithValue("@Notes", "Initial version");
                command.ExecuteNonQuery();

                // Insert dummy character if not exists
                //command.CommandText = @"
                //    INSERT INTO Characters (UniqueId, Name, WorldId, Gil)
                //    SELECT '00000000-0000-0000-0000-000000000000', 'Dummy Character', 0, 0
                //    WHERE NOT EXISTS (SELECT 1 FROM Characters WHERE UniqueId = '00000000-0000-0000-0000-000000000000');
                //";
                //command.ExecuteNonQuery();

                // Insert dummy retainer if not exists
                //command.CommandText = @"
                //    INSERT INTO Retainers (RetainerId, Name, Gil, OwnerCharacterId)
                //    SELECT 0, 'Dummy Retainer', 0, '00000000-0000-0000-0000-000000000000'
                //    WHERE NOT EXISTS (SELECT 1 FROM Retainers WHERE RetainerId = 0);
                //";
                //command.ExecuteNonQuery();
            }
        }
    }

    private void CheckDatabaseFile()
    {
        if (this.DatabaseFile == null)
        {
            //TODO: log this
            throw new InvalidOperationException("Database operation failed - database file is not set.");
        }
    }

    #region *** SystemInfo table ***

    internal DateTime? GetLastUpdateTimestamp()
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT LastUpdateTimestamp
                FROM SystemInfo
                ORDER BY Id DESC
                LIMIT 1;
            ";

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    string timestamp = reader.GetString(0);
                    if (DateTime.TryParse(timestamp, out DateTime dateTime))
                    {
                        return dateTime;
                    }
                }
            }
        }

        return null;
    }

    internal DateTime UpdateLastUpdateTimestamp(DateTime currentTime = default)
	{
		if (currentTime == default)
		{
			currentTime = DateTime.Now;
		}

		CheckDatabaseFile();

		using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
		{
			connection.Open();

			var command = connection.CreateCommand();
			command.CommandText = @"
                UPDATE SystemInfo
                SET LastUpdateTimestamp = @CurrentTime
                WHERE Id = (SELECT Id FROM SystemInfo ORDER BY Id DESC LIMIT 1);
            ";
			command.Parameters.AddWithValue("@CurrentTime", currentTime.ToString("yyyy-MM-dd HH:mm:ss"));

			command.ExecuteNonQuery();
		}

		return currentTime;
	}

    #endregion

    #region *** Characters ***

    internal void AddCharacter(GilCharacter character)
    {
        CheckDatabaseFile();

        if (character.UniqueId == Guid.Empty)
        {
            throw new InvalidOperationException("AddCharacter: UniqueId is empty.");
        }

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Characters (UniqueId, Name, WorldId, Gil)
                VALUES (@UniqueId, @Name, @WorldId, @Gil);
            ";
            command.Parameters.AddWithValue("@UniqueId", character.UniqueId.ToString());
            command.Parameters.AddWithValue("@Name", character.Name);
            command.Parameters.AddWithValue("@WorldId", character.HomeWorldId);
            command.Parameters.AddWithValue("@Gil", character.Gil);
            command.ExecuteNonQuery();
        }
    }

    internal void UpdateCharacter(GilCharacter character)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Characters
                SET Name = @Name,
                    WorldId = @WorldId,
                    Gil = @Gil
                WHERE UniqueId = @UniqueId;
            ";
            command.Parameters.AddWithValue("@UniqueId", character.UniqueId.ToString());
            command.Parameters.AddWithValue("@Name", character.Name);
            command.Parameters.AddWithValue("@WorldId", character.HomeWorldId);
            command.Parameters.AddWithValue("@Gil", character.Gil);
            command.ExecuteNonQuery();
        }
    }

    internal void AddOrUpdateCharacter(GilCharacter character)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Characters (UniqueId, Name, WorldId, Gil)
                VALUES (@UniqueId, @Name, @WorldId, @Gil)
                ON CONFLICT(UniqueId) DO UPDATE SET
                    Name = excluded.Name,
                    WorldId = excluded.WorldId,
                    Gil = excluded.Gil;
            ";
            command.Parameters.AddWithValue("@UniqueId", character.UniqueId.ToString());
            command.Parameters.AddWithValue("@Name", character.Name);
            command.Parameters.AddWithValue("@WorldId", character.HomeWorldId);
            command.Parameters.AddWithValue("@Gil", character.Gil);
            command.ExecuteNonQuery();
        }
    }

    internal void DeleteCharacter(Guid uniqueId)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM Characters
                WHERE UniqueId = @UniqueId;
            ";
            command.Parameters.AddWithValue("@UniqueId", uniqueId.ToString());
            command.ExecuteNonQuery();
        }
    }

    internal GilCharacter? LoadCharacterByNameAndWorldId(string name, uint worldId)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT UniqueId, Name, WorldId, Gil
                FROM Characters
                WHERE Name = @Name COLLATE NOCASE AND WorldId = @WorldId;
            ";
            command.Parameters.AddWithValue("@Name", name);
            command.Parameters.AddWithValue("@WorldId", worldId);

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    return new GilCharacter
                    {
                        UniqueId = Guid.Parse(reader.GetString(0)),
                        Name = reader.GetString(1),
                        HomeWorldId = (uint)reader.GetInt32(2),
                        Gil = (uint)reader.GetInt32(3),
                    };
                }
            }
        }

        return null;
    }

    internal GilCharacter? LoadCharacterByUniqueId(Guid uniqueId)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT UniqueId, Name, WorldId, Gil
                FROM Characters
                WHERE UniqueId = @UniqueId;
            ";
            command.Parameters.AddWithValue("@UniqueId", uniqueId.ToString());

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    return new GilCharacter
                    {
                        UniqueId = Guid.Parse(reader.GetString(0)),
                        Name = reader.GetString(1),
                        HomeWorldId = (uint)reader.GetInt32(2),
                        Gil = (uint)reader.GetInt32(3),
                    };
                }
            }
        }

        return null;
    }

    #endregion

    #region *** Retainers ***

    internal void AddOrUpdateRetainer(GilRetainer retainer)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Retainers (RetainerId, Name, Gil, OwnerCharacterId)
                VALUES (@RetainerId, @Name, @Gil, @OwnerCharacterId)
                ON CONFLICT(RetainerId) DO UPDATE SET
                    Name = excluded.Name,
                    Gil = excluded.Gil,
                    OwnerCharacterId = excluded.OwnerCharacterId;
            ";
            command.Parameters.AddWithValue("@RetainerId", retainer.RetainerId);
            command.Parameters.AddWithValue("@Name", retainer.Name);
            command.Parameters.AddWithValue("@Gil", retainer.Gil);
            command.Parameters.AddWithValue("@OwnerCharacterId", retainer.OwnerCharacterId.ToString());
            command.ExecuteNonQuery();
        }
    }


    internal void DeleteRetainer(ulong retainerId)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM Retainers
                WHERE RetainerId = @RetainerId;
            ";
            command.Parameters.AddWithValue("@RetainerId", retainerId);
            command.ExecuteNonQuery();
        }
    }

    internal GilRetainer? LoadRetainerByNameAndOwner(string name, Guid ownerCharacterId)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT RetainerId, Name, Gil, OwnerCharacterId
                FROM Retainers
                WHERE Name = @Name COLLATE NOCASE AND OwnerCharacterId = @OwnerCharacterId;
            ";
            command.Parameters.AddWithValue("@Name", name);
            command.Parameters.AddWithValue("@OwnerCharacterId", ownerCharacterId.ToString());

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    return new GilRetainer
                    {
                        RetainerId = (ulong)reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Gil = (uint)reader.GetInt32(2),
                        OwnerCharacterId = Guid.Parse(reader.GetString(3)),
                    };
                }
            }
        }

        return null;
    }

    internal List<GilRetainer> GetAllRetainers()
    {
        CheckDatabaseFile();

        var retainers = new List<GilRetainer>();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
                command.CommandText = @"
                SELECT RetainerId, Name, Gil, OwnerCharacterId
                FROM Retainers;
            ";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    //var retainer = new GilRetainer
                    //{
                    //    RetainerId = (ulong)reader.GetInt32(0),
                    //    Name = reader.GetString(1),
                    //    Gil = (uint)reader.GetInt32(2),
                    //    OwnerCharacterId = Guid.Parse(reader.GetString(3)),
                    //};

                    GilRetainer retainer = new GilRetainer
                    {
                        RetainerId = reader.GetFieldValue<ulong>(0), // Use ulong if RetainerId is large
                        Name = reader.GetString(1),
                        Gil = reader.GetFieldValue<uint>(2), // Use uint if Gil is large
                        OwnerCharacterId = reader.GetGuid(3)
                    };

                    retainers.Add(retainer);
                }
            }
        }

        return retainers;
    }

    internal GilRetainer? LoadRetainerByRetainerId(ulong retainerId)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT RetainerId, Name, Gil, OwnerCharacterId
                FROM Retainers
                WHERE RetainerId = @RetainerId;
            ";
            command.Parameters.AddWithValue("@RetainerId", retainerId);

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    return new GilRetainer
                    {
                        RetainerId = (ulong)reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Gil = (uint)reader.GetInt32(2),
                        OwnerCharacterId = Guid.Parse(reader.GetString(3)),
                    };
                }
            }
        }

        return null;
    }


    #endregion

    #region *** Save Gil, do daily aggregation and reporting ***

    /// <summary>
    /// Save gil record for a character or a retainer.
    /// Set retainerId to 0 if the record is for a character.
    /// </summary>
    internal void SaveGilRecord(Guid characterId, ulong retainerId, long gil)
    {
        CheckDatabaseFile();

//-        DebuggerLog.Write($"SaveGilRecord: CharacterId: '{characterId}', RetainerId: '{retainerId}', Gil: {gil}");

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                //
                // FirstDailyGilRecords table
                //

                var checkCommand1 = connection.CreateCommand();
                checkCommand1.CommandText = @"
                    SELECT COUNT(*) FROM FirstDailyGilRecords
                    WHERE CharacterId = $CharacterId AND RetainerId = $RetainerId AND DATE(Timestamp) = DATE('now')";
                checkCommand1.Parameters.AddWithValue("$CharacterId", characterId.ToString());
                checkCommand1.Parameters.AddWithValue("$RetainerId", retainerId);

				var count = (long)(checkCommand1.ExecuteScalar() ?? 0);

                if (count == 0)
                {
                    var insertDailyCommand = connection.CreateCommand();
                    insertDailyCommand.CommandText = @"
                        INSERT INTO FirstDailyGilRecords (CharacterId, RetainerId, Gil, Timestamp)
                        VALUES ($CharacterId, $RetainerId, $Gil, $Timestamp)";
                    insertDailyCommand.Parameters.AddWithValue("$CharacterId", characterId.ToString());
                    insertDailyCommand.Parameters.AddWithValue("$RetainerId", retainerId);
                    insertDailyCommand.Parameters.AddWithValue("$Gil", gil);
                    insertDailyCommand.Parameters.AddWithValue("$Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    insertDailyCommand.ExecuteNonQuery();
                }



                //
                // GilRecords table
                //

                // Check if a record for today already exists
                var checkCommand2 = connection.CreateCommand();
                checkCommand2.CommandText = @"
                    SELECT Gil FROM GilRecords
                    WHERE CharacterId = @CharacterId AND
                          (RetainerId = @RetainerId OR (@RetainerId IS NULL AND RetainerId IS NULL)) AND
                          DATE(Timestamp) = DATE('now');
                ";
                checkCommand2.Parameters.AddWithValue("@CharacterId", characterId.ToString());
                checkCommand2.Parameters.AddWithValue("@RetainerId", retainerId == 0 ? (object)DBNull.Value : retainerId);

                var existingGil = checkCommand2.ExecuteScalar();

                if (existingGil != null)
                {
                    // Update the existing record
                    var updateCommand = connection.CreateCommand();
                    updateCommand.CommandText = @"
                        UPDATE GilRecords
                        SET Gil = @Gil, Timestamp = @Timestamp
                        WHERE CharacterId = @CharacterId AND
                              (RetainerId = @RetainerId OR (@RetainerId IS NULL AND RetainerId IS NULL)) AND
                              DATE(Timestamp) = DATE('now');
                    ";
                    updateCommand.Parameters.AddWithValue("@Gil", gil);
                    updateCommand.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    updateCommand.Parameters.AddWithValue("@CharacterId", characterId.ToString());
                    updateCommand.Parameters.AddWithValue("@RetainerId", retainerId == 0 ? (object)DBNull.Value : retainerId);
                    int rowsUpdated = updateCommand.ExecuteNonQuery();

//-                    DebuggerLog.Write($"SaveGilRecord updated {rowsUpdated} rows.");
                }
                else
                {
                    // Insert a new record
                    var insertCommand = connection.CreateCommand();
                    insertCommand.CommandText = @"
                        INSERT INTO GilRecords (CharacterId, RetainerId, Gil, Timestamp)
                        VALUES (@CharacterId, @RetainerId, @Gil, @Timestamp);
                    ";
                    insertCommand.Parameters.AddWithValue("@CharacterId", characterId.ToString());
                    insertCommand.Parameters.AddWithValue("@RetainerId", retainerId == 0 ? (object)DBNull.Value : retainerId);
                    insertCommand.Parameters.AddWithValue("@Gil", gil);
                    insertCommand.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    int rowsInserted = insertCommand.ExecuteNonQuery();

//-                    DebuggerLog.Write($"SaveGilRecord inserted {rowsInserted} rows.");
                }

                transaction.Commit();
            }
        }
    }


    /// <summary>
    /// Daily aggregation of gil records
    /// </summary>
    internal void DoDailyAggregation()
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Aggregate daily records
                    var aggregateCommand = connection.CreateCommand();
                    aggregateCommand.CommandText = @"
                        INSERT INTO DailyGilSummary (CharacterId, RetainerId, TotalGil, Date)
                        SELECT CharacterId, RetainerId, SUM(Gil), DATE(Timestamp)
                        FROM GilRecords
                        GROUP BY CharacterId, RetainerId, DATE(Timestamp);
                    ";
                    int rowsAggregated = aggregateCommand.ExecuteNonQuery();

                    // Archive old records
                    var archiveCommand = connection.CreateCommand();
                        archiveCommand.CommandText = @"
                        INSERT INTO GilRecordsArchive (CharacterId, RetainerId, Gil, Timestamp)
                        SELECT CharacterId, RetainerId, Gil, Timestamp
                        FROM GilRecords;
                    ";
                    int rowsArchived = archiveCommand.ExecuteNonQuery();
                    //WHERE Timestamp < DATE('now', '-1 day');

                    // Delete old records
                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = @"
                        DELETE FROM GilRecords;
                    ";
                    int rowsDeleted = deleteCommand.ExecuteNonQuery();
                    //WHERE Timestamp < DATE('now', '-1 day');


                    //
                    // Clear FirstDailyGilRecords table
                    //
                    var truncateCommand = connection.CreateCommand();
                    truncateCommand.CommandText = "DELETE FROM FirstDailyGilRecords";
                    truncateCommand.ExecuteNonQuery();

                    transaction.Commit();

                    DebuggerLog.Write($"Aggregated {rowsAggregated} rows, archived {rowsArchived} rows, and deleted {rowsDeleted} rows. FirstDailyGilRecords table truncated.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    DebuggerLog.Write($"Error during daily aggregation: {ex.Message}");
                    throw;
                }
            }
        }
    }

    internal long CalculateCharacterGilIncome(Guid characterId, DateTime start, DateTime end)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            string query = @"
                SELECT SUM(Gil) FROM GilRecords
                WHERE CharacterId = @CharacterId AND RetainerId = 0 AND Timestamp BETWEEN @Start AND @End;
            ";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = query;
                command.Parameters.AddWithValue("@CharacterId", characterId.ToString());
                command.Parameters.AddWithValue("@Start", start.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@End", end.ToString("yyyy-MM-dd HH:mm:ss"));

                return (long)(command.ExecuteScalar() ?? 0);
            }
        }
    }

    internal long CalculateRetainerGilIncome(Guid characterId, ulong retainerId, DateTime start, DateTime end)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            string query = @"
                SELECT SUM(Gil) FROM GilRecords
                WHERE CharacterId = @CharacterId AND RetainerId = @RetainerId AND Timestamp BETWEEN @Start AND @End;
            ";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = query;
                command.Parameters.AddWithValue("@CharacterId", characterId.ToString());
                command.Parameters.AddWithValue("@RetainerId", retainerId);
                command.Parameters.AddWithValue("@Start", start.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@End", end.ToString("yyyy-MM-dd HH:mm:ss"));

                return (long)(command.ExecuteScalar() ?? 0);
            }
        }
    }

    internal long CalculateTotalGilIncome(Guid characterId, DateTime start, DateTime end)
    {
        CheckDatabaseFile();

        using (var connection = new SqliteConnection($"Data Source={DatabaseFile}"))
        {
            connection.Open();

            string query = @"
                SELECT SUM(Gil) FROM GilRecords
                WHERE CharacterId = @CharacterId AND RetainerId = 0 AND Timestamp BETWEEN @Start AND @End
                UNION ALL
                SELECT SUM(Gil) FROM GilRecords
                WHERE CharacterId = @CharacterId AND RetainerId != 0 AND Timestamp BETWEEN @Start AND @End;
            ";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = query;
                command.Parameters.AddWithValue("@CharacterId", characterId.ToString());
                command.Parameters.AddWithValue("@Start", start.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@End", end.ToString("yyyy-MM-dd HH:mm:ss"));

                long totalGil = 0;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        totalGil += reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    }
                }

                return totalGil;
            }
        }
    }

    internal long CalculateGilIncomeSinceMidnight(Guid characterId)
    {
        using var connection = new SqliteConnection($"Data Source={DatabaseFile}");
        connection.Open();

        // Get the first gil record of the day
        var firstGilCommand = connection.CreateCommand();
        firstGilCommand.CommandText = @"
            SELECT SUM(Gil)
            FROM FirstDailyGilRecords
            WHERE CharacterId = $characterId
            AND DATE(Timestamp) = DATE('now')";
        firstGilCommand.Parameters.AddWithValue("$characterId", characterId.ToString());

        var firstGilAmount = firstGilCommand.ExecuteScalar();
        long firstGil = firstGilAmount != DBNull.Value ? Convert.ToInt64(firstGilAmount) : 0;


        // Get the current gil amount (including retainers)
        var currentGilCommand = connection.CreateCommand();
        currentGilCommand.CommandText = @"
            SELECT SUM(Gil)
            FROM GilRecords
            WHERE CharacterId = $characterId";
        currentGilCommand.Parameters.AddWithValue("$characterId", characterId.ToString());

        var currentGilAmount = currentGilCommand.ExecuteScalar();
        long currentGil = currentGilAmount != DBNull.Value ? Convert.ToInt64(currentGilAmount) : 0;


        DebuggerLog.Write($"[CalculateGilIncomeSinceMidnight] FirstDailyGilRecords gil: {MiscHelpers.FormatNumber(firstGil)}, Current GilRecords gil: {MiscHelpers.FormatNumber(currentGil)}");

        // Calculate the gil income since midnight
        return currentGil - firstGil;
    }

    #endregion

    #region *** IDisposable ***

    private bool disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Dispose managed resources here.
            }

            // Dispose unmanaged resources here.

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    ~GilStorageManager()
    {
        Dispose(disposing: false);
    }

    #endregion
}

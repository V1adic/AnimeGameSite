using Microsoft.Data.Sqlite;
using System.Numerics;
using System;
using System.Collections.Generic;

namespace DemoSRP
{
    public class UserDatabase
    {
        private readonly string _connectionString;

        public UserDatabase(string dbPath = "users.db")
        {
            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Username TEXT PRIMARY KEY,
                        Salt TEXT NOT NULL,
                        Verifier TEXT NOT NULL,
                        Role TEXT NOT NULL DEFAULT 'User' CHECK(Role IN ('User', 'Donator', 'Admin'))
                    );

                    CREATE TABLE IF NOT EXISTS Logs (
                        LogId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL,
                        OldRole TEXT,
                        NewRole TEXT,
                        ChangeDate TEXT NOT NULL DEFAULT (datetime('now')),
                        FOREIGN KEY (Username) REFERENCES Users(Username)
                    );

                    CREATE TABLE IF NOT EXISTS AuthLogs (
                        LogId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL,
                        EventType TEXT NOT NULL CHECK(EventType IN ('Login', 'Logout')),
                        EventDate TEXT NOT NULL DEFAULT (datetime('now')),
                        FOREIGN KEY (Username) REFERENCES Users(Username)
                    );

                    CREATE TABLE IF NOT EXISTS Posts (
                        PostId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Content TEXT NOT NULL,
                        PhotoPaths TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                    );

                    CREATE TRIGGER IF NOT EXISTS LogRoleChange
                    AFTER UPDATE OF Role ON Users
                    FOR EACH ROW
                    WHEN OLD.Role != NEW.Role
                    BEGIN
                        INSERT INTO Logs (Username, OldRole, NewRole)
                        VALUES (NEW.Username, OLD.Role, NEW.Role);
                    END;";
            command.ExecuteNonQuery();
        }

        public bool UserExists(string username)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT COUNT(*) 
                    FROM Users 
                    WHERE Username = @username";
            command.Parameters.AddWithValue("@username", username);

            var count = (long)command.ExecuteScalar();
            return count > 0;
        }

        public void RegisterUser(string username, BigInteger salt, BigInteger verifier)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            if (UserExists(username))
            {
                throw new InvalidOperationException("Пользователь с таким именем уже существует.");
            }

            var command = connection.CreateCommand();
            command.CommandText = @"
                    INSERT INTO Users (Username, Salt, Verifier, Role)
                    VALUES (@username, @salt, @verifier, 'User')";
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@salt", salt.ToString());
            command.Parameters.AddWithValue("@verifier", verifier.ToString());

            try
            {
                command.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
            {
                throw new InvalidOperationException("Пользователь с таким именем уже существует.");
            }
        }

        public (BigInteger Salt, BigInteger Verifier, string Role)? GetUserData(string username)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT Salt, Verifier, Role
                    FROM Users
                    WHERE Username = @username";
            command.Parameters.AddWithValue("@username", username);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var salt = BigInteger.Parse(reader.GetString(0));
                var verifier = BigInteger.Parse(reader.GetString(1));
                var role = reader.GetString(2);
                return (salt, verifier, role);
            }
            return null;
        }

        public void UpdateUserRole(string username, string newRole)
        {
            if (!new[] { "User", "Donator", "Admin" }.Contains(newRole))
            {
                throw new ArgumentException("Указана недопустимая роль.");
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                    UPDATE Users
                    SET Role = @newRole
                    WHERE Username = @username";
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@newRole", newRole);

            var rowsAffected = command.ExecuteNonQuery();
            if (rowsAffected == 0)
            {
                throw new InvalidOperationException("Пользователь не найден.");
            }
        }

        public List<(long LogId, string Username, string OldRole, string NewRole, string ChangeDate)> GetRoleChangeLogs()
        {
            var logs = new List<(long, string, string, string, string)>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT LogId, Username, OldRole, NewRole, ChangeDate
                    FROM Logs
                    ORDER BY ChangeDate DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                logs.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4)
                ));
            }
            return logs;
        }

        public void LogAuthEvent(string username, string eventType)
        {
            if (!new[] { "Login", "Logout" }.Contains(eventType))
            {
                throw new ArgumentException("Указан недопустимый тип события.");
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                    INSERT INTO AuthLogs (Username, EventType, EventDate)
                    VALUES (@username, @eventType, @eventDate)";
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@eventType", eventType);
            command.Parameters.AddWithValue("@eventDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            command.ExecuteNonQuery();
        }

        public List<(long LogId, string Username, string EventType, string EventDate)> GetAuthLogs()
        {
            var logs = new List<(long, string, string, string)>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT LogId, Username, EventType, EventDate
                    FROM AuthLogs
                    ORDER BY EventDate DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                logs.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)
                ));
            }
            return logs;
        }

        public long CreatePost(string content, string photoPaths)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                    INSERT INTO Posts (Content, PhotoPaths, CreatedAt)
                    VALUES (@content, @photoPaths, @createdAt);
                    SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@content", content);
            command.Parameters.AddWithValue("@photoPaths", photoPaths);
            command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            return (long)command.ExecuteScalar();
        }

        public List<(long PostId, string Content, string PhotoPaths, string CreatedAt)> GetPosts()
        {
            var posts = new List<(long, string, string, string)>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT PostId, Content, PhotoPaths, CreatedAt
                    FROM Posts
                    ORDER BY CreatedAt DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                posts.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)
                ));
            }
            return posts;
        }

        public void DeletePostsRange()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM Posts
                WHERE PostId BETWEEN 4 AND 8";
            command.ExecuteNonQuery();
        }
    }
}
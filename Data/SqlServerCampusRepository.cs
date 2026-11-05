using Microsoft.Data.SqlClient;

namespace SmartCampus;





public sealed class SqlServerCampusRepository : ICampusRepository
{
    private readonly string _connectionString;

    public SqlServerCampusRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString));

        _connectionString = connectionString;
    }

    public void EnsureConnection()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
    }

    public IReadOnlyList<Student> GetStudents()
    {
        const string sql = @"
SELECT Id, Name, Department, Email, [Year], [Status]
FROM dbo.Students
ORDER BY Id;";

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        connection.Open();
        using var reader = command.ExecuteReader();

        var students = new List<Student>();
        while (reader.Read())
        {
            students.Add(new Student
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Department = reader.GetString(2),
                Email = reader.GetString(3),
                Year = reader.GetString(4),
                Status = reader.GetString(5)
            });
        }

        return students;
    }

    public void Save(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);

        const string sql = @"
IF EXISTS (SELECT 1 FROM dbo.Students WHERE Id = @Id)
BEGIN
    UPDATE dbo.Students
    SET Name = @Name, Department = @Department, Email = @Email,
        [Year] = @Year, [Status] = @Status
    WHERE Id = @Id;
END
ELSE
BEGIN
    INSERT INTO dbo.Students (Id, Name, Department, Email, [Year], [Status])
    VALUES (@Id, @Name, @Department, @Email, @Year, @Status);
END;";

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        AddStudentParameters(command, student);
        connection.Open();
        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("DELETE FROM dbo.Students WHERE Id = @Id;", connection);
        command.Parameters.AddWithValue("@Id", id);
        connection.Open();
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AttendanceRecord> GetAttendance()
    {
        const string sql = @"
SELECT Student, Course, [Time], [Status]
FROM dbo.Attendance
ORDER BY AttendanceId DESC;";

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        connection.Open();
        using var reader = command.ExecuteReader();

        var records = new List<AttendanceRecord>();
        while (reader.Read())
        {
            records.Add(new AttendanceRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return records;
    }

    public IReadOnlyList<CampusRoom> GetRooms()
    {
        const string sql = @"
SELECT Name, Building, Capacity, [State]
FROM dbo.Rooms
ORDER BY Name;";

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        connection.Open();
        using var reader = command.ExecuteReader();

        var rooms = new List<CampusRoom>();
        while (reader.Read())
        {
            rooms.Add(new CampusRoom(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3)));
        }

        return rooms;
    }

    public void SaveRoom(CampusRoom room)
    {
        ArgumentNullException.ThrowIfNull(room);

        const string sql = @"
IF EXISTS (SELECT 1 FROM dbo.Rooms WHERE Name = @Name)
BEGIN
    UPDATE dbo.Rooms
    SET Building = @Building, Capacity = @Capacity, [State] = @State
    WHERE Name = @Name;
END
ELSE
BEGIN
    INSERT INTO dbo.Rooms (Name, Building, Capacity, [State])
    VALUES (@Name, @Building, @Capacity, @State);
END;";

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", room.Name);
        command.Parameters.AddWithValue("@Building", room.Building);
        command.Parameters.AddWithValue("@Capacity", room.Capacity);
        command.Parameters.AddWithValue("@State", room.State);
        connection.Open();
        command.ExecuteNonQuery();
    }

    public void DeleteRoom(string name)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("DELETE FROM dbo.Rooms WHERE Name = @Name;", connection);
        command.Parameters.AddWithValue("@Name", name);
        connection.Open();
        command.ExecuteNonQuery();
    }

    private static void AddStudentParameters(SqlCommand command, Student student)
    {
        command.Parameters.AddWithValue("@Id", student.Id);
        command.Parameters.AddWithValue("@Name", student.Name);
        command.Parameters.AddWithValue("@Department", student.Department);
        command.Parameters.AddWithValue("@Email", student.Email);
        command.Parameters.AddWithValue("@Year", student.Year);
        command.Parameters.AddWithValue("@Status", student.Status);
    }
}

namespace SmartCampus;

public interface ICampusRepository
{
    IReadOnlyList<Student> GetStudents();
    void Save(Student student);
    void Delete(int id);
    IReadOnlyList<AttendanceRecord> GetAttendance();
    IReadOnlyList<CampusRoom> GetRooms();
    void SaveRoom(CampusRoom room);
    void DeleteRoom(string name);
}

public sealed class Student
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Department { get; set; } = "";
    public string Email { get; set; } = "";
    public string Year { get; set; } = "";
    public string Status { get; set; } = "Active";
}

public sealed record AttendanceRecord(string Student, string Course, string Time, string Status);
public sealed record CampusRoom(string Name, string Building, int Capacity, string State);
public sealed record CampusNotice(string Title, string Message, DateTime CreatedAt);

public sealed class DemoCampusRepository : ICampusRepository
{
    private readonly object _sync = new();
    private readonly List<Student> _students =
    [
        new() { Id = 24001, Name = "Mariam Hassan", Department = "Computer Science", Email = "mariam.h@must.edu.eg", Year = "Year 3", Status = "Active" },
        new() { Id = 24002, Name = "Omar Khaled", Department = "Information Systems", Email = "omar.k@must.edu.eg", Year = "Year 2", Status = "Active" },
        new() { Id = 24003, Name = "Nour Ahmed", Department = "Computer Science", Email = "nour.a@must.edu.eg", Year = "Year 4", Status = "Active" },
        new() { Id = 24004, Name = "Youssef Ali", Department = "Cybersecurity", Email = "youssef.a@must.edu.eg", Year = "Year 1", Status = "On leave" },
        new() { Id = 24005, Name = "Salma Mostafa", Department = "Information Systems", Email = "salma.m@must.edu.eg", Year = "Year 3", Status = "Active" },
        new() { Id = 24006, Name = "Adam Mahmoud", Department = "Computer Science", Email = "adam.m@must.edu.eg", Year = "Year 2", Status = "Active" },
        new() { Id = 24007, Name = "Laila Samir", Department = "Cybersecurity", Email = "laila.s@must.edu.eg", Year = "Year 4", Status = "Active" },
        new() { Id = 24008, Name = "Ziad Tarek", Department = "Computer Science", Email = "ziad.t@must.edu.eg", Year = "Year 1", Status = "Active" }
    ];
    private readonly List<AttendanceRecord> _attendance =
    [
        new("Mariam Hassan", "C# Programming", "09:04 AM", "Present"),
        new("Omar Khaled", "Database Systems", "09:12 AM", "Late"),
        new("Nour Ahmed", "Computer Networks", "09:00 AM", "Present"),
        new("Youssef Ali", "C# Programming", "—", "Absent"),
        new("Salma Mostafa", "Database Systems", "09:02 AM", "Present")
    ];
    private readonly List<CampusRoom> _rooms =
    [
        new("Lab 204", "Engineering · Floor 2", 32, "In use"),
        new("Lecture Hall A", "Main building · Floor 1", 120, "Available"),
        new("Lab 108", "Engineering · Floor 1", 28, "In use"),
        new("Room 301", "Main building · Floor 3", 45, "Available")
    ];
    public IReadOnlyList<Student> GetStudents() { lock (_sync) return _students.Select(x => new Student { Id = x.Id, Name = x.Name, Department = x.Department, Email = x.Email, Year = x.Year, Status = x.Status }).OrderBy(x => x.Id).ToList(); }
    public IReadOnlyList<AttendanceRecord> GetAttendance() => _attendance;
    public IReadOnlyList<CampusRoom> GetRooms() => _rooms;
    public void SaveRoom(CampusRoom room)
    {
        lock (_sync)
        {
            var index = _rooms.FindIndex(x => x.Name.Equals(room.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) _rooms[index] = room;
            else _rooms.Add(room);
        }
    }
    public void DeleteRoom(string name) { lock (_sync) _rooms.RemoveAll(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); }
    public void Save(Student student)
    {
        lock (_sync)
        {
            var existing = _students.FirstOrDefault(x => x.Id == student.Id);
            if (existing is null) _students.Add(student);
            else { existing.Name = student.Name; existing.Department = student.Department; existing.Email = student.Email; existing.Year = student.Year; existing.Status = student.Status; }
        }
    }
    public void Delete(int id) { lock (_sync) _students.RemoveAll(x => x.Id == id); }
}

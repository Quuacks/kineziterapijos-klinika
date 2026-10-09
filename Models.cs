using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Hospital;

public class Pacient
{
    public ObjectId Id { get; set; }
    public string Name { get; set; } = "";

    [BsonDateTimeOptions(DateOnly = true)]
    public DateTime BirthDate { get; set; }

    public List<string> ContactInfo { get; set; } = [];
    public List<MedicalNote> MedicalNotes { get; set; } = [];
}

public class MedicalNote
{
    public string Diagnosis { get; set; } = "";
    public string Notes { get; set; } = "";
}

public class Kineziterapeutas
{
    public ObjectId Id { get; set; }
    public string Name { get; set; } = "";
    public string Specialization { get; set; } = "";
    public List<WorkLocation> WorkLocations { get; set; } = [];
}

public class WorkLocation
{
    public string Address { get; set; } = "";
    public string Room { get; set; } = "";
}

public class Visit
{
    public ObjectId Id { get; set; }
    public string PacientName { get; set; } = "";
    public string DoctorName { get; set; } = "";

    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    public DateTime VisitDate { get; set; }

    public string Diagnosis { get; set; } = "";
    public int TimeTakenMins { get; set; }
    public List<ProcedureDone> ProceduresDone { get; set; } = [];

    [BsonIgnore]
    public DateTime EndDate => VisitDate.AddMinutes(TimeTakenMins);
}

public class ProcedureDone
{
    public string ProcedureName { get; set; } = "";
    public int ProcedureTimeMins { get; set; }
    public int ProcedurePrice { get; set; }
}

// Ataskaitų rezultatai (nesaugomi duomenų bazėje).

public class DoctorWorkload
{
    public string DoctorName { get; set; } = "";
    public int VisitCount { get; set; }
    public int TotalMinutes { get; set; }
}

public class ProcedureStats
{
    [BsonId]
    public string ProcedureName { get; set; } = "";
    public int Count { get; set; }
    public long TotalMinutes { get; set; }
    public long TotalRevenue { get; set; }
}

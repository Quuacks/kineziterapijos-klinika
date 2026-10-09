using MongoDB.Bson;
using MongoDB.Driver;

namespace Hospital;

public class ClinicService(ClinicDatabase db)
{
    // ---------- Pacientai ----------

    public List<Pacient> GetPacients() =>
        db.Pacients.Find(FilterDefinition<Pacient>.Empty).SortBy(p => p.Name).ToList();

    public void AddPacient(Pacient pacient) => db.Pacients.InsertOne(pacient);

    public void AddMedicalNote(Pacient pacient, MedicalNote note) =>
        db.Pacients.UpdateOne(p => p.Id == pacient.Id, Builders<Pacient>.Update.Push(p => p.MedicalNotes, note));

    public long CountPacientVisits(string pacientName) =>
        db.Visits.CountDocuments(v => v.PacientName == pacientName);

    public void DeletePacient(Pacient pacient, bool withVisits)
    {
        db.Pacients.DeleteOne(p => p.Id == pacient.Id);
        // Vizitai susieti vardu, todėl jų netriname, jei liko kitas pacientas tuo pačiu vardu.
        if (withVisits && db.Pacients.CountDocuments(p => p.Name == pacient.Name) == 0)
            db.Visits.DeleteMany(v => v.PacientName == pacient.Name);
    }

    // ---------- Kineziterapeutai ----------

    public List<Kineziterapeutas> GetDoctors() =>
        db.Kineziterapeutai.Find(FilterDefinition<Kineziterapeutas>.Empty).SortBy(d => d.Name).ToList();

    public void AddDoctor(Kineziterapeutas doctor) => db.Kineziterapeutai.InsertOne(doctor);

    public long CountDoctorVisits(string doctorName) =>
        db.Visits.CountDocuments(v => v.DoctorName == doctorName);

    public void DeleteDoctor(Kineziterapeutas doctor, bool withVisits)
    {
        db.Kineziterapeutai.DeleteOne(d => d.Id == doctor.Id);
        if (withVisits && db.Kineziterapeutai.CountDocuments(d => d.Name == doctor.Name) == 0)
            db.Visits.DeleteMany(v => v.DoctorName == doctor.Name);
    }

    // ---------- Vizitai ----------

    // Indeksas: pacientName_1_visitDate_1
    public List<Visit> GetPacientHistory(string pacientName) =>
        db.Visits.Find(v => v.PacientName == pacientName).SortBy(v => v.VisitDate).ToList();

    // Indeksas: doctorName_1_visitDate_1
    public List<Visit> GetDoctorSchedule(string doctorName, DateTime from, DateTime to) =>
        db.Visits.Find(v => v.DoctorName == doctorName && v.VisitDate >= from && v.VisitDate < to)
            .SortBy(v => v.VisitDate).ToList();

    // Indeksas: visitDate_1
    public List<Visit> GetVisits(DateTime from, DateTime to) =>
        db.Visits.Find(v => v.VisitDate >= from && v.VisitDate < to).SortBy(v => v.VisitDate).ToList();

    /// <summary>
    /// Grąžina jau užregistruotą vizitą, kurio laikas persidengia su naujuoju
    /// (tas pats kineziterapeutas arba tas pats pacientas), arba null, jei laikas laisvas.
    /// </summary>
    public Visit? FindConflict(Visit visit)
    {
        var dayStart = visit.VisitDate.Date;
        var dayEnd = dayStart.AddDays(1);
        var sameDay = GetDoctorSchedule(visit.DoctorName, dayStart, dayEnd).Concat(
            db.Visits.Find(v => v.PacientName == visit.PacientName && v.VisitDate >= dayStart && v.VisitDate < dayEnd)
                .ToList());
        return sameDay.FirstOrDefault(v => v.VisitDate < visit.EndDate && visit.VisitDate < v.EndDate);
    }

    public void AddVisit(Visit visit) => db.Visits.InsertOne(visit);

    public void DeleteVisit(Visit visit) => db.Visits.DeleteOne(v => v.Id == visit.Id);

    // ---------- Procedūros ----------

    // Indeksas: proceduresDone.procedureName_1
    public List<string> GetKnownProcedureNames() =>
        db.Visits.Distinct<string>("proceduresDone.procedureName", FilterDefinition<Visit>.Empty)
            .ToList().Order().ToList();

    /// <summary>Paskutinį kartą naudota procedūros trukmė ir kaina (naudojama kaip numatytoji reikšmė).</summary>
    public ProcedureDone? GetLastProcedureUsage(string procedureName)
    {
        var visit = db.Visits.Find(Builders<Visit>.Filter.Eq("proceduresDone.procedureName", procedureName))
            .SortByDescending(v => v.VisitDate).Limit(1).FirstOrDefault();
        return visit?.ProceduresDone.First(p => p.ProcedureName == procedureName);
    }

    // ---------- Ataskaitos ----------

    /// <summary>Vizitų skaičius ir bendra trukmė kiekvienam kineziterapeutui per laikotarpį [from, to).</summary>
    public List<DoctorWorkload> GetWorkload(DateTime from, DateTime to)
    {
        var rows = db.Visits.Aggregate()
            .Match(v => v.VisitDate >= from && v.VisitDate < to)
            .Group(v => v.DoctorName, g => new DoctorWorkload
            {
                DoctorName = g.Key,
                VisitCount = g.Count(),
                TotalMinutes = g.Sum(v => v.TimeTakenMins),
            })
            .ToList();

        // Kineziterapeutai be vizitų taip pat rodomi (su nuliais).
        var doctorNames = GetDoctors().Select(d => d.Name).Distinct().ToList();
        return doctorNames
            .Select(name => rows.FirstOrDefault(r => r.DoctorName == name) ?? new DoctorWorkload { DoctorName = name })
            .Concat(rows.Where(r => !doctorNames.Contains(r.DoctorName)))
            .OrderByDescending(r => r.TotalMinutes)
            .ToList();
    }

    /// <summary>Procedūros pagal atlikimų skaičių. Jei laikotarpis nenurodytas – per visą laiką.</summary>
    public List<ProcedureStats> GetPopularProcedures(DateTime? from, DateTime? to)
    {
        var pipeline = db.Visits.Aggregate();
        if (from is { } f && to is { } t)
            pipeline = pipeline.Match(v => v.VisitDate >= f && v.VisitDate < t);

        return pipeline
            .Unwind(v => v.ProceduresDone)
            .Group(new BsonDocument
            {
                { "_id", "$proceduresDone.procedureName" },
                { "count", new BsonDocument("$sum", 1) },
                { "totalMinutes", new BsonDocument("$sum", "$proceduresDone.procedureTimeMins") },
                { "totalRevenue", new BsonDocument("$sum", "$proceduresDone.procedurePrice") },
            })
            .Sort(new BsonDocument { { "count", -1 }, { "_id", 1 } })
            .As<ProcedureStats>()
            .ToList();
    }
}

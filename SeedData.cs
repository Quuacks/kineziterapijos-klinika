using MongoDB.Driver;

namespace Hospital;

/// <summary>Demonstraciniai duomenys: vizitai per paskutines 3 savaites ir suplanuoti ateinančiai savaitei.</summary>
public static class SeedData
{
    // Svoris nurodo, kaip dažnai procedūra skiriama (kad ataskaitoje būtų aiškūs lyderiai).
    private static readonly (string Name, int Mins, int Price, int Weight)[] Procedures =
    [
        ("Kineziterapija", 30, 35, 6),
        ("Masažas", 30, 30, 5),
        ("Elektroterapija", 15, 15, 3),
        ("Ultragarso terapija", 15, 20, 2),
        ("Kinezioteipavimas", 15, 15, 2),
        ("Lazerio terapija", 10, 20, 1),
        ("Hidroterapija", 30, 30, 1),
        ("Tempimo terapija", 20, 25, 1),
    ];

    // Vizitų pradžios laikai; tarpas 90 min., nes ilgiausias vizitas (3 procedūros) trunka iki 90 min.
    private static readonly TimeSpan[] Slots =
        [new(8, 0, 0), new(9, 30, 0), new(11, 0, 0), new(12, 30, 0), new(14, 0, 0), new(15, 30, 0)];

    public static (int Pacients, int Doctors, int Visits) Load(ClinicDatabase db)
    {
        db.Visits.DeleteMany(FilterDefinition<Visit>.Empty);
        db.Pacients.DeleteMany(FilterDefinition<Pacient>.Empty);
        db.Kineziterapeutai.DeleteMany(FilterDefinition<Kineziterapeutas>.Empty);

        var pacients = new List<Pacient>
        {
            NewPacient("Jonas Petraitis", 1975, 3, 12, ["+37061234567", "jonas.petraitis@example.com"],
                ("Juosmens išvarža", "Skausmas plinta į kairę koją, vengti sunkių kėlimų.")),
            NewPacient("Ona Kazlauskienė", 1958, 7, 2, ["+37062345678"],
                ("Kelio sąnario artrozė", "Dešinys kelis, ribotas lenkimas."),
                ("Osteoporozė", "Atsargiai su apkrovomis.")),
            NewPacient("Tomas Jankauskas", 1992, 11, 23, ["+37063456789", "tomas.j@example.com"],
                ("Peties raiščių patempimas", "Sportinė trauma (krepšinis).")),
            NewPacient("Rūta Vasiliauskienė", 1985, 5, 8, ["+37064567890", "Vilniaus g. 5, Vilnius"],
                ("Kaklo osteochondrozė", "Sėdimas darbas, galvos skausmai.")),
            NewPacient("Mantas Stankevičius", 2001, 1, 30, ["+37065678901"],
                ("Priekinio kryžminio raiščio plyšimas", "Po operacijos, reabilitacija 6 mėn.")),
            NewPacient("Eglė Butkutė", 1969, 9, 14, ["egle.butkute@example.com"],
                ("Skoliozė", "II laipsnio krūtinės ląstos skoliozė.")),
            NewPacient("Paulius Žukauskas", 1980, 4, 4, ["+37067890123"],
                ("Achilo sausgyslės uždegimas", "Bėgimas, kairė koja.")),
        };

        var doctors = new List<Kineziterapeutas>
        {
            NewDoctor("Laura Mickevičienė", "Sportinė kineziterapija",
                ("Gedimino pr. 10, Vilnius", "101"), ("Savanorių pr. 25, Vilnius", "12")),
            NewDoctor("Andrius Paulauskas", "Ortopedinė reabilitacija", ("Gedimino pr. 10, Vilnius", "102")),
            NewDoctor("Inga Rimkutė", "Neurologinė reabilitacija", ("Savanorių pr. 25, Vilnius", "14")),
            NewDoctor("Darius Kavaliauskas", "Manualinė terapija", ("Gedimino pr. 10, Vilnius", "103")),
        };

        db.Pacients.InsertMany(pacients);
        db.Kineziterapeutai.InsertMany(doctors);

        var random = new Random(42);
        var weightedProcedures = Procedures.SelectMany(p => Enumerable.Repeat(p, p.Weight)).ToList();
        var visits = new List<Visit>();

        for (var day = DateTime.Today.AddDays(-21); day <= DateTime.Today.AddDays(7); day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            // Tas pats pacientas negali būti pas du kineziterapeutus tuo pačiu metu.
            var busyPacients = new HashSet<(TimeSpan, string)>();
            foreach (var doctor in doctors)
            {
                var slots = Slots.OrderBy(_ => random.Next()).Take(random.Next(2, 6)).Order();
                foreach (var slot in slots)
                {
                    Pacient pacient;
                    do pacient = pacients[random.Next(pacients.Count)];
                    while (!busyPacients.Add((slot, pacient.Name)));

                    var procedures = weightedProcedures
                        .OrderBy(_ => random.Next())
                        .Distinct()
                        .Take(random.Next(1, 4))
                        .Select(p => new ProcedureDone { ProcedureName = p.Name, ProcedureTimeMins = p.Mins, ProcedurePrice = p.Price })
                        .ToList();

                    visits.Add(new Visit
                    {
                        PacientName = pacient.Name,
                        DoctorName = doctor.Name,
                        VisitDate = day + slot,
                        Diagnosis = pacient.MedicalNotes[random.Next(pacient.MedicalNotes.Count)].Diagnosis,
                        TimeTakenMins = procedures.Sum(p => p.ProcedureTimeMins),
                        ProceduresDone = procedures,
                    });
                }
            }
        }

        db.Visits.InsertMany(visits);
        return (pacients.Count, doctors.Count, visits.Count);
    }

    private static Pacient NewPacient(string name, int year, int month, int day, List<string> contacts,
        params (string Diagnosis, string Notes)[] notes) => new()
    {
        Name = name,
        BirthDate = new DateTime(year, month, day),
        ContactInfo = contacts,
        MedicalNotes = notes.Select(n => new MedicalNote { Diagnosis = n.Diagnosis, Notes = n.Notes }).ToList(),
    };

    private static Kineziterapeutas NewDoctor(string name, string specialization,
        params (string Address, string Room)[] locations) => new()
    {
        Name = name,
        Specialization = specialization,
        WorkLocations = locations.Select(l => new WorkLocation { Address = l.Address, Room = l.Room }).ToList(),
    };
}

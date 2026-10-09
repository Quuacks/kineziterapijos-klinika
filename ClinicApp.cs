using System.Globalization;
using MongoDB.Driver;

namespace Hospital;

public class ClinicApp(ClinicService service, ClinicDatabase database)
{
    // Darbo valandos, pagal kurias skaičiuojamas laisvas laikas ir užimtumas.
    private static readonly TimeSpan WorkDayStart = TimeSpan.FromHours(8);
    private static readonly TimeSpan WorkDayEnd = TimeSpan.FromHours(18);

    private static readonly CultureInfo Lt = new("lt-LT");

    public void Run() => Menu("Kineziterapijos klinika", "Išeiti",
        ("Pacientai", PacientsMenu),
        ("Kineziterapeutai", DoctorsMenu),
        ("Vizitai", VisitsMenu),
        ("Ataskaitos", ReportsMenu),
        ("Įkelti demonstracinius duomenis", LoadDemoData));

    private void PacientsMenu() => Menu("Pacientai", "Atgal",
        ("Pacientų sąrašas", ListPacients),
        ("Paciento kortelė ir vizitų istorija", ShowPacientCard),
        ("Pridėti pacientą", AddPacient),
        ("Pridėti medicininį įrašą", AddMedicalNote),
        ("Ištrinti pacientą", DeletePacient));

    private void DoctorsMenu() => Menu("Kineziterapeutai", "Atgal",
        ("Kineziterapeutų sąrašas", ListDoctors),
        ("Pridėti kineziterapeutą", AddDoctor),
        ("Ištrinti kineziterapeutą", DeleteDoctor));

    private void VisitsMenu() => Menu("Vizitai", "Atgal",
        ("Registruoti vizitą", RegisterVisit),
        ("Paciento vizitų istorija", ShowPacientCard),
        ("Kineziterapeuto tvarkaraštis", ShowDoctorSchedule),
        ("Dienos užimtumas ir laisvas laikas", ShowDayAvailability),
        ("Ištrinti vizitą", DeleteVisit));

    private void ReportsMenu() => Menu("Ataskaitos", "Atgal",
        ("Kineziterapeutų užimtumas per laikotarpį", ShowWorkload),
        ("Populiariausios procedūros", ShowPopularProcedures));

    private static void Menu(string title, string exitLabel, params (string Label, Action Action)[] items)
    {
        while (true)
        {
            Console.WriteLine($"\n=== {title} ===");
            for (var i = 0; i < items.Length; i++)
                Console.WriteLine($"{i + 1}. {items[i].Label}");
            Console.WriteLine($"0. {exitLabel}");

            var choice = Input.Int("Pasirinkimas", 0, items.Length);
            if (choice == 0) return;

            Console.WriteLine();
            try
            {
                items[choice - 1].Action();
            }
            catch (MongoException e)
            {
                Console.WriteLine($"Duomenų bazės klaida: {e.Message}");
            }
        }
    }

    // ---------- Pacientai ----------

    private void ListPacients()
    {
        var pacients = service.GetPacients();
        if (pacients.Count == 0)
        {
            Console.WriteLine("Pacientų nėra.");
            return;
        }

        Console.WriteLine($"{"Vardas",-28} {"Gim. data",-11} {"Kontaktai",-45} Įrašai");
        foreach (var p in pacients)
            Console.WriteLine($"{p.Name,-28} {p.BirthDate.ToString(Input.DateFormat),-11} {string.Join(", ", p.ContactInfo),-45} {p.MedicalNotes.Count}");
    }

    private void ShowPacientCard()
    {
        var pacient = ChoosePacient();
        if (pacient is null) return;

        Console.WriteLine($"\n{pacient.Name}, gim. {pacient.BirthDate.ToString(Input.DateFormat)}");
        Console.WriteLine($"Kontaktai: {(pacient.ContactInfo.Count == 0 ? "-" : string.Join(", ", pacient.ContactInfo))}");
        Console.WriteLine("Medicininiai įrašai:");
        if (pacient.MedicalNotes.Count == 0) Console.WriteLine("  -");
        foreach (var note in pacient.MedicalNotes)
            Console.WriteLine($"  • {note.Diagnosis}: {note.Notes}");

        var history = service.GetPacientHistory(pacient.Name);
        Console.WriteLine($"\nVizitų istorija ({history.Count}):");
        foreach (var v in history)
        {
            var marker = v.VisitDate > DateTime.Now ? " (suplanuotas)" : "";
            Console.WriteLine($"  {v.VisitDate.ToString(Input.DateTimeFormat)}{marker} | {v.DoctorName} | {v.Diagnosis} | {v.TimeTakenMins} min.");
            foreach (var p in v.ProceduresDone)
                Console.WriteLine($"      - {p.ProcedureName}, {p.ProcedureTimeMins} min., {p.ProcedurePrice} €");
        }

        var total = history.Sum(v => v.ProceduresDone.Sum(p => p.ProcedurePrice));
        Console.WriteLine($"Iš viso procedūrų už {total} €");
    }

    private void AddPacient()
    {
        var pacient = new Pacient
        {
            Name = Input.Text("Vardas ir pavardė"),
            BirthDate = Input.Date("Gimimo data"),
            ContactInfo = Input.Lines("Kontaktas (tel., el. paštas, adresas)"),
        };
        while (Input.Confirm("Pridėti medicininį įrašą?"))
            pacient.MedicalNotes.Add(ReadMedicalNote());

        service.AddPacient(pacient);
        Console.WriteLine("Pacientas pridėtas.");
    }

    private void AddMedicalNote()
    {
        var pacient = ChoosePacient();
        if (pacient is null) return;

        service.AddMedicalNote(pacient, ReadMedicalNote());
        Console.WriteLine("Įrašas pridėtas.");
    }

    private static MedicalNote ReadMedicalNote() => new()
    {
        Diagnosis = Input.Text("Diagnozė"),
        Notes = Input.Text("Pastabos", optional: true),
    };

    private void DeletePacient()
    {
        var pacient = ChoosePacient();
        if (pacient is null) return;

        var visitCount = service.CountPacientVisits(pacient.Name);
        if (!Input.Confirm($"Ištrinti pacientą {pacient.Name}?")) return;
        var withVisits = visitCount > 0 && Input.Confirm($"Ištrinti ir jo vizitus ({visitCount})?");

        service.DeletePacient(pacient, withVisits);
        Console.WriteLine("Pacientas ištrintas.");
    }

    // ---------- Kineziterapeutai ----------

    private void ListDoctors()
    {
        var doctors = service.GetDoctors();
        if (doctors.Count == 0)
        {
            Console.WriteLine("Kineziterapeutų nėra.");
            return;
        }

        foreach (var d in doctors)
        {
            Console.WriteLine($"{d.Name} – {d.Specialization}");
            foreach (var l in d.WorkLocations)
                Console.WriteLine($"    {l.Address}, kab. {l.Room}");
        }
    }

    private void AddDoctor()
    {
        var doctor = new Kineziterapeutas
        {
            Name = Input.Text("Vardas ir pavardė"),
            Specialization = Input.Text("Specializacija"),
        };
        do
        {
            doctor.WorkLocations.Add(new WorkLocation
            {
                Address = Input.Text("Darbo vietos adresas"),
                Room = Input.Text("Kabinetas"),
            });
        } while (Input.Confirm("Pridėti dar vieną darbo vietą?"));

        service.AddDoctor(doctor);
        Console.WriteLine("Kineziterapeutas pridėtas.");
    }

    private void DeleteDoctor()
    {
        var doctor = ChooseDoctor();
        if (doctor is null) return;

        var visitCount = service.CountDoctorVisits(doctor.Name);
        if (!Input.Confirm($"Ištrinti kineziterapeutą {doctor.Name}?")) return;
        var withVisits = visitCount > 0 && Input.Confirm($"Ištrinti ir jo vizitus ({visitCount})?");

        service.DeleteDoctor(doctor, withVisits);
        Console.WriteLine("Kineziterapeutas ištrintas.");
    }

    // ---------- Vizitai ----------

    private void RegisterVisit()
    {
        var pacient = ChoosePacient();
        if (pacient is null) return;
        var doctor = ChooseDoctor();
        if (doctor is null) return;

        var nextHour = DateTime.Today.AddHours(DateTime.Now.Hour + 1);
        var visitDate = Input.DateAndTime("Vizito pradžia", nextHour);
        var diagnosis = Input.Text("Diagnozė", pacient.MedicalNotes.LastOrDefault()?.Diagnosis);
        var procedures = ReadProcedures();
        var proceduresTime = procedures.Sum(p => p.ProcedureTimeMins);
        var duration = Input.Int("Vizito trukmė, min.", Math.Max(1, proceduresTime), 600,
            proceduresTime > 0 ? proceduresTime : 30);

        var visit = new Visit
        {
            PacientName = pacient.Name,
            DoctorName = doctor.Name,
            VisitDate = visitDate,
            Diagnosis = diagnosis,
            TimeTakenMins = duration,
            ProceduresDone = procedures,
        };

        if (service.FindConflict(visit) is { } conflict)
        {
            Console.WriteLine($"Laikas užimtas: {conflict.VisitDate.ToString(Input.DateTimeFormat)}–{conflict.EndDate:HH:mm} " +
                              $"jau yra vizitas ({conflict.PacientName} pas {conflict.DoctorName}). Vizitas neužregistruotas.");
            return;
        }

        service.AddVisit(visit);
        Console.WriteLine($"Vizitas užregistruotas: {visit.VisitDate.ToString(Input.DateTimeFormat)}–{visit.EndDate:HH:mm}.");
    }

    private List<ProcedureDone> ReadProcedures()
    {
        var known = service.GetKnownProcedureNames();
        if (known.Count > 0)
        {
            Console.WriteLine("Žinomos procedūros (galite įvesti numerį arba naują pavadinimą):");
            for (var i = 0; i < known.Count; i++)
                Console.WriteLine($"  {i + 1}. {known[i]}");
        }

        var procedures = new List<ProcedureDone>();
        while (Input.Text("Procedūra (Enter – baigti)", optional: true) is { Length: > 0 } text)
        {
            var name = int.TryParse(text, out var number) && number >= 1 && number <= known.Count
                ? known[number - 1]
                : text;
            var last = service.GetLastProcedureUsage(name);
            procedures.Add(new ProcedureDone
            {
                ProcedureName = name,
                ProcedureTimeMins = Input.Int($"  {name}: trukmė, min.", 1, 600, last?.ProcedureTimeMins),
                ProcedurePrice = Input.Int($"  {name}: kaina, €", 0, 100_000, last?.ProcedurePrice),
            });
        }
        return procedures;
    }

    private void ShowDoctorSchedule()
    {
        var doctor = ChooseDoctor();
        if (doctor is null) return;

        var from = Input.Date("Nuo", DateTime.Today);
        var to = Input.Date("Iki (imtinai)", from.AddDays(6));
        var visits = service.GetDoctorSchedule(doctor.Name, from, to.AddDays(1));

        Console.WriteLine($"\n{doctor.Name} tvarkaraštis {from.ToString(Input.DateFormat)} – {to.ToString(Input.DateFormat)}:");
        if (visits.Count == 0) Console.WriteLine("  Vizitų nėra.");
        foreach (var day in visits.GroupBy(v => v.VisitDate.Date))
        {
            Console.WriteLine($"\n{day.Key.ToString(Input.DateFormat)} ({DayName(day.Key)}), " +
                              $"užimta {FormatHours(day.Sum(v => v.TimeTakenMins))}");
            foreach (var v in day)
                Console.WriteLine($"  {v.VisitDate:HH:mm}–{v.EndDate:HH:mm}  {v.PacientName,-24} {v.Diagnosis} | " +
                                  string.Join(", ", v.ProceduresDone.Select(p => p.ProcedureName)));
        }
    }

    private void ShowDayAvailability()
    {
        var day = Input.Date("Diena", DateTime.Today);
        var visits = service.GetVisits(day, day.AddDays(1));
        Console.WriteLine($"\n{day.ToString(Input.DateFormat)} ({DayName(day)}), darbo laikas {WorkDayStart:hh\\:mm}–{WorkDayEnd:hh\\:mm}");

        foreach (var doctor in service.GetDoctors())
        {
            var own = visits.Where(v => v.DoctorName == doctor.Name).ToList();
            Console.WriteLine($"\n{doctor.Name} ({doctor.Specialization})");
            Console.WriteLine("  Užimta: " + (own.Count == 0
                ? "-"
                : string.Join(", ", own.Select(v => $"{v.VisitDate:HH:mm}–{v.EndDate:HH:mm}"))));

            var free = FreeSlots(day, own).ToList();
            Console.WriteLine("  Laisva: " + (free.Count == 0
                ? "-"
                : string.Join(", ", free.Select(s => $"{s.From:HH:mm}–{s.To:HH:mm}"))));
        }
    }

    /// <summary>Laisvi tarpai tarp vizitų darbo laiko ribose. Vizitai turi būti surikiuoti pagal laiką.</summary>
    private static IEnumerable<(DateTime From, DateTime To)> FreeSlots(DateTime day, List<Visit> visits)
    {
        var cursor = day.Date + WorkDayStart;
        var end = day.Date + WorkDayEnd;
        foreach (var v in visits)
        {
            if (cursor >= end) yield break;
            if (v.VisitDate > cursor)
                yield return (cursor, v.VisitDate < end ? v.VisitDate : end);
            if (v.EndDate > cursor)
                cursor = v.EndDate;
        }
        if (cursor < end)
            yield return (cursor, end);
    }

    private void DeleteVisit()
    {
        var pacient = ChoosePacient();
        if (pacient is null) return;

        var visit = Input.Choose("Paciento vizitai:", service.GetPacientHistory(pacient.Name),
            v => $"{v.VisitDate.ToString(Input.DateTimeFormat)} | {v.DoctorName} | {v.Diagnosis}");
        if (visit is null || !Input.Confirm("Ištrinti šį vizitą?")) return;

        service.DeleteVisit(visit);
        Console.WriteLine("Vizitas ištrintas.");
    }

    // ---------- Ataskaitos ----------

    private void ShowWorkload()
    {
        var from = Input.Date("Nuo", DateTime.Today.AddDays(-30));
        var to = Input.Date("Iki (imtinai)", DateTime.Today);
        var rows = service.GetWorkload(from, to.AddDays(1));

        var workDays = Enumerable.Range(0, (to - from).Days + 1)
            .Select(i => from.AddDays(i))
            .Count(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        var availableMinutes = workDays * (WorkDayEnd - WorkDayStart).TotalMinutes;

        Console.WriteLine($"\nLaikotarpis {from.ToString(Input.DateFormat)} – {to.ToString(Input.DateFormat)}: " +
                          $"{workDays} darbo d. po {(WorkDayEnd - WorkDayStart).TotalHours} val.");
        Console.WriteLine($"{"Kineziterapeutas",-26} {"Vizitai",8} {"Užimta",12} {"Vid. vizitas",13} {"Užimtumas",10}");
        foreach (var r in rows)
        {
            var average = r.VisitCount == 0 ? "-" : $"{r.TotalMinutes / r.VisitCount} min.";
            var load = availableMinutes == 0 ? "-" : $"{r.TotalMinutes / availableMinutes:P0}";
            Console.WriteLine($"{r.DoctorName,-26} {r.VisitCount,8} {FormatHours(r.TotalMinutes),12} {average,13} {load,10}");
        }
    }

    private void ShowPopularProcedures()
    {
        DateTime? from = null, to = null;
        if (Input.Confirm("Riboti laikotarpiu?"))
        {
            from = Input.Date("Nuo", DateTime.Today.AddDays(-30));
            to = Input.Date("Iki (imtinai)", DateTime.Today).AddDays(1);
        }

        var stats = service.GetPopularProcedures(from, to);
        if (stats.Count == 0)
        {
            Console.WriteLine("Procedūrų nerasta.");
            return;
        }

        var totalCount = stats.Sum(s => s.Count);
        Console.WriteLine($"\n{"#",3} {"Procedūra",-24} {"Kartai",7} {"Dalis",6} {"Laikas",12} {"Pajamos",9} {"Vid. kaina",10}");
        for (var i = 0; i < stats.Count; i++)
        {
            var s = stats[i];
            Console.WriteLine($"{i + 1,3} {s.ProcedureName,-24} {s.Count,7} {(double)s.Count / totalCount,6:P0} " +
                              $"{FormatHours(s.TotalMinutes),12} {s.TotalRevenue + " €",9} {s.TotalRevenue / s.Count + " €",10}");
        }
    }

    // ---------- Demonstraciniai duomenys ----------

    private void LoadDemoData()
    {
        if (!Input.Confirm("Visi esami duomenys bus ištrinti ir pakeisti demonstraciniais. Tęsti?")) return;
        var (pacients, doctors, visits) = SeedData.Load(database);
        Console.WriteLine($"Įkelta: {pacients} pacientai, {doctors} kineziterapeutai, {visits} vizitai.");
    }

    // ---------- Pagalbinės ----------

    private Pacient? ChoosePacient() =>
        Input.Choose("Pasirinkite pacientą:", service.GetPacients(), p => $"{p.Name} (gim. {p.BirthDate.Year})");

    private Kineziterapeutas? ChooseDoctor() =>
        Input.Choose("Pasirinkite kineziterapeutą:", service.GetDoctors(), d => $"{d.Name} – {d.Specialization}");

    private static string DayName(DateTime date) => Lt.DateTimeFormat.GetDayName(date.DayOfWeek);

    private static string FormatHours(long minutes) => $"{minutes / 60} h {minutes % 60:00} min";
}

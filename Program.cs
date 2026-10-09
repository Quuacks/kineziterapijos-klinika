using System.Globalization;
using System.Text;
using Hospital;

Console.OutputEncoding = Encoding.UTF8;
// Windows konsolėje UTF-16 įvestis leidžia teisingai nuskaityti lietuviškas raides (ą, č, ė...).
if (OperatingSystem.IsWindows() && !Console.IsInputRedirected)
    Console.InputEncoding = Encoding.Unicode;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var connectionString = Environment.GetEnvironmentVariable("MONGO_URL") ?? "mongodb://localhost:27017";
var database = new ClinicDatabase(connectionString);

try
{
    database.Ping();
}
catch (Exception e)
{
    Console.WriteLine($"Nepavyko prisijungti prie MongoDB ({connectionString}): {e.Message}");
    return;
}

if (!database.IsInitialized())
    Console.WriteLine("Įspėjimas: kolekcijos nesukurtos. Paleiskite: docker exec -i <konteineris> mongosh < db/init.js");

new ClinicApp(new ClinicService(database), database).Run();

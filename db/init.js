// Kineziterapijos klinikos duomenų bazės sukūrimas.
// Paleidimas: docker exec -i <container> mongosh < db/init.js

db = db.getSiblingDB("KineziterapijosKlinika");

for (const name of ["Pacients", "Kineziterapeutai", "Visits"]) {
  if (db.getCollectionNames().includes(name)) db.getCollection(name).drop();
}

db.createCollection("Pacients", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["name", "birthDate", "contactInfo", "medicalNotes"],
      properties: {
        name: { bsonType: "string" },
        birthDate: { bsonType: "date" },
        contactInfo: { bsonType: "array", items: { bsonType: "string" } },
        medicalNotes: {
          bsonType: "array",
          items: {
            bsonType: "object",
            required: ["diagnosis", "notes"],
            properties: {
              diagnosis: { bsonType: "string" },
              notes: { bsonType: "string" },
            },
          },
        },
      },
    },
  },
});

db.createCollection("Kineziterapeutai", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["name", "specialization", "workLocations"],
      properties: {
        name: { bsonType: "string" },
        specialization: { bsonType: "string" },
        workLocations: {
          bsonType: "array",
          items: {
            bsonType: "object",
            required: ["address", "room"],
            properties: {
              address: { bsonType: "string" },
              room: { bsonType: "string" },
            },
          },
        },
      },
    },
  },
});

db.createCollection("Visits", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["pacientName", "doctorName", "visitDate", "diagnosis", "timeTakenMins", "proceduresDone"],
      properties: {
        pacientName: { bsonType: "string" },
        doctorName: { bsonType: "string" },
        visitDate: { bsonType: "date" },
        diagnosis: { bsonType: "string" },
        timeTakenMins: { bsonType: "int", minimum: 0 },
        proceduresDone: {
          bsonType: "array",
          items: {
            bsonType: "object",
            required: ["procedureName", "procedureTimeMins", "procedurePrice"],
            properties: {
              procedureName: { bsonType: "string" },
              procedureTimeMins: { bsonType: "int", minimum: 0 },
              procedurePrice: { bsonType: "int", minimum: 0 },
            },
          },
        },
      },
    },
  },
});

// Visų gydytojų užimtumas / laisvas laikas pasirinktu laikotarpiu.
db.Visits.createIndex({ visitDate: 1 });
// Konkretaus kineziterapeuto tvarkaraštis ir užimtumas.
db.Visits.createIndex({ doctorName: 1, visitDate: 1 });
// Paciento vizitų istorija.
db.Visits.createIndex({ pacientName: 1, visitDate: 1 });
// Procedūrų paieška (multikey indeksas masyvo elementams).
db.Visits.createIndex({ "proceduresDone.procedureName": 1 });

printjson({
  collections: db.getCollectionNames(),
  pacientIndexes: db.Pacients.getIndexes().map((i) => i.name),
  kineziterapeutaiIndexes: db.Kineziterapeutai.getIndexes().map((i) => i.name),
  visitIndexes: db.Visits.getIndexes().map((i) => i.name),
});

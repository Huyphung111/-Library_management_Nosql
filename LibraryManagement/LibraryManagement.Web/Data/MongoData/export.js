// Xuất database library_management ra file JSON (Extended JSON dạng canonical, mỗi dòng một document).
// Chạy trong thư mục Data/MongoData:
//   mongosh "mongodb://localhost:27017/library_management?replicaSet=rs0" --file export.js
const fs = require("fs");
const path = require("path");

const outDir = process.cwd();
const collections = db.getCollectionNames().filter(c => !c.startsWith("system.")).sort();

for (const name of collections) {
  const lines = db.getCollection(name).find().sort({ _id: 1 }).toArray()
    .map(doc => EJSON.stringify(doc, { relaxed: false }));
  fs.writeFileSync(path.join(outDir, `${name}.json`), lines.join("\n") + (lines.length ? "\n" : ""), "utf8");
  print(`${name.padEnd(14)} ${lines.length} document`);
}
print(`Đã xuất ${collections.length} collection vào ${outDir}`);

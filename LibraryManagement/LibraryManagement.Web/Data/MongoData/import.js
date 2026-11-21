// Nhập dữ liệu mẫu từ các file *.json trong thư mục này vào database library_management.
// CẢNH BÁO: XÓA dữ liệu hiện có của các collection có file .json rồi mới nhập lại.
// Chạy trong thư mục Data/MongoData:
//   mongosh "mongodb://localhost:27017/library_management?replicaSet=rs0" --file import.js
// Index không nằm trong file: chạy ứng dụng một lần, DbInitializer sẽ tự tạo.
const fs = require("fs");
const path = require("path");

const dir = process.cwd();
const files = fs.readdirSync(dir).filter(f => f.endsWith(".json")).sort();
if (files.length === 0) throw new Error(`Không thấy file .json nào trong ${dir}. Hãy cd vào Data/MongoData trước.`);

for (const file of files) {
  const name = path.basename(file, ".json");
  const docs = fs.readFileSync(path.join(dir, file), "utf8")
    .split(/\r?\n/).filter(line => line.trim() !== "")
    .map(line => EJSON.parse(line, { relaxed: false }));

  db.getCollection(name).deleteMany({});
  if (docs.length) db.getCollection(name).insertMany(docs);
  print(`${name.padEnd(14)} ${docs.length} document`);
}
print(`Đã nhập ${files.length} collection vào database ${db.getName()}`);

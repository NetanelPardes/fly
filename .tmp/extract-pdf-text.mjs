import fs from "node:fs";
import zlib from "node:zlib";

const input = process.argv[2];
const bytes = fs.readFileSync(input);
const raw = bytes.toString("latin1");

function decodePdfString(value) {
  if (value.startsWith("<")) {
    const hex = value.slice(1, -1).replace(/\s+/g, "");
    const out = [];
    for (let i = 0; i < hex.length; i += 2) out.push(parseInt(hex.slice(i, i + 2), 16));
    const buf = Buffer.from(out);
    if (buf.length >= 2 && buf[0] === 0xfe && buf[1] === 0xff) return buf.subarray(2).toString("utf16le").split("").map((_, i, a) => i % 2 ? "" : String.fromCharCode((buf[i + 2] << 8) | buf[i + 3])).join("");
    return buf.toString("utf8");
  }

  let text = value.slice(1, -1);
  text = text.replace(/\\([nrtbf()\\])/g, (_, ch) => ({ n: "\n", r: "\r", t: "\t", b: "\b", f: "\f", "(": "(", ")": ")", "\\": "\\" }[ch]));
  text = text.replace(/\\([0-7]{1,3})/g, (_, oct) => String.fromCharCode(parseInt(oct, 8)));
  return text;
}

function extractTextFromStream(text) {
  const found = [];
  const token = /<([0-9A-Fa-f\s]+)>|\((?:\\.|[^\\)])*\)/g;
  for (const match of text.matchAll(token)) {
    const value = match[0];
    const decoded = decodePdfString(value).replace(/\s+/g, " ").trim();
    if (decoded && /[A-Za-z\u0590-\u05FF0-9]/.test(decoded)) found.push(decoded);
  }
  return found;
}

const pages = [];
const streams = [...raw.matchAll(/(\d+)\s+0\s+obj([\s\S]*?)stream\r?\n([\s\S]*?)\r?\nendstream/g)];
for (const match of streams) {
  const objectNumber = Number(match[1]);
  const dict = match[2];
  const streamRaw = Buffer.from(match[3], "latin1");
  let stream = streamRaw;
  if (dict.includes("/FlateDecode")) {
    try {
      stream = zlib.inflateSync(streamRaw);
    } catch {
      continue;
    }
  }
  const extracted = extractTextFromStream(stream.toString("latin1"));
  if (extracted.length) {
    pages.push({ objectNumber, text: extracted });
  }
}

for (const page of pages) {
  console.log(`\n--- object ${page.objectNumber} ---`);
  console.log([...new Set(page.text)].join("\n"));
}

import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const workspace = process.cwd();
const buildDir = path.join(workspace, ".tmp", "presentation-build", "pptx-src");
const finalPath = path.join(workspace, "output", "airport-management-system-presentation.pptx");
const logoPath = path.join(workspace, "Airport.Ui", "assets", "kodfly-logo.png");

const CX = 12192000;
const CY = 6858000;

const colors = {
  navy: "102338",
  blue: "0F5D9B",
  sky: "DCEFFB",
  mint: "DFF4EA",
  amber: "FFE8B3",
  coral: "FFD8CC",
  ink: "1D2B36",
  muted: "5E6D78",
  paper: "F8FAFC",
  white: "FFFFFF",
  line: "C7D3DD",
};

const slides = [
  {
    kind: "cover",
    title: "מערכת לניהול טיסות",
    subtitle: "מצגת קוד ותהליכי עבודה",
    body: "ביצוע: נתנאל פרדס ואבי רוזנגרטן",
  },
  {
    title: "תמונת מערכת",
    intro: "בשלב הראשון המערכת מחברת בין ממשק משתמש, API, מסד נתונים ותורי הודעות כדי לנהל טיסות וכרטיסים מקצה לקצה.",
    bullets: [
      "Airport.Ui מציג פורטל נוסע וקונסולת מנהל ב-HTML, CSS ו-JavaScript.",
      "Airport.Api מספק REST API עם הרשאות JWT, EF Core ו-MySQL.",
      "RabbitMQ מפעיל יצירת קובץ כרטיס לאחר הזמנה.",
      "Kafka משמש לעדכוני טיסות חיצוניים במצב אופציונלי.",
    ],
    flow: ["UI", "API", "MySQL", "RabbitMQ", "Ticket Worker"],
  },
  {
    title: "כניסה והרשאות",
    intro: "לפני כל פעולה המשתמש מזדהה, והשרת מחזיר JWT שמגדיר אילו מסכים ופעולות זמינים עבורו.",
    bullets: [
      "מסך לקוח מאפשר התחברות ורישום נוסע חדש.",
      "מסך מנהל מפנה לקונסולת התפעול.",
      "התפקידים Admin, Ticketing, Baggage ו-Passenger משפיעים על הרשאות API.",
      "ה-UI שומר את פרטי המשתמש והטוקן ומשתמש בהם בכל קריאת API.",
    ],
    process: ["כניסה", "JWT", "בדיקת תפקיד", "ניווט למסכים"],
  },
  {
    title: "חיפוש טיסות בפורטל הנוסע",
    intro: "הנוסע מתחיל בחיפוש מסלול לפי תאריך, מוצא טיסות זמינות ומשווה מחיר בסיסי במטבע נבחר.",
    bullets: [
      "המסך מציג מדדים: מספר טיסות, יעדים, ימי נסיעה ומחיר נמוך.",
      "הסינון כולל תאריכים, מוצא, יעד ומיון לפי תאריך או מחיר.",
      "שירות המטבע ממיר מחירים מהשרת, והדפדפן נשאר מנותק מספק המטבע החיצוני.",
      "חלון פרטים מציג נתוני טיסה לפני מעבר להזמנה.",
    ],
    process: ["פילטרים", "תוצאות", "פרטי טיסה", "המשך להזמנה"],
  },
  {
    title: "הזמנת כרטיס",
    intro: "בשלב ההזמנה המערכת בודקת זמינות מושב, מחשבת מחיר ושומרת את הכרטיס והאירוע באותה טרנזקציה.",
    bullets: [
      "הנוסע בוחר מחלקה, מושב אופציונלי וכבודה.",
      "השרת מונע הזמנה פחות משעה לפני המראה.",
      "מחיר הכרטיס מחושב לפי מחיר בסיס, מחלקה, כבודה ובחירת מושב.",
      "לאחר השמירה נשלח מייל אישור בעזרת התבנית BookingConfirmationEmailTemplate.",
    ],
    process: ["בחירת אפשרויות", "בדיקות שרת", "שמירה ב-MySQL", "מייל אישור"],
  },
  {
    title: "יצירת קובץ כרטיס",
    intro: "לאחר יצירת הכרטיס מופעל תהליך רקע שמייצר קובץ טקסט להורדה בלי לעכב את פעולת ההזמנה במסך.",
    bullets: [
      "ה-API כותב OutboxMessage כחלק מטרנזקציית ההזמנה.",
      "TicketOutboxPublisher שולח משימות ל-RabbitMQ במחזורים קצרים.",
      "ticket-generator מקבל הודעה, יוצר ticket-{id}.txt ומעדכן את הנתיב במסד הנתונים.",
      "ProcessedEvents מונע טיפול כפול באותו אירוע.",
    ],
    flow: ["Tickets", "Outbox", "RabbitMQ", "Worker", "Ticket file"],
  },
  {
    title: "ניהול כרטיסים ופרופיל נוסע",
    intro: "לאחר ההזמנה הנוסע מנהל את הכרטיסים שלו מתוך מסך אחד, כולל שינויים והורדת קובץ הכרטיס.",
    bullets: [
      "מסך My tickets מציג כרטיסים פעילים, כבודה וקבצים מוכנים.",
      "הנוסע יכול לבטל, לשנות מושב, לשדרג מחלקה ולהוסיף כבודה.",
      "כל שינוי עובר דרך הרשאות API שמוודאות שהנוסע רואה רק את הכרטיסים שלו.",
      "מסך הפרופיל מאפשר עדכון פרטי נוסע ושמירתם במערכת.",
    ],
    process: ["רשימת כרטיסים", "עריכת אפשרויות", "עדכון מחיר", "הורדת קובץ"],
  },
  {
    title: "דשבורד מנהל",
    intro: "קונסולת המנהל מרכזת מצב תפעולי ומאפשרת מעבר מהיר בין מסכי הניהול.",
    bullets: [
      "הדשבורד מציג ספירת טיסות, כרטיסים, משתמשים וסטטוס חיבור ל-API.",
      "לוח ההמראות מדגיש טיסות בשעה הקרובה.",
      "התפריט הצדדי נטען לפי משתמש מחובר ומגן מפני כניסת Passenger.",
      "כפתור רענון קורא שוב לנתוני השרת בזמן אמת.",
    ],
    process: ["מדדים", "לוח המראות", "ניווט מנהל", "רענון נתונים"],
  },
  {
    title: "ניהול תפעולי",
    intro: "מסכי הניהול מאפשרים לצוות לעדכן את רכיבי התפעול המרכזיים של שדה התעופה.",
    bullets: [
      "Flights מציג רשימת טיסות עם סינון לפי תאריך ומוצא.",
      "Gates מאפשר הוספת שערים לפי מספר שער וטרמינל.",
      "Tickets מאפשר חיפוש לפי מספר טיסה או תעודת זהות ועדכון פרטי כרטיס.",
      "Baggage מציג את כל פריטי הכבודה ומאפשר מעקב אחר סטטוס.",
    ],
    process: ["טיסות", "שערים", "כרטיסים", "כבודה"],
  },
  {
    title: "משתמשים ונתוני בסיס",
    intro: "המערכת נשענת על נתוני משתמשים, שדות תעופה וכלי טיס כדי לבנות לוח טיסות אמין לדמו.",
    bullets: [
      "Users מאפשר חיפוש משתמשים לפי תעודת זהות או אימייל.",
      "DemoDataSeeder יוצר משתמשים ראשוניים בהפעלה.",
      "CsvFlightScheduleSeeder קורא קבצי CSV של שדות תעופה וכלי טיס ויוצר לוח טיסות מקומי.",
      "המודלים כוללים אינדקסים ייחודיים למייל, תעודת זהות, מושב פעיל ושער.",
    ],
    process: ["Users", "CSV", "Seeder", "Flights"],
  },
  {
    title: "עדכוני טיסות חיצוניים",
    intro: "במצב מתקדם ניתן לחבר מקורות חיצוניים לטיסות, לפרסם עדכונים ל-Kafka ולשמור אותם במסד הנתונים.",
    bullets: [
      "flight-producer תומך בסימולציית CSV, Amadeus, OpenSky ו-FlightAware Firehose.",
      "ההודעות נשלחות ל-topic בשם flight-updates.",
      "FlightUpdates.Consumer קורא הודעות, מבצע upsert לפי Source ו-ExternalId, ורק אז מאשר offset.",
      "הזרימה מופרדת בפרופיל Docker Compose כדי שהדמו המקומי ימשיך לעבוד גם בלי API חיצוני.",
    ],
    flow: ["External source", "Producer", "Kafka", "Consumer", "MySQL"],
  },
  {
    title: "סיכום זרימת עבודה",
    intro: "המערכת בנויה סביב הפרדה ברורה בין חוויית נוסע, ניהול תפעולי ותהליכי רקע שמחזקים אמינות.",
    bullets: [
      "הנוסע מחפש טיסה, מזמין, מקבל אישור ומנהל את הכרטיס.",
      "המנהל עוקב אחר מצב המערכת ומנהל טיסות, משתמשים, כרטיסים ושערים.",
      "השרת מרכז אימות, הרשאות, חוקי הזמנה, מיילים וגישה למסד הנתונים.",
      "תורי הודעות מאפשרים לבצע פעולות רקע בלי לפגוע בחוויית המשתמש.",
    ],
    process: ["נוסע", "מנהל", "API", "תהליכי רקע"],
  },
];

function esc(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function rm(dir) {
  if (fs.existsSync(dir)) fs.rmSync(dir, { recursive: true, force: true });
}

function relsXml(rels) {
  return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
${rels.map((r) => `  <Relationship Id="${r.id}" Type="${r.type}" Target="${r.target}"/>`).join("\n")}
</Relationships>`;
}

function textbox(id, name, x, y, w, h, text, opts = {}) {
  const font = opts.font || "Arial";
  const size = opts.size || 2800;
  const color = opts.color || colors.ink;
  const bold = opts.bold ? '<a:b/>' : "";
  const anchor = opts.valign || "top";
  const align = opts.align || "r";
  const rtl = opts.rtl === false ? "" : ' rtl="1"';
  const lines = String(text).split("\n");
  return `<p:sp>
  <p:nvSpPr><p:cNvPr id="${id}" name="${esc(name)}"/><p:cNvSpPr txBox="1"/><p:nvPr/></p:nvSpPr>
  <p:spPr><a:xfrm><a:off x="${x}" y="${y}"/><a:ext cx="${w}" cy="${h}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln></p:spPr>
  <p:txBody><a:bodyPr wrap="square" anchor="${anchor}" rtlCol="1"><a:spAutoFit/></a:bodyPr><a:lstStyle/>
  ${lines.map((line) => `<a:p><a:pPr algn="${align}"${rtl}/><a:r><a:rPr lang="he-IL" sz="${size}" dirty="0">${bold}<a:solidFill><a:srgbClr val="${color}"/></a:solidFill><a:latin typeface="${font}"/><a:cs typeface="${font}"/></a:rPr><a:t>${esc(line)}</a:t></a:r></a:p>`).join("")}
  </p:txBody>
</p:sp>`;
}

function rect(id, name, x, y, w, h, fill, line = colors.line, radius = "roundRect") {
  return `<p:sp>
  <p:nvSpPr><p:cNvPr id="${id}" name="${esc(name)}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
  <p:spPr><a:xfrm><a:off x="${x}" y="${y}"/><a:ext cx="${w}" cy="${h}"/></a:xfrm><a:prstGeom prst="${radius}"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="${fill}"/></a:solidFill><a:ln w="12700"><a:solidFill><a:srgbClr val="${line}"/></a:solidFill></a:ln></p:spPr>
</p:sp>`;
}

function picture(id, relId, x, y, w, h, name = "logo") {
  return `<p:pic>
  <p:nvPicPr><p:cNvPr id="${id}" name="${esc(name)}"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
  <p:blipFill><a:blip r:embed="${relId}"/><a:stretch><a:fillRect/></a:stretch></p:blipFill>
  <p:spPr><a:xfrm><a:off x="${x}" y="${y}"/><a:ext cx="${w}" cy="${h}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></p:spPr>
</p:pic>`;
}

function line(id, x1, y1, x2, y2, color = colors.blue) {
  const x = Math.min(x1, x2);
  const y = Math.min(y1, y2);
  const w = Math.abs(x2 - x1) || 1;
  const h = Math.abs(y2 - y1) || 1;
  return `<p:cxnSp>
  <p:nvCxnSpPr><p:cNvPr id="${id}" name="connector ${id}"/><p:cNvCxnSpPr/><p:nvPr/></p:nvCxnSpPr>
  <p:spPr><a:xfrm><a:off x="${x}" y="${y}"/><a:ext cx="${w}" cy="${h}"/></a:xfrm><a:prstGeom prst="straightConnector1"><a:avLst/></a:prstGeom><a:ln w="25400"><a:solidFill><a:srgbClr val="${color}"/></a:solidFill><a:tailEnd type="none"/><a:headEnd type="triangle"/></a:ln></p:spPr>
</p:cxnSp>`;
}

function bulletText(items) {
  return items.map((item) => `• ${item}`).join("\n");
}

function flowShapes(items, y, startId, mode = "flow") {
  const n = items.length;
  const gap = 170000;
  const totalW = 10000000;
  const boxW = Math.floor((totalW - gap * (n - 1)) / n);
  const x0 = 1096000;
  let xml = "";
  items.forEach((item, i) => {
    const x = x0 + i * (boxW + gap);
    const fill = [colors.sky, colors.mint, colors.amber, colors.coral, "E9E7FF"][i % 5];
    xml += rect(startId + i * 3, `${mode} ${i + 1}`, x, y, boxW, 760000, fill, "B6C6D4");
    xml += textbox(startId + i * 3 + 1, `${mode} text ${i + 1}`, x + 90000, y + 185000, boxW - 180000, 360000, item, { size: 1900, bold: true, color: colors.navy, align: "c" });
    if (i < n - 1) xml += line(startId + i * 3 + 2, x + boxW + 20000, y + 380000, x + boxW + gap - 20000, y + 380000);
  });
  return xml;
}

function contentSlideXml(slide, index) {
  let id = 10;
  const title = textbox(id++, "title", 6800000, 470000, 4800000, 650000, slide.title, { size: 3300, bold: true, color: colors.navy });
  const intro = textbox(id++, "intro", 1440000, 1240000, 10080000, 780000, slide.intro, { size: 2050, color: colors.muted });
  const bullets = textbox(id++, "bullets", 5900000, 2340000, 5500000, 3000000, bulletText(slide.bullets), { size: 1850, color: colors.ink });
  const band = rect(id++, "left panel", 850000, 2300000, 4500000, 3080000, colors.white, "D7E1EA", "roundRect");
  const label = textbox(id++, "stage label", 1040000, 2520000, 4120000, 440000, "תיאור מקדים לשלב", { size: 1800, bold: true, color: colors.blue });
  const desc = textbox(id++, "stage desc", 1040000, 3030000, 4120000, 1420000, slide.intro, { size: 1780, color: colors.ink });
  const diagram = flowShapes(slide.flow || slide.process, 5620000, id, slide.flow ? "flow" : "process");
  const num = textbox(90, "slide number", 420000, 6200000, 600000, 260000, String(index).padStart(2, "0"), { size: 1350, color: "8A98A5", align: "l", rtl: false });
  return slideShell(`${title}${intro}${band}${label}${desc}${bullets}${diagram}${num}`, index);
}

function coverSlideXml(slide) {
  let id = 10;
  const bgBand = rect(id++, "cover color field", 0, 0, CX, CY, "EFF7FC", "EFF7FC", "rect");
  const logo = fs.existsSync(logoPath) ? picture(id++, "rId2", 850000, 540000, 1800000, 830000, "KodFly logo") : "";
  const kicker = textbox(id++, "kicker", 7650000, 980000, 3700000, 320000, slide.subtitle, { size: 1900, color: colors.blue, bold: true });
  const title = textbox(id++, "cover title", 3600000, 1980000, 7900000, 1220000, slide.title, { size: 5200, bold: true, color: colors.navy });
  const body = textbox(id++, "authors", 3600000, 3440000, 7900000, 600000, slide.body, { size: 2450, color: colors.ink });
  const accent = rect(id++, "accent", 800000, 5100000, 10400000, 170000, colors.blue, colors.blue, "rect");
  const bottom = textbox(id++, "bottom", 3600000, 5460000, 7900000, 420000, "סקירת מסכים, תהליכים, API ותהליכי רקע", { size: 1900, color: colors.muted });
  return slideShell(`${bgBand}${logo}${kicker}${title}${body}${accent}${bottom}`, 1);
}

function slideShell(content, index) {
  const background = index === 1 ? "" : `<p:bg><p:bgPr><a:solidFill><a:srgbClr val="${colors.paper}"/></a:solidFill><a:effectLst/></p:bgPr></p:bg>`;
  return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sld xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
<p:cSld>${background}<p:spTree>
<p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
<p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="${CX}" cy="${CY}"/><a:chOff x="0" y="0"/><a:chExt cx="${CX}" cy="${CY}"/></a:xfrm></p:grpSpPr>
${content}
</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
</p:sld>`;
}

function writeBaseFiles() {
  ensureDir(buildDir);
  for (const dir of ["_rels", "docProps", "ppt/_rels", "ppt/slides/_rels", "ppt/slides", "ppt/slideLayouts/_rels", "ppt/slideLayouts", "ppt/slideMasters/_rels", "ppt/slideMasters", "ppt/theme", "ppt/media"]) {
    ensureDir(path.join(buildDir, dir));
  }
  fs.writeFileSync(path.join(buildDir, "_rels", ".rels"), relsXml([
    { id: "rId1", type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", target: "ppt/presentation.xml" },
    { id: "rId2", type: "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", target: "docProps/core.xml" },
    { id: "rId3", type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties", target: "docProps/app.xml" },
  ]));
  fs.writeFileSync(path.join(buildDir, "docProps", "core.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><dc:title>מערכת לניהול טיסות</dc:title><dc:creator>Codex</dc:creator><cp:lastModifiedBy>Codex</cp:lastModifiedBy><dcterms:created xsi:type="dcterms:W3CDTF">2026-10-13T00:00:00Z</dcterms:created><dcterms:modified xsi:type="dcterms:W3CDTF">2026-10-13T00:00:00Z</dcterms:modified></cp:coreProperties>`);
  fs.writeFileSync(path.join(buildDir, "docProps", "app.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"><Application>Codex</Application><PresentationFormat>On-screen Show (16:9)</PresentationFormat><Slides>${slides.length}</Slides></Properties>`);
  fs.writeFileSync(path.join(buildDir, "ppt", "presProps.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><p:presentationPr xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"/>`);
  fs.writeFileSync(path.join(buildDir, "ppt", "viewProps.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><p:viewPr xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"/>`);
  fs.writeFileSync(path.join(buildDir, "ppt", "tableStyles.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><a:tblStyleLst xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" def="{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}"/>`);
  fs.writeFileSync(path.join(buildDir, "ppt", "theme", "theme1.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Airport"><a:themeElements><a:clrScheme name="Airport"><a:dk1><a:srgbClr val="${colors.navy}"/></a:dk1><a:lt1><a:srgbClr val="FFFFFF"/></a:lt1><a:dk2><a:srgbClr val="${colors.ink}"/></a:dk2><a:lt2><a:srgbClr val="${colors.paper}"/></a:lt2><a:accent1><a:srgbClr val="${colors.blue}"/></a:accent1><a:accent2><a:srgbClr val="28A07A"/></a:accent2><a:accent3><a:srgbClr val="D79517"/></a:accent3><a:accent4><a:srgbClr val="D7674F"/></a:accent4><a:accent5><a:srgbClr val="7566CC"/></a:accent5><a:accent6><a:srgbClr val="5E6D78"/></a:accent6><a:hlink><a:srgbClr val="${colors.blue}"/></a:hlink><a:folHlink><a:srgbClr val="7566CC"/></a:folHlink></a:clrScheme><a:fontScheme name="Arial"><a:majorFont><a:latin typeface="Arial"/><a:cs typeface="Arial"/></a:majorFont><a:minorFont><a:latin typeface="Arial"/><a:cs typeface="Arial"/></a:minorFont></a:fontScheme><a:fmtScheme name="Airport"><a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:fillStyleLst><a:lnStyleLst><a:ln w="9525"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme></a:themeElements><a:objectDefaults/><a:extraClrSchemeLst/></a:theme>`);
  fs.writeFileSync(path.join(buildDir, "ppt", "slideMasters", "slideMaster1.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><p:sldMaster xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"><p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="${CX}" cy="${CY}"/><a:chOff x="0" y="0"/><a:chExt cx="${CX}" cy="${CY}"/></a:xfrm></p:grpSpPr></p:spTree></p:cSld><p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2" accent1="accent1" accent2="accent2" accent3="accent3" accent4="accent4" accent5="accent5" accent6="accent6" hlink="hlink" folHlink="folHlink"/><p:sldLayoutIdLst><p:sldLayoutId id="2147483649" r:id="rId1"/></p:sldLayoutIdLst><p:txStyles><p:titleStyle/><p:bodyStyle/><p:otherStyle/></p:txStyles></p:sldMaster>`);
  fs.writeFileSync(path.join(buildDir, "ppt", "slideMasters", "_rels", "slideMaster1.xml.rels"), relsXml([
    { id: "rId1", type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout", target: "../slideLayouts/slideLayout1.xml" },
    { id: "rId2", type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme", target: "../theme/theme1.xml" },
  ]));
  fs.writeFileSync(path.join(buildDir, "ppt", "slideLayouts", "slideLayout1.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><p:sldLayout xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" type="blank" preserve="1"><p:cSld name="Blank"><p:spTree><p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="${CX}" cy="${CY}"/><a:chOff x="0" y="0"/><a:chExt cx="${CX}" cy="${CY}"/></a:xfrm></p:grpSpPr></p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>`);
  fs.writeFileSync(path.join(buildDir, "ppt", "slideLayouts", "_rels", "slideLayout1.xml.rels"), relsXml([
    { id: "rId1", type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster", target: "../slideMasters/slideMaster1.xml" },
  ]));
}

function writeSlides() {
  if (fs.existsSync(logoPath)) fs.copyFileSync(logoPath, path.join(buildDir, "ppt", "media", "kodfly-logo.png"));
  slides.forEach((slide, i) => {
    const n = i + 1;
    const xml = i === 0 ? coverSlideXml(slide) : contentSlideXml(slide, n);
    fs.writeFileSync(path.join(buildDir, "ppt", "slides", `slide${n}.xml`), xml);
    const rels = [{ id: "rId1", type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout", target: "../slideLayouts/slideLayout1.xml" }];
    if (i === 0 && fs.existsSync(logoPath)) rels.push({ id: "rId2", type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", target: "../media/kodfly-logo.png" });
    fs.writeFileSync(path.join(buildDir, "ppt", "slides", "_rels", `slide${n}.xml.rels`), relsXml(rels));
  });
}

function writePresentation() {
  const slideIds = slides.map((_, i) => `<p:sldId id="${256 + i}" r:id="rId${i + 5}"/>`).join("");
  fs.writeFileSync(path.join(buildDir, "ppt", "presentation.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><p:presentation xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"><p:sldMasterIdLst><p:sldMasterId id="2147483648" r:id="rId1"/></p:sldMasterIdLst><p:sldIdLst>${slideIds}</p:sldIdLst><p:sldSz cx="${CX}" cy="${CY}" type="wide"/><p:notesSz cx="6858000" cy="9144000"/><p:defaultTextStyle/></p:presentation>`);
  const rels = [
    { id: "rId1", type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster", target: "slideMasters/slideMaster1.xml" },
    { id: "rId2", type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/presProps", target: "presProps.xml" },
    { id: "rId3", type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/viewProps", target: "viewProps.xml" },
    { id: "rId4", type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/tableStyles", target: "tableStyles.xml" },
    ...slides.map((_, i) => ({ id: `rId${i + 5}`, type: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide", target: `slides/slide${i + 1}.xml` })),
  ];
  fs.writeFileSync(path.join(buildDir, "ppt", "_rels", "presentation.xml.rels"), relsXml(rels));
}

function writeContentTypes() {
  const slideOverrides = slides.map((_, i) => `<Override PartName="/ppt/slides/slide${i + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slide+xml"/>`).join("");
  fs.writeFileSync(path.join(buildDir, "[Content_Types].xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Default Extension="png" ContentType="image/png"/><Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/><Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/><Override PartName="/ppt/presentation.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"/><Override PartName="/ppt/slideMasters/slideMaster1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml"/><Override PartName="/ppt/slideLayouts/slideLayout1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml"/><Override PartName="/ppt/theme/theme1.xml" ContentType="application/vnd.openxmlformats-officedocument.theme+xml"/><Override PartName="/ppt/presProps.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.presProps+xml"/><Override PartName="/ppt/viewProps.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.viewProps+xml"/><Override PartName="/ppt/tableStyles.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.tableStyles+xml"/>${slideOverrides}</Types>`);
}

function zipDeck() {
  ensureDir(path.dirname(finalPath));
  if (fs.existsSync(finalPath)) fs.rmSync(finalPath, { force: true });
  const zipPath = finalPath.replace(/\.pptx$/i, ".zip");
  if (fs.existsSync(zipPath)) fs.rmSync(zipPath, { force: true });
  execFileSync("powershell.exe", ["-NoProfile", "-Command", `Compress-Archive -Path '${buildDir}\\*' -DestinationPath '${zipPath}' -Force`], { stdio: "inherit" });
  fs.renameSync(zipPath, finalPath);
}

rm(buildDir);
writeBaseFiles();
writeSlides();
writePresentation();
writeContentTypes();
zipDeck();
console.log(finalPath);

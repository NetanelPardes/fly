import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const workspace = process.cwd();
const buildDir = path.join(workspace, ".tmp", "presentation-build", "polished-pptx-src");
const outDir = path.join(workspace, "output");
const finalPath = path.join(outDir, "airport-management-system-premium.pptx");
const logoPath = path.join(workspace, "Airport.Ui", "assets", "kodfly-logo.png");
const planePath = path.join(workspace, "Data", "Airplane_pic.png");
const screenshotDir = "C:\\Users\\sendi\\Desktop\\New folder (10)";

const screenshotDefs = {
  userLogin: { file: "לוג אין של משתמש.jpeg", media: "screenshot-user-login.jpeg" },
  adminLogin: { file: "לוג אין של מנהל.jpeg", media: "screenshot-admin-login.jpeg" },
  searchBoard: { file: "בורד חיפוש טיסות.jpeg", media: "screenshot-flight-search.jpeg" },
  filterBoard: { file: "בורד סינון טיסות.jpeg", media: "screenshot-flight-filter.jpeg" },
  bookingDialog: { file: "הזמנת כרטיס טיסה.jpeg", media: "screenshot-booking-dialog.jpeg" },
  bookingLoader: { file: "אחרי הזמנת טיסה , חלון המתנה.jpeg", media: "screenshot-booking-loader.jpeg" },
  bookingEmail: { file: "מייל על הזמנת טיסה.jpeg", media: "screenshot-booking-email.jpeg" },
  ticketUpdate: { file: "שינוי כרטיס.jpeg", media: "screenshot-ticket-update.jpeg" },
  updateEmail: { file: "מייל על שינוי פרטי משתמש.jpeg", media: "screenshot-update-email.jpeg" },
  adminDepartures: { file: "בורד מנהל על טיסות עם המראה בשעה הקרובה.jpeg", media: "screenshot-admin-departures.jpeg" },
  adminFlights: { file: "בורד מנהל עם הטיסות לפי אותו יום.jpeg", media: "screenshot-admin-flights.jpeg" },
  adminUsers: { file: "בורד מנהל על המשתמשים.jpeg", media: "screenshot-admin-users.jpeg" },
  folderLogo: { file: "לוגו.jpeg", media: "screenshot-folder-logo.jpeg" },
};

const CX = 12192000;
const CY = 6858000;

const relTypes = {
  officeDocument: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument",
  slide: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide",
  slideMaster: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster",
  slideLayout: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout",
  theme: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme",
  image: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image",
  core: "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties",
  app: "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties",
};

const colors = {
  ink: "102338",
  blue: "0F5D9B",
  blue2: "1C7DBD",
  sky: "DCEFFB",
  ice: "F5FAFD",
  green: "DDF4EA",
  amber: "FFE8B3",
  coral: "FFD9CF",
  lavender: "E8E7FF",
  line: "C7D6E2",
  muted: "5E6D78",
  white: "FFFFFF",
  dark: "0B1F33",
};

const slides = [
  {
    type: "cover",
    title: "מערכת לניהול טיסות",
    subtitle: "מצגת קוד, מסכים ותהליכי עבודה",
    authors: "ביצוע: נתנאל פרדס ואבי רוזנגרטן",
    chips: ["UI", "API", "MySQL", "Kafka", "Docker"],
  },
  {
    title: "מטרת המערכת",
    intro: "השלב הראשון מסביר מה המערכת פותרת: חיבור בין נוסעים, צוותי תפעול ותהליכי רקע למערכת אחת לניהול טיסות וכרטיסים.",
    bullets: [
      "חיפוש טיסות, הזמנה וניהול כרטיסים מצד הנוסע.",
      "ניהול טיסות, שערים, משתמשים, כרטיסים וכבודה מצד המנהל.",
      "שמירת מידע ב-MySQL עם API מרכזי והרשאות לפי תפקיד.",
      "יצירת קבצי כרטיסים ושליחת מיילים בלי לעצור את חוויית המשתמש.",
    ],
    flow: ["נוסע", "API", "מסד נתונים", "תפעול", "תהליכי רקע"],
  },
  {
    title: "מבנה הפרויקט",
    intro: "בשלב הזה רואים איך הקוד מחולק לפרויקטים ברורים, כך שכל שכבה אחראית על חלק אחר במערכת.",
    bullets: [
      "Airport.Api כולל REST API, EF Core, JWT, Swagger ותבניות מייל.",
      "Airport.Ui כולל מסכי HTML/CSS/JavaScript עבור נוסע ומנהל.",
      "Airport.FlightUpdates.Consumer מקבל עדכוני Kafka ושומר אותם בבסיס הנתונים.",
      "python/ticket-generator צורך הודעות RabbitMQ ומייצר קבצי כרטיסים.",
    ],
    blocks: [
      ["Airport.Api", "לוגיקה, הרשאות ונתונים"],
      ["Airport.Ui", "מסכים וחוויית משתמש"],
      ["Consumers", "Kafka ו-RabbitMQ"],
      ["Docker", "הרצה מלאה בסביבה אחת"],
    ],
  },
  {
    title: "ארכיטקטורה כללית",
    intro: "כאן מוצג מסלול הנתונים מקצה לקצה: מהדפדפן, דרך השרת, ועד תהליכים אסינכרוניים שממשיכים לעבוד ברקע.",
    bullets: [
      "הדפדפן קורא ל-API עם JWT לכל פעולה מוגנת.",
      "ה-API שומר את הנתונים ב-MySQL ומנהל טרנזקציות.",
      "RabbitMQ משמש ליצירת כרטיסים לאחר הזמנה.",
      "Kafka משמש לעדכוני טיסות חיצוניים במצב האופציונלי.",
    ],
    diagram: ["Airport.Ui", "Airport.Api", "MySQL", "RabbitMQ", "Ticket Worker"],
  },
  {
    title: "אימות והרשאות",
    intro: "לפני שהמשתמש נכנס למסכים, המערכת מזהה אותו ומחליטה אילו פעולות מותר לו לבצע.",
    bullets: [
      "POST /api/auth/login מחזיר JWT לשימוש בקריאות הבאות.",
      "תפקידי Admin, Ticketing, Baggage ו-Passenger מגבילים מסכים ופעולות.",
      "נוסע יכול לראות רק את הכרטיסים והכבודה שלו.",
      "הממשק שומר את פרטי ההתחברות ומוסיף Authorization לכל בקשת API.",
    ],
    flow: ["כניסה", "JWT", "בדיקת תפקיד", "גישה למסך"],
  },
  {
    title: "מסך כניסת נוסע",
    intro: "הנוסע מתחיל במסך כניסה או רישום, וממנו עובר לפורטל החיפוש וההזמנות.",
    bullets: [
      "המסך תומך בכניסה לחשבון קיים וגם ביצירת חשבון חדש.",
      "לאחר כניסה מוצלחת נשמרים token ופרטי משתמש בצד הלקוח.",
      "שגיאות התחברות מוצגות למשתמש בצורה ברורה.",
      "הניווט מפנה אוטומטית למסכי הטיסות והכרטיסים של הלקוח.",
    ],
    screen: ["Customer Login", "Create Account", "Session Ready"],
  },
  {
    title: "חיפוש טיסות",
    intro: "בשלב החיפוש הנוסע מסנן טיסות לפי תאריך, מוצא ויעד, ואז משווה אפשרויות לפני הזמנה.",
    bullets: [
      "מסך הטיסות מציג מדדים, מסננים, רשימת תוצאות וכפתורי פעולה.",
      "המשתמש יכול לבחור מטבע להצגת המחירים דרך endpoint ייעודי.",
      "רשימות גדולות מחולקות לדפים של 30 פריטים כדי לשמור על מסך קריא.",
      "בכל פעולה מול השרת מופיע loader עם המטוס הקטן כדי שלא תהיה תחושת תקיעה.",
    ],
    screen: ["Filters", "Flight Cards", "30 per page", "Currency"],
  },
  {
    title: "פרטי טיסה והזמנה",
    intro: "אחרי בחירת טיסה, הנוסע רואה פרטים מלאים ובוחר אפשרויות לפני יצירת הכרטיס.",
    bullets: [
      "חלון הפרטים מציג מסלול, שעות, מטוס, שער ומחיר.",
      "חלון ההזמנה כולל מחלקה, כבודה ומושב כאשר מותר לבחור.",
      "השרת בודק זמינות מושב וחוסם הזמנות קרובות מדי להמראה.",
      "המחיר מחושב מחדש לפי מחלקה, כבודה ואפשרויות נוספות.",
    ],
    flow: ["בחירת טיסה", "פרטים", "אפשרויות", "אישור הזמנה"],
  },
  {
    title: "חוקי הזמנה",
    intro: "המערכת לא מסתפקת בממשק יפה: כללי העסק נאכפים גם בצד השרת כדי לשמור על נתונים נכונים.",
    bullets: [
      "אין כפילות של מושב פעיל באותה טיסה.",
      "אי אפשר להזמין פחות משעה לפני ההמראה.",
      "מחיר הכרטיס נקבע לפי מחיר בסיס, מחלקה וכבודה.",
      "כל הזמנה יוצרת גם רשומת Outbox ליצירת קובץ הכרטיס.",
    ],
    blocks: [
      ["Seat", "בדיקת זמינות"],
      ["Cutoff", "חסימת הזמנה מאוחרת"],
      ["Price", "חישוב לפי אפשרויות"],
      ["Outbox", "משימת רקע"],
    ],
  },
  {
    title: "מייל אישור הזמנה",
    intro: "לאחר הזמנה מוצלחת הנוסע מקבל מייל מסודר עם כל פרטי הטיסה, הנוסע והתשלום.",
    bullets: [
      "BookingConfirmationEmailTemplate בונה HTML וגם fallback טקסט פשוט.",
      "הלוגו מצורף כתמונה פנימית כדי שיופיע בתוך המייל.",
      "הפרטים מחולקים למקטעים: נוסע, מסלול, לו״ז, אפשרויות ותשלום.",
      "אם SMTP לא מוגדר, ההזמנה עדיין נשמרת והמערכת רק מדלגת על שליחת המייל.",
    ],
    email: "Have a pleasant flight!\nBooking reference: KOD-000014\nFlight: ETM4416\nSeat: 12A\nClass: Business",
  },
  {
    title: "הכרטיסים שלי",
    intro: "במסך הכרטיסים הנוסע מנהל את ההזמנות שלו ומוריד קבצים כאשר הם מוכנים.",
    bullets: [
      "המסך מציג כרטיסים פעילים, כבודה וקבצים זמינים להורדה.",
      "הנוסע רואה רק כרטיסים ששייכים לחשבון שלו.",
      "קובץ הכרטיס נוצר ברקע ומופיע להורדה כאשר הנתיב נשמר בבסיס הנתונים.",
      "הכרטיסים מוצגים ברשימה מדורגת עם פייג׳ינג של 30 פריטים.",
    ],
    screen: ["My Tickets", "Upgrades", "Download", "Profile"],
  },
  {
    title: "מגבלות נוסע אחרי הזמנה",
    intro: "בשלב עדכון הכרטיס הוגדרו גבולות ברורים: נוסע יכול לשדרג, אבל לא לפגוע בהזמנה קיימת.",
    bullets: [
      "נוסע לא יכול לבטל כרטיס מתוך מסך הלקוח.",
      "נוסע לא יכול לשנות מושב לאחר ההזמנה.",
      "אפשר להעלות כבודה, למשל מ-20 ל-25 ק״ג, אבל לא להוריד.",
      "אפשר לשדרג מחלקה, אבל לא לרדת למחלקה נמוכה יותר.",
    ],
    blocks: [
      ["ביטול", "חסום לנוסע"],
      ["מושב", "נעול אחרי הזמנה"],
      ["כבודה", "רק העלאה"],
      ["מחלקה", "רק שדרוג"],
    ],
  },
  {
    title: "מייל עדכון כרטיס",
    intro: "כאשר פרטי כרטיס משתנים, המערכת שולחת מייל עדכון מעוצב ולא טקסט גולמי.",
    bullets: [
      "TicketUpdateEmailTemplate כולל HTML מעוצב עם לוגו ומקטעים ברורים.",
      "הטקסט הפשוט נשמר כתמיכה למיילים שלא מציגים HTML.",
      "המייל כולל סטטוס, מסלול, לו״ז, שער, מושב, מחלקה, כבודה וסכום.",
      "אותה תשתית משמשת גם לעדכון וגם לביטול מצד גורם מורשה.",
    ],
    email: "Ticket update\nYour ticket details have been updated.\nRoute: Eilat to Matsuyama\nBaggage: 25 kg\nStatus: Active",
  },
  {
    title: "דשבורד מנהל",
    intro: "המנהל מקבל תמונת מצב תפעולית ומעבר מהיר למסכים שבהם מטפלים בנתוני המערכת.",
    bullets: [
      "הדשבורד מציג מדדי טיסות, כרטיסים, משתמשים וחיבור ל-API.",
      "לוח המראות מדגיש טיסות קרובות בשעה הקרובה.",
      "כפתור Refresh טוען נתונים עדכניים מהשרת.",
      "התפריט מותאם למסכי תפעול: טיסות, שערים, כרטיסים, משתמשים וכבודה.",
    ],
    screen: ["Metrics", "Departures", "Admin Navigation", "Live API"],
  },
  {
    title: "ניהול טיסות ושערים",
    intro: "במסכי התפעול ניתן לעדכן את מצב הטיסות ואת תשתית השערים בצורה מרוכזת.",
    bullets: [
      "מסך Flights מציג רשימות עם סינון לפי תאריכים, מוצא ויעד.",
      "אפשר לדחות או לבטל טיסה בהתאם להרשאות.",
      "מסך Gates מאפשר הוספה וניהול שערים לפי מספר וטרמינל.",
      "רשימות ארוכות מוצגות בפייג׳ינג של 30 פריטים לשמירה על ביצועים וקריאות.",
    ],
    flow: ["Flights", "Filter", "Delay / Cancel", "Gates"],
  },
  {
    title: "ניהול כרטיסים, משתמשים וכבודה",
    intro: "הצוות יכול לאתר רשומות במהירות ולטפל בבעיות שירות או תפעול.",
    bullets: [
      "Tickets מאפשר חיפוש לפי טיסה או נוסע ועדכון פרטי כרטיס.",
      "Users מאפשר איתור משתמשים לפי תעודת זהות או אימייל.",
      "Baggage מציג פריטי כבודה ומעקב סטטוסים.",
      "הפעולות מוצגות עם loader גלובלי כדי שכל לחיצה תיתן משוב ברור.",
    ],
    blocks: [
      ["Tickets", "שינוי / ביטול מורשה"],
      ["Users", "איתור חשבונות"],
      ["Baggage", "מעקב כבודה"],
      ["Loader", "משוב בזמן טעינה"],
    ],
  },
  {
    title: "יצירת כרטיס ברקע",
    intro: "תהליך יצירת קובץ הכרטיס מופרד מההזמנה עצמה, כדי שהמשתמש יקבל תגובה מהירה והמערכת תישאר אמינה.",
    bullets: [
      "ה-API שומר Ticket ו-OutboxMessage באותה טרנזקציה.",
      "TicketOutboxPublisher מפרסם משימות ל-RabbitMQ.",
      "ה-worker יוצר ticket-{id}.txt ומעדכן TicketFilePath.",
      "ProcessedEvents מונע טיפול כפול באותו EventId.",
    ],
    diagram: ["Ticket", "Outbox", "RabbitMQ", "Worker", "File"],
  },
  {
    title: "עדכוני טיסות חיצוניים",
    intro: "כאשר רוצים להתחבר למקורות חיצוניים, המערכת מפעילה producer ו-consumer נפרדים דרך Docker profile.",
    bullets: [
      "flight-producer יכול לעבוד במצב דמו או מול מקורות חיצוניים כמו Amadeus/Firehose.",
      "ההודעות מפורסמות ל-topic בשם flight-updates.",
      "FlightUpdates.Consumer עושה upsert לפי Source ו-ExternalId.",
      "offset מאושר רק אחרי שמירה מוצלחת ל-MySQL.",
    ],
    diagram: ["External API", "Producer", "Kafka", "Consumer", "MySQL"],
  },
  {
    title: "נתוני CSV ודמו מקומי",
    intro: "כדי שהמערכת תעבוד גם בלי API חיצוני, היא מייצרת לוח טיסות מקומי מתוך קבצי נתונים.",
    bullets: [
      "Data כולל aircraft.csv, airport.csv ותמונות/נכסי דמו.",
      "CsvFlightScheduleSeeder יוצר טיסות לפי הגדרות סביבתיות.",
      "MySQL נשמר ב-Docker volume ולכן נתונים ממשיכים מהרצה להרצה.",
      "ניתן להפעיל רגנרציה יזומה רק כאשר רוצים לבנות לוח טיסות מחדש.",
    ],
    flow: ["CSV", "Seeder", "GeneratedFlightSchedules", "Flights"],
  },
  {
    title: "הרצה ובדיקות",
    intro: "בסיום, כל המערכת ניתנת להרצה דרך Docker Compose, עם נתונים שנשמרים ועם בדיקות בסיס שמוודאות שהקוד תקין.",
    bullets: [
      "הרצה מלאה: docker compose up --build או קובץ ההרצה שנוסף לפרויקט.",
      "המערכת לא מוחקת volume, ולכן MySQL וקבצי כרטיסים נשמרים מהרצה להרצה.",
      "בדיקות שבוצעו: dotnet build, בדיקת JavaScript ו-docker compose config.",
      "כתובות שימושיות: http://localhost:5173 לממשק החדש ו-http://localhost:8080/swagger ל-API.",
    ],
    final: true,
  },
];

const bookingRulesIndex = slides.findIndex((slide) => slide.title === "חוקי הזמנה");
if (bookingRulesIndex >= 0) slides.splice(bookingRulesIndex, 1);

Object.assign(slides[5], { screenshots: ["userLogin"] });
Object.assign(slides[6], { screenshots: ["filterBoard", "searchBoard"] });
Object.assign(slides[7], { screenshots: ["bookingDialog", "bookingLoader"] });
Object.assign(slides[8], { screenshots: ["bookingEmail"] });
Object.assign(slides[9], { screenshots: ["ticketUpdate"] });
Object.assign(slides[10], { screenshots: ["ticketUpdate"] });
Object.assign(slides[11], { screenshots: ["updateEmail"] });
Object.assign(slides[12], { screenshots: ["adminLogin", "adminDepartures"] });
Object.assign(slides[13], { screenshots: ["adminFlights"] });
Object.assign(slides[14], { screenshots: ["adminUsers"] });

function esc(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function cleanDir(dir) {
  if (fs.existsSync(dir)) fs.rmSync(dir, { recursive: true, force: true });
  fs.mkdirSync(dir, { recursive: true });
}

function write(file, data) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, data, "utf8");
}

function copy(file, target) {
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(file, target);
}

function relsXml(rels) {
  return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
${rels.map((r) => `  <Relationship Id="${r.id}" Type="${r.type}" Target="${r.target}"/>`).join("\n")}
</Relationships>`;
}

function shape(id, name, x, y, w, h, fill, line = fill, prst = "roundRect", alpha = null) {
  const alphaXml = alpha == null ? "" : `<a:alpha val="${alpha}"/>`;
  return `<p:sp>
  <p:nvSpPr><p:cNvPr id="${id}" name="${esc(name)}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
  <p:spPr><a:xfrm><a:off x="${x}" y="${y}"/><a:ext cx="${w}" cy="${h}"/></a:xfrm><a:prstGeom prst="${prst}"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="${fill}">${alphaXml}</a:srgbClr></a:solidFill><a:ln w="12700"><a:solidFill><a:srgbClr val="${line}"/></a:solidFill></a:ln></p:spPr>
</p:sp>`;
}

function textRun(text, opts = {}) {
  const size = opts.size ?? 2100;
  const color = opts.color ?? colors.ink;
  const bold = opts.bold ? "<a:b/>" : "";
  return `<a:r><a:rPr lang="he-IL" sz="${size}" dirty="0">${bold}<a:solidFill><a:srgbClr val="${color}"/></a:solidFill><a:latin typeface="Arial"/><a:cs typeface="Arial"/></a:rPr><a:t>${esc(text)}</a:t></a:r>`;
}

function textBox(id, name, x, y, w, h, text, opts = {}) {
  const align = opts.align ?? "r";
  const rtl = opts.rtl === false ? "" : ' rtl="1"';
  const anchor = opts.anchor ?? "top";
  const paras = String(text ?? "").split("\n").map((line) => `<a:p><a:pPr algn="${align}"${rtl}/>${textRun(line, opts)}</a:p>`).join("");
  return `<p:sp>
  <p:nvSpPr><p:cNvPr id="${id}" name="${esc(name)}"/><p:cNvSpPr txBox="1"/><p:nvPr/></p:nvSpPr>
  <p:spPr><a:xfrm><a:off x="${x}" y="${y}"/><a:ext cx="${w}" cy="${h}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln></p:spPr>
  <p:txBody><a:bodyPr wrap="square" anchor="${anchor}" rtlCol="1"><a:spAutoFit/></a:bodyPr><a:lstStyle/>${paras}</p:txBody>
</p:sp>`;
}

function bulletList(id, x, y, w, h, items) {
  const paras = items.map((item) => `<a:p><a:pPr marR="245000" indent="-205000" algn="r" rtl="1"><a:buChar char="•"/></a:pPr>${textRun(item, { size: 1850, color: colors.ink })}</a:p>`).join("");
  return `<p:sp>
  <p:nvSpPr><p:cNvPr id="${id}" name="bullets"/><p:cNvSpPr txBox="1"/><p:nvPr/></p:nvSpPr>
  <p:spPr><a:xfrm><a:off x="${x}" y="${y}"/><a:ext cx="${w}" cy="${h}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln></p:spPr>
  <p:txBody><a:bodyPr wrap="square" rtlCol="1"><a:spAutoFit/></a:bodyPr><a:lstStyle/>${paras}</p:txBody>
</p:sp>`;
}

function pic(id, relId, x, y, w, h, name = "image", alpha = null) {
  const alphaXml = alpha == null ? "" : `<a:alphaModFix amt="${alpha}"/>`;
  return `<p:pic>
  <p:nvPicPr><p:cNvPr id="${id}" name="${esc(name)}"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
  <p:blipFill><a:blip r:embed="${relId}">${alphaXml}</a:blip><a:stretch><a:fillRect/></a:stretch></p:blipFill>
  <p:spPr><a:xfrm><a:off x="${x}" y="${y}"/><a:ext cx="${w}" cy="${h}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></p:spPr>
</p:pic>`;
}

function imagePathFor(key) {
  const def = screenshotDefs[key];
  return def ? path.join(screenshotDir, def.file) : null;
}

function mediaNameFor(key) {
  return screenshotDefs[key]?.media;
}

function imageSize(file) {
  const data = fs.readFileSync(file);
  if (data[0] === 0x89 && data.toString("ascii", 1, 4) === "PNG") {
    return { width: data.readUInt32BE(16), height: data.readUInt32BE(20) };
  }

  let offset = 2;
  while (offset < data.length) {
    if (data[offset] !== 0xff) break;
    const marker = data[offset + 1];
    const length = data.readUInt16BE(offset + 2);
    if ((marker >= 0xc0 && marker <= 0xc3) || (marker >= 0xc5 && marker <= 0xc7) || (marker >= 0xc9 && marker <= 0xcb) || (marker >= 0xcd && marker <= 0xcf)) {
      return { height: data.readUInt16BE(offset + 5), width: data.readUInt16BE(offset + 7) };
    }
    offset += 2 + length;
  }

  return { width: 1600, height: 900 };
}

function fitImage(file, box) {
  const { width, height } = imageSize(file);
  const scale = Math.min(box.w / width, box.h / height);
  const w = Math.round(width * scale);
  const h = Math.round(height * scale);
  return {
    x: Math.round(box.x + (box.w - w) / 2),
    y: Math.round(box.y + (box.h - h) / 2),
    w,
    h,
  };
}

function connector(id, x1, y1, x2, y2) {
  const x = Math.min(x1, x2);
  const y = Math.min(y1, y2);
  const w = Math.max(1, Math.abs(x2 - x1));
  const h = Math.max(1, Math.abs(y2 - y1));
  return `<p:cxnSp>
  <p:nvCxnSpPr><p:cNvPr id="${id}" name="connector ${id}"/><p:cNvCxnSpPr/><p:nvPr/></p:nvCxnSpPr>
  <p:spPr><a:xfrm><a:off x="${x}" y="${y}"/><a:ext cx="${w}" cy="${h}"/></a:xfrm><a:prstGeom prst="straightConnector1"><a:avLst/></a:prstGeom><a:ln w="25400"><a:solidFill><a:srgbClr val="${colors.blue2}"/></a:solidFill><a:headEnd type="triangle"/></a:ln></p:spPr>
</p:cxnSp>`;
}

function pillRow(items, y, startId, width = 9900000, x0 = 1120000) {
  const gap = 140000;
  const boxW = Math.floor((width - gap * (items.length - 1)) / items.length);
  let xml = "";
  for (let i = 0; i < items.length; i++) {
    const x = x0 + i * (boxW + gap);
    const fill = [colors.sky, colors.green, colors.amber, colors.coral, colors.lavender][i % 5];
    xml += shape(startId + i * 3, `step-${i}`, x, y, boxW, 630000, fill, colors.line, "roundRect");
    xml += textBox(startId + i * 3 + 1, `step-text-${i}`, x + 80000, y + 165000, boxW - 160000, 250000, items[i], { align: "c", size: 1650, bold: true, color: colors.dark });
    if (i < items.length - 1) xml += connector(startId + i * 3 + 2, x + boxW + 15000, y + 315000, x + boxW + gap - 15000, y + 315000);
  }
  return xml;
}

function blocks(items, startId) {
  let xml = "";
  const x0 = 1100000;
  const y0 = 4060000;
  const w = 2380000;
  const h = 850000;
  const gap = 180000;
  for (let i = 0; i < items.length; i++) {
    const x = x0 + i * (w + gap);
    const [title, body] = items[i];
    const fill = [colors.sky, colors.green, colors.amber, colors.lavender][i % 4];
    xml += shape(startId + i * 3, `block-${i}`, x, y0, w, h, fill, colors.line, "roundRect");
    xml += textBox(startId + i * 3 + 1, `block-title-${i}`, x + 140000, y0 + 145000, w - 280000, 230000, title, { align: "c", size: 1780, bold: true, color: colors.blue });
    xml += textBox(startId + i * 3 + 2, `block-body-${i}`, x + 140000, y0 + 430000, w - 280000, 260000, body, { align: "c", size: 1320, color: colors.ink });
  }
  return xml;
}

function screenMock(items, startId) {
  let xml = "";
  const x = 910000;
  const y = 3750000;
  const w = 4420000;
  const h = 1420000;
  xml += shape(startId, "browser", x, y, w, h, colors.white, colors.line, "roundRect");
  xml += shape(startId + 1, "browser bar", x, y, w, 300000, colors.sky, colors.sky, "roundRect");
  xml += textBox(startId + 2, "screen-title", x + 2500000, y + 480000, 1500000, 280000, items[0] ?? "Screen", { size: 1800, bold: true, color: colors.blue });
  for (let i = 1; i < items.length; i++) {
    const yy = y + 480000 + (i - 1) * 280000;
    xml += shape(startId + 2 + i, `screen-row-${i}`, x + 320000, yy, 1720000, 170000, [colors.green, colors.amber, colors.coral][(i - 1) % 3], [colors.green, colors.amber, colors.coral][(i - 1) % 3], "roundRect");
    xml += textBox(startId + 10 + i, `screen-row-text-${i}`, x + 380000, yy + 25000, 1600000, 110000, items[i], { align: "c", size: 1050, bold: true, color: colors.dark });
  }
  return xml;
}

function emailMock(text, startId) {
  let xml = "";
  const x = 880000;
  const y = 3570000;
  const w = 4620000;
  const h = 1600000;
  xml += shape(startId, "email", x, y, w, h, colors.white, colors.line, "roundRect");
  xml += textBox(startId + 1, "email-subject", x + 330000, y + 220000, w - 660000, 240000, "KodFly", { size: 1700, bold: true, color: colors.blue, align: "l", rtl: false });
  xml += shape(startId + 2, "email-line", x + 330000, y + 560000, w - 660000, 25000, colors.line, colors.line, "rect");
  xml += textBox(startId + 3, "email-body", x + 330000, y + 710000, w - 660000, 660000, text, { size: 1320, color: colors.ink, align: "l", rtl: false });
  return xml;
}

function screenshotGrid(keys, startId) {
  const existing = keys
    .map((key) => ({ key, file: imagePathFor(key), media: mediaNameFor(key) }))
    .filter((item) => item.file && item.media && fs.existsSync(item.file));

  if (existing.length === 0) return "";

  let xml = "";
  const outer = { x: 790000, y: 2180000, w: 5480000, h: 2880000 };
  xml += shape(startId++, "screenshot-panel", outer.x, outer.y, outer.w, outer.h, colors.white, colors.line, "roundRect");

  const gap = 120000;
  const boxes = existing.length === 1
    ? [{ x: outer.x + 180000, y: outer.y + 180000, w: outer.w - 360000, h: outer.h - 360000 }]
    : existing.slice(0, 2).map((_, i) => ({
        x: outer.x + 180000,
        y: outer.y + 180000 + i * ((outer.h - 360000 - gap) / 2 + gap),
        w: outer.w - 360000,
        h: Math.floor((outer.h - 360000 - gap) / 2),
      }));

  existing.slice(0, 2).forEach((item, i) => {
    const box = boxes[i];
    const fitted = fitImage(item.file, box);
    xml += shape(startId++, `screenshot-bg-${i}`, box.x, box.y, box.w, box.h, "F8FBFE", "D8E4EE", "roundRect");
    xml += pic(startId++, `rId${20 + i}`, fitted.x, fitted.y, fitted.w, fitted.h, `screenshot-${item.key}`);
  });

  return xml;
}

function screenshotShowcase(slide, startId) {
  const existing = (slide.screenshots ?? [])
    .map((key) => ({ key, file: imagePathFor(key), media: mediaNameFor(key) }))
    .filter((item) => item.file && item.media && fs.existsSync(item.file))
    .slice(0, 2);

  if (existing.length === 0) return "";

  let xml = "";
  const stage = { x: 560000, y: 1120000, w: 6600000, h: 5000000 };
  xml += shape(startId++, "screenshot-shadow", stage.x + 100000, stage.y + 120000, stage.w, stage.h, "0A1724", "0A1724", "roundRect", 26000);
  xml += shape(startId++, "screenshot-stage", stage.x, stage.y, stage.w, stage.h, colors.white, "B7CADA", "roundRect");

  if (existing.length === 1) {
    const inset = { x: stage.x + 160000, y: stage.y + 160000, w: stage.w - 320000, h: stage.h - 320000 };
    const fitted = fitImage(existing[0].file, inset);
    xml += pic(startId++, "rId20", fitted.x, fitted.y, fitted.w, fitted.h, `screenshot-${existing[0].key}`);
    return xml;
  }

  const gap = 130000;
  const box = { x: stage.x + 160000, y: stage.y + 160000, w: stage.w - 320000, h: Math.floor((stage.h - 320000 - gap) / 2) };
  existing.forEach((item, i) => {
    const slot = { ...box, y: box.y + i * (box.h + gap) };
    const fitted = fitImage(item.file, slot);
    xml += pic(startId++, `rId${20 + i}`, fitted.x, fitted.y, fitted.w, fitted.h, `screenshot-${item.key}`);
  });

  return xml;
}

function headerFooter(index, title) {
  let xml = "";
  xml += shape(2, "bg", 0, 0, CX, CY, colors.ice, colors.ice, "rect");
  xml += shape(3, "top-band", 0, 0, CX, 230000, colors.blue, colors.blue, "rect");
  xml += shape(4, "side-accent", 11390000, 0, 790000, CY, colors.sky, colors.sky, "rect");
  if (fs.existsSync(logoPath)) xml += pic(5, "rId2", 390000, 360000, 1280000, 590000, "KodFly logo");
  xml += textBox(6, "footer", 430000, 6280000, 1900000, 220000, `${String(index).padStart(2, "0")} / ${slides.length}`, { size: 1200, color: "7D8B97", align: "l", rtl: false });
  xml += textBox(7, "section-title", 9300000, 6240000, 2200000, 240000, "KodFly", { size: 1220, color: "7D8B97" });
  return xml;
}

function contentSlide(slide, index) {
  let id = 20;
  if (slide.screenshots) return screenshotSlide(slide, index);

  let xml = headerFooter(index, slide.title);
  xml += textBox(id++, "title", 6230000, 540000, 5060000, 560000, slide.title, { size: 3180, bold: true, color: colors.dark });
  xml += textBox(id++, "intro", 1460000, 1240000, 9820000, 640000, slide.intro, { size: 1800, color: colors.muted });
  xml += shape(id++, "intro-card", 6450000, 2140000, 4760000, 2850000, colors.white, colors.line, "roundRect");
  xml += textBox(id++, "bullets-title", 9280000, 2350000, 1700000, 260000, "עיקרי השלב", { size: 1500, bold: true, color: colors.blue });
  xml += bulletList(id++, 6760000, 2750000, 4080000, 1700000, slide.bullets);

  if (slide.screenshots) xml += screenshotGrid(slide.screenshots, id);
  else if (slide.diagram) xml += pillRow(slide.diagram, 5080000, id);
  else if (slide.flow) xml += pillRow(slide.flow, 5080000, id, 9600000, 1260000);
  else if (slide.blocks) xml += blocks(slide.blocks, id);
  else if (slide.screen) xml += screenMock(slide.screen, id);
  else if (slide.email) xml += emailMock(slide.email, id);
  else if (slide.final) {
    xml += shape(id++, "final-callout", 940000, 3720000, 4580000, 1420000, colors.sky, colors.blue, "roundRect");
    xml += textBox(id++, "final-title", 1260000, 3970000, 3920000, 340000, "מוכן להרצה והצגה", { align: "c", size: 2200, bold: true, color: colors.dark });
    xml += textBox(id++, "final-body", 1260000, 4430000, 3920000, 330000, "קובץ ההרצה מפעיל את המערכת בלי למחוק נתונים קיימים.", { align: "c", size: 1500, color: colors.ink });
  }

  return slideXml(xml);
}

function screenshotSlide(slide, index) {
  let id = 20;
  let xml = "";
  xml += shape(2, "bg", 0, 0, CX, CY, "071827", "071827", "rect");
  xml += shape(3, "blue-field", 7300000, 0, 4890000, CY, "EAF6FD", "EAF6FD", "rect");
  xml += shape(4, "accent-line", 7300000, 0, 115000, CY, colors.blue2, colors.blue2, "rect");
  if (fs.existsSync(logoPath)) xml += pic(5, "rId2", 8460000, 460000, 1200000, 555000, "KodFly logo");
  xml += screenshotShowcase(slide, id);
  id += 20;
  xml += textBox(id++, "title", 7710000, 1300000, 3440000, 700000, slide.title, { size: 3260, bold: true, color: colors.dark });
  xml += textBox(id++, "intro", 7710000, 2140000, 3400000, 850000, slide.intro, { size: 1670, color: colors.muted });
  xml += bulletList(id++, 7820000, 3290000, 3180000, 1420000, slide.bullets.slice(0, 3));
  xml += textBox(id++, "marker", 7740000, 5540000, 850000, 260000, String(index).padStart(2, "0"), { align: "l", rtl: false, size: 1500, bold: true, color: colors.blue });
  xml += textBox(id++, "footer-topic", 8700000, 5540000, 2450000, 260000, "מסכי המערכת בפועל", { size: 1320, color: "7D8B97" });
  return slideXml(xml);
}

function coverSlide(slide) {
  let id = 20;
  let xml = "";
  xml += shape(2, "cover-bg", 0, 0, CX, CY, "071827", "071827", "rect");
  xml += shape(3, "cover-field", 6880000, 0, 5312000, CY, "EAF6FD", "EAF6FD", "rect");
  if (fs.existsSync(planePath)) xml += pic(4, "rId3", 3600000, 520000, 8200000, 5480000, "airplane", 45000);
  xml += shape(5, "title-line", 6480000, 1320000, 90000, 3020000, colors.blue2, colors.blue2, "rect");
  if (fs.existsSync(logoPath)) xml += pic(6, "rId2", 700000, 520000, 1720000, 790000, "KodFly logo");
  xml += textBox(id++, "subtitle", 7090000, 1280000, 3740000, 340000, slide.subtitle, { size: 1740, bold: true, color: colors.blue });
  xml += textBox(id++, "title", 7060000, 1960000, 4140000, 1160000, slide.title, { size: 4700, bold: true, color: colors.dark });
  xml += textBox(id++, "authors", 7060000, 3360000, 4020000, 420000, slide.authors, { size: 2140, color: colors.ink });
  xml += pillRow(slide.chips, 4730000, id, 5120000, 5580000);
  xml += textBox(90, "date", 700000, 5880000, 2900000, 300000, "מצגת מוצר, קוד ותהליכי מערכת", { size: 1450, color: "B7C9D6" });
  return slideXml(xml);
}

function slideXml(content) {
  return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sld xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:cSld><p:spTree>
    <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
    <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="${CX}" cy="${CY}"/><a:chOff x="0" y="0"/><a:chExt cx="${CX}" cy="${CY}"/></a:xfrm></p:grpSpPr>
    ${content}
  </p:spTree></p:cSld>
  <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
</p:sld>`;
}

function writeBaseFiles() {
  write(path.join(buildDir, "[Content_Types].xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Default Extension="png" ContentType="image/png"/>
  <Default Extension="jpeg" ContentType="image/jpeg"/>
  <Default Extension="jpg" ContentType="image/jpeg"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/ppt/presentation.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"/>
  <Override PartName="/ppt/slideMasters/slideMaster1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml"/>
  <Override PartName="/ppt/slideLayouts/slideLayout1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml"/>
  <Override PartName="/ppt/theme/theme1.xml" ContentType="application/vnd.openxmlformats-officedocument.theme+xml"/>
${slides.map((_, i) => `  <Override PartName="/ppt/slides/slide${i + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slide+xml"/>`).join("\n")}
</Types>`);

  write(path.join(buildDir, "_rels", ".rels"), relsXml([
    { id: "rId1", type: relTypes.officeDocument, target: "ppt/presentation.xml" },
    { id: "rId2", type: relTypes.core, target: "docProps/core.xml" },
    { id: "rId3", type: relTypes.app, target: "docProps/app.xml" },
  ]));

  write(path.join(buildDir, "docProps", "core.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>מערכת לניהול טיסות</dc:title>
  <dc:creator>Codex</dc:creator>
  <cp:lastModifiedBy>Codex</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">2026-10-13T00:00:00Z</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">2026-10-13T00:00:00Z</dcterms:modified>
</cp:coreProperties>`);

  write(path.join(buildDir, "docProps", "app.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>Microsoft PowerPoint</Application>
  <PresentationFormat>Widescreen</PresentationFormat>
  <Slides>${slides.length}</Slides>
  <ScaleCrop>false</ScaleCrop>
  <Company>KodFly</Company>
</Properties>`);

  write(path.join(buildDir, "ppt", "presentation.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:presentation xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:sldMasterIdLst><p:sldMasterId id="2147483648" r:id="rId1"/></p:sldMasterIdLst>
  <p:sldIdLst>
${slides.map((_, i) => `    <p:sldId id="${256 + i}" r:id="rId${i + 2}"/>`).join("\n")}
  </p:sldIdLst>
  <p:sldSz cx="${CX}" cy="${CY}" type="wide"/>
  <p:notesSz cx="6858000" cy="9144000"/>
</p:presentation>`);

  write(path.join(buildDir, "ppt", "_rels", "presentation.xml.rels"), relsXml([
    { id: "rId1", type: relTypes.slideMaster, target: "slideMasters/slideMaster1.xml" },
    ...slides.map((_, i) => ({ id: `rId${i + 2}`, type: relTypes.slide, target: `slides/slide${i + 1}.xml` })),
  ]));

  write(path.join(buildDir, "ppt", "slideMasters", "slideMaster1.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sldMaster xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="${CX}" cy="${CY}"/><a:chOff x="0" y="0"/><a:chExt cx="${CX}" cy="${CY}"/></a:xfrm></p:grpSpPr></p:spTree></p:cSld>
  <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2" accent1="accent1" accent2="accent2" accent3="accent3" accent4="accent4" accent5="accent5" accent6="accent6" hlink="hlink" folHlink="folHlink"/>
  <p:sldLayoutIdLst><p:sldLayoutId id="2147483649" r:id="rId1"/></p:sldLayoutIdLst>
  <p:txStyles><p:titleStyle/><p:bodyStyle/><p:otherStyle/></p:txStyles>
</p:sldMaster>`);
  write(path.join(buildDir, "ppt", "slideMasters", "_rels", "slideMaster1.xml.rels"), relsXml([
    { id: "rId1", type: relTypes.slideLayout, target: "../slideLayouts/slideLayout1.xml" },
    { id: "rId2", type: relTypes.theme, target: "../theme/theme1.xml" },
  ]));

  write(path.join(buildDir, "ppt", "slideLayouts", "slideLayout1.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sldLayout xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" type="blank" preserve="1">
  <p:cSld name="Blank"><p:spTree><p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="${CX}" cy="${CY}"/><a:chOff x="0" y="0"/><a:chExt cx="${CX}" cy="${CY}"/></a:xfrm></p:grpSpPr></p:spTree></p:cSld>
  <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
</p:sldLayout>`);
  write(path.join(buildDir, "ppt", "slideLayouts", "_rels", "slideLayout1.xml.rels"), relsXml([
    { id: "rId1", type: relTypes.slideMaster, target: "../slideMasters/slideMaster1.xml" },
  ]));

  write(path.join(buildDir, "ppt", "theme", "theme1.xml"), `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="KodFly">
  <a:themeElements>
    <a:clrScheme name="KodFly"><a:dk1><a:srgbClr val="${colors.dark}"/></a:dk1><a:lt1><a:srgbClr val="${colors.white}"/></a:lt1><a:dk2><a:srgbClr val="${colors.ink}"/></a:dk2><a:lt2><a:srgbClr val="${colors.ice}"/></a:lt2><a:accent1><a:srgbClr val="${colors.blue}"/></a:accent1><a:accent2><a:srgbClr val="${colors.blue2}"/></a:accent2><a:accent3><a:srgbClr val="53B987"/></a:accent3><a:accent4><a:srgbClr val="FFBE55"/></a:accent4><a:accent5><a:srgbClr val="6B6BE8"/></a:accent5><a:accent6><a:srgbClr val="F07A63"/></a:accent6><a:hlink><a:srgbClr val="${colors.blue}"/></a:hlink><a:folHlink><a:srgbClr val="6B6BE8"/></a:folHlink></a:clrScheme>
    <a:fontScheme name="KodFly"><a:majorFont><a:latin typeface="Arial"/><a:cs typeface="Arial"/></a:majorFont><a:minorFont><a:latin typeface="Arial"/><a:cs typeface="Arial"/></a:minorFont></a:fontScheme>
    <a:fmtScheme name="KodFly"><a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:fillStyleLst><a:lnStyleLst><a:ln w="6350"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme>
  </a:themeElements>
</a:theme>`);
}

function build() {
  cleanDir(buildDir);
  fs.mkdirSync(outDir, { recursive: true });
  writeBaseFiles();
  if (fs.existsSync(logoPath)) copy(logoPath, path.join(buildDir, "ppt", "media", "kodfly-logo.png"));
  if (fs.existsSync(planePath)) copy(planePath, path.join(buildDir, "ppt", "media", "airplane.png"));
  Object.values(screenshotDefs).forEach((def) => {
    const source = path.join(screenshotDir, def.file);
    if (fs.existsSync(source)) copy(source, path.join(buildDir, "ppt", "media", def.media));
  });

  slides.forEach((slide, i) => {
    const n = i + 1;
    write(path.join(buildDir, "ppt", "slides", `slide${n}.xml`), slide.type === "cover" ? coverSlide(slide) : contentSlide(slide, n));
    const rels = [{ id: "rId1", type: relTypes.slideLayout, target: "../slideLayouts/slideLayout1.xml" }];
    if (fs.existsSync(logoPath)) rels.push({ id: "rId2", type: relTypes.image, target: "../media/kodfly-logo.png" });
    if (n === 1 && fs.existsSync(planePath)) rels.push({ id: "rId3", type: relTypes.image, target: "../media/airplane.png" });
    (slide.screenshots ?? []).slice(0, 2).forEach((key, index) => {
      const media = mediaNameFor(key);
      const source = imagePathFor(key);
      if (media && source && fs.existsSync(source)) {
        rels.push({ id: `rId${20 + index}`, type: relTypes.image, target: `../media/${media}` });
      }
    });
    write(path.join(buildDir, "ppt", "slides", "_rels", `slide${n}.xml.rels`), relsXml(rels));
  });

  if (fs.existsSync(finalPath)) fs.rmSync(finalPath);
  const zipPath = `${finalPath}.zip`;
  if (fs.existsSync(zipPath)) fs.rmSync(zipPath);
  execFileSync("powershell.exe", [
    "-NoProfile",
    "-Command",
    `Compress-Archive -Path '${buildDir}\\*' -DestinationPath '${zipPath}' -Force`,
  ], { stdio: "inherit" });
  fs.renameSync(zipPath, finalPath);
  console.log(finalPath);
}

build();

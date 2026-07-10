export const code = async (inputs) => {
  // Nhận output từ bước 5
  const source =
    inputs.rows ??
    inputs["Key: rows"] ??
    inputs.data ??
    [];

  function findRows(x) {
    if (Array.isArray(x)) return x;

    if (x && typeof x === "object") {
      if (Array.isArray(x.rows)) return x.rows;
      if (Array.isArray(x.data)) return x.data;
      if (Array.isArray(x.values)) return x.values;
      if (Array.isArray(x.items)) return x.items;
      if (Array.isArray(x.output)) return x.output;
      if (Array.isArray(x.result)) return x.result;
    }

    return [];
  }

  let rows = findRows(source);

  if (rows.length === 0) {
    return {
      LOI: "Chưa nhận được dữ liệu từ bước 5. Hãy thêm input tên rows và kéo output của bước 5 vào.",
      INPUT_KEYS: Object.keys(inputs ?? {}),
      INPUT_HIEN_TAI: inputs,
    };
  }

  // Chuẩn hóa nếu row có dạng { json: {...} }
  rows = rows.map(r => r?.json ?? r);

  // ========= Xóa trùng CHUYEN =========
  const map = new Map();

  for (const row of rows) {
    if (!row?.CHUYEN) continue;

    // Nếu trùng C10, C9... thì giữ dòng đầu tiên
    if (!map.has(row.CHUYEN)) {
      map.set(row.CHUYEN, row);
    }
  }

  rows = [...map.values()];

  // ========= Lấy giờ Việt Nam =========
  const TZ = "Asia/Ho_Chi_Minh";
  const nowVN = new Date(new Date().toLocaleString("en-US", { timeZone: TZ }));

  const hour = nowVN.getHours();
  const minute = nowVN.getMinutes();

  const GIO_HIEN_TAI =
    String(hour).padStart(2, "0") + ":" + String(minute).padStart(2, "0");

  // ========= Ép số an toàn =========
  function toNumberSafe(v) {
    if (v === null || v === undefined) return 0;

    if (typeof v === "string") {
      const t = v.trim().toLowerCase();

      if (
        t === "" ||
        t === "empty" ||
        t === "null" ||
        t === "undefined" ||
        t === "#n/a" ||
        t === "#div/0!"
      ) {
        return 0;
      }

      const n = Number(t.replace(/,/g, "").replace(/%/g, ""));
      return Number.isFinite(n) ? n : 0;
    }

    const n = Number(v);
    return Number.isFinite(n) ? n : 0;
  }

  // ========= Chọn cột AFTER theo giờ hiện tại =========
  function getAfterColumnName() {
    if (hour === 9) return "AFTER 9H";
    if (hour === 10) return "AFTER 10H";
    if (hour === 11) return "AFTER 11H";

    if (hour === 12) return "AFTER 12H30";
    if (hour === 13) return "AFTER 13H30";
    if (hour === 14) return "AFTER 14H30";
    if (hour === 15) return "AFTER 15H30";

    if (hour >= 16) return "AFTER 16H30";

    return "";
  }

  const COT_SAN_LUONG = getAfterColumnName();

  // ========= Output =========
  return rows.map(row => {
    const DM_NGAY = toNumberSafe(row["DM/NGAY"]);
    const DM_GIO = toNumberSafe(row["DM/GIO"]);

    const SAN_LUONG_GIO = COT_SAN_LUONG
      ? toNumberSafe(row[COT_SAN_LUONG])
      : 0;

    return {
      CHUYEN: row.CHUYEN,

      "DM/NGAY": DM_NGAY,
      "DM/GIO": DM_GIO,

      GIO_HIEN_TAI,
      COT_SAN_LUONG,
      SAN_LUONG_GIO,
    };
  });
};
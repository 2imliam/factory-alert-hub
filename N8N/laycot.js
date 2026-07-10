export const code = async (inputs) => {
  const rows = inputs.excel ?? [];

  if (!Array.isArray(rows) || rows.length === 0) {
    return {
      LOI: "Chưa nhận được dữ liệu từ bước 4. Hãy thêm input tên excel và kéo output của bước 4 vào.",
      INPUT_KEYS: Object.keys(inputs ?? {}),
      INPUT_HIEN_TAI: inputs,
    };
  }

  // ========= Helper =========
  const clean = (v) =>
    String(v ?? "")
      .replace(/\u00A0/g, " ")
      .replace(/[\u200B-\u200D\uFEFF]/g, "")
      .replace(/[\r\n]+/g, " ")
      .trim();

  function norm(v) {
    const s = clean(v);
    if (!s) return "";

    const low = s.toLowerCase();

    if (
      low === "empty" ||
      low === "undefined" ||
      low === "null" ||
      low === "#n/a" ||
      low === "#div/0!"
    ) {
      return "";
    }

    return s;
  }

  function toNum(v) {
    const s = norm(v);
    if (!s) return 0;

    const n = Number(
      s
        .replace(/,/g, "")
        .replace(/%/g, "")
    );

    return Number.isFinite(n) ? n : 0;
  }

  function toNumNull(v) {
    const s = norm(v);
    if (!s) return null;

    const n = Number(
      s
        .replace(/,/g, "")
        .replace(/%/g, "")
    );

    return Number.isFinite(n) ? n : null;
  }

  function getValues(row) {
    const r = row?.json ?? row;
    return r?.values ?? r ?? {};
  }

  function getCell(values, col) {
    return values?.[col] ?? "";
  }

  // ========= Map cột Excel =========
  const COL = {
    CHUYEN: "A",

    DM_NGAY: "H",
    DM_GIO: "I",

    AFTER_9H: "J",
    AFTER_10H: "K",
    AFTER_11H: "L",
    AFTER_12H30: "M",
    AFTER_13H30: "N",
    AFTER_14H30: "O",
    AFTER_15H30: "P",
    AFTER_16H30: "Q",
  };

  function isStopKey(raw) {
    const s = norm(raw).toUpperCase();
    return s === "CẮT" || s === "CAT";
  }

  function getChuyen(raw) {
    const s = norm(raw).toUpperCase();
    const m = s.match(/^C\s*0*(10|[1-9])$/i);
    return m ? `C${Number(m[1])}` : "";
  }

  function createGroup(chuyen) {
    return {
      CHUYEN: chuyen,

      DM_NGAY: 0,
      DM_GIO: 0,

      AFTER_9H: 0,
      AFTER_10H: 0,
      AFTER_11H: 0,
      AFTER_12H30: 0,
      AFTER_13H30: 0,
      AFTER_14H30: 0,
      AFTER_15H30: 0,
      AFTER_16H30: 0,
    };
  }

  function addRow(g, values) {
    g.DM_NGAY += toNum(getCell(values, COL.DM_NGAY));
    g.DM_GIO += toNum(getCell(values, COL.DM_GIO));

    const n9 = toNumNull(getCell(values, COL.AFTER_9H));
    let prev = n9 === null ? 0 : n9;

    const take = (raw) => {
      const n = toNumNull(raw);

      if (n === null) {
        return prev;
      }

      prev = n;
      return n;
    };

    const after10 = take(getCell(values, COL.AFTER_10H));
    const after11 = take(getCell(values, COL.AFTER_11H));
    const after12h30 = take(getCell(values, COL.AFTER_12H30));
    const after13h30 = take(getCell(values, COL.AFTER_13H30));
    const after14h30 = take(getCell(values, COL.AFTER_14H30));
    const after15h30 = take(getCell(values, COL.AFTER_15H30));
    const after16h30 = take(getCell(values, COL.AFTER_16H30));

    g.AFTER_9H += n9 === null ? 0 : n9;
    g.AFTER_10H += after10;
    g.AFTER_11H += after11;
    g.AFTER_12H30 += after12h30;
    g.AFTER_13H30 += after13h30;
    g.AFTER_14H30 += after14h30;
    g.AFTER_15H30 += after15h30;
    g.AFTER_16H30 += after16h30;
  }

  // ========= Gom dữ liệu theo chuyền =========
  const groups = [];
  let currentGroup = null;
  let currentChuyen = "";

  for (const row of rows) {
    const values = getValues(row);

    const rawChuyen = getCell(values, COL.CHUYEN);
    const chuyen = getChuyen(rawChuyen);

    if (isStopKey(rawChuyen)) {
      if (currentGroup) groups.push(currentGroup);
      break;
    }

    if (chuyen) {
      if (currentGroup && chuyen !== currentChuyen) {
        groups.push(currentGroup);
        currentGroup = createGroup(chuyen);
        currentChuyen = chuyen;
      } else if (!currentGroup) {
        currentGroup = createGroup(chuyen);
        currentChuyen = chuyen;
      }

      addRow(currentGroup, values);
      continue;
    }

    if (currentGroup) {
      addRow(currentGroup, values);
    }
  }

  if (currentGroup) {
    groups.push(currentGroup);
  }

  function chuyenNo(c) {
    const m = String(c).match(/^C(\d+)$/i);
    return m ? Number(m[1]) : 999;
  }

  const result = groups
    .sort((a, b) => chuyenNo(a.CHUYEN) - chuyenNo(b.CHUYEN))
    .map(g => ({
      CHUYEN: g.CHUYEN,

      "DM/NGAY": g.DM_NGAY,
      "DM/GIO": g.DM_GIO,

      "AFTER 9H": g.AFTER_9H,
      "AFTER 10H": g.AFTER_10H,
      "AFTER 11H": g.AFTER_11H,
      "AFTER 12H30": g.AFTER_12H30,
      "AFTER 13H30": g.AFTER_13H30,
      "AFTER 14H30": g.AFTER_14H30,
      "AFTER 15H30": g.AFTER_15H30,
      "AFTER 16H30": g.AFTER_16H30,
    }))
    .filter(r => r["DM/NGAY"] > 0);

  if (result.length === 0) {
    return {
      LOI: "Có dữ liệu nhưng không tìm thấy chuyền C1-C10 hoặc DM/NGAY = 0.",
      SO_DONG_NHAN_DUOC: rows.length,
      HANG_MAU: rows[0],
    };
  }

  return result;
};
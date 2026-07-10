export const code = async (inputs) => {
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

  let rows = findRows(source).map(r => r?.json ?? r);

  if (rows.length === 0) {
    return {
      LOI: "Chưa nhận được dữ liệu từ bước trước.",
      INPUT_KEYS: Object.keys(inputs ?? {}),
      INPUT_HIEN_TAI: inputs,
    };
  }

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

  // Xóa trùng chuyền nếu có
  const map = new Map();

  for (const row of rows) {
    if (!row.CHUYEN) continue;

    if (!map.has(row.CHUYEN)) {
      map.set(row.CHUYEN, row);
    }
  }

  rows = [...map.values()];

  const DANH_SACH_KIEM_TRA = rows.map(row => {
    const dmGio = toNumberSafe(row["DM/GIO"]);
    const sanLuongGio = toNumberSafe(row.SAN_LUONG_GIO);

    const khongDat = sanLuongGio < dmGio;
    const thieu = khongDat ? dmGio - sanLuongGio : 0;
    const vuot = sanLuongGio > dmGio ? sanLuongGio - dmGio : 0;

    return {
      CHUYEN: row.CHUYEN,
      GIO_HIEN_TAI: row.GIO_HIEN_TAI,

      "DM/GIO": dmGio,
      SAN_LUONG_GIO: sanLuongGio,

      DAT: !khongDat,
      KHONG_DAT: khongDat,

      THIEU: thieu,
      VUOT: vuot,

      TRANG_THAI: khongDat ? "KHÔNG ĐẠT" : "ĐẠT",
    };
  });

  return DANH_SACH_KIEM_TRA;
};
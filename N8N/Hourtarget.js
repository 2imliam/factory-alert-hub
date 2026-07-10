export const code = async (inputs) => {
  // rows = output từ bước trước
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
      LOI: "Chưa nhận được dữ liệu từ bước trước. Hãy thêm input tên rows và kéo output bước trước vào.",
      INPUT_KEYS: Object.keys(inputs ?? {}),
      INPUT_HIEN_TAI: inputs,
    };
  }

  rows = rows.map(r => r?.json ?? r);

  // ===== LẤY GIỜ HIỆN TẠI TỪ DỮ LIỆU =====
  const gioHienTai = rows[0]?.GIO_HIEN_TAI || "00:00";
  const hour = Number(String(gioHienTai).split(":")[0]);

  // ===== XÁC ĐỊNH HỆ SỐ GIỜ LŨY TIẾN =====
  let heSo = 0;

  if (hour === 9) heSo = 1;
  else if (hour === 10) heSo = 2;
  else if (hour === 11) heSo = 3;
  else if (hour === 12) heSo = 4;
  else if (hour === 13) heSo = 5;
  else if (hour === 14) heSo = 6;
  else if (hour === 15) heSo = 7;
  else if (hour >= 16) heSo = 8;

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

  // ===== TÍNH LẠI DM/GIO LŨY TIẾN =====
  return rows.map(row => {
    const dmGioGoc = toNumberSafe(row["DM/GIO"]);
    const dmGioLuyTien = dmGioGoc * heSo;

    return {
      ...row,

      HE_SO_GIO: heSo,

      // Giữ lại định mức giờ gốc để kiểm tra
      "DM/GIO_GOC": dmGioGoc,

      // Ghi đè DM/GIO thành định mức lũy tiến
      "DM/GIO": dmGioLuyTien,
    };
  });
};
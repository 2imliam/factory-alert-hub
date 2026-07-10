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

  const rows = findRows(source).map(r => r?.json ?? r);

  if (rows.length === 0) {
    return {
      message: "Không có dữ liệu kiểm tra sản lượng."
    };
  }

  function getChuyenNumber(chuyen) {
    const s = String(chuyen || "").trim();
    const m = s.match(/^C\s*0*([0-9]+)$/i);
    return m ? Number(m[1]) : null;
  }

  function formatChuyenList(list) {
    const nums = list
      .map(row => getChuyenNumber(row.CHUYEN))
      .filter(n => typeof n === "number" && Number.isFinite(n));

    const uniqueSorted = [...new Set(nums)].sort((a, b) => a - b);

    return uniqueSorted.map(n => `chuyền ${n}`).join(", ");
  }

  const danhSachDat = rows.filter(row => row.DAT === true);
  const danhSachKhongDat = rows.filter(row => row.KHONG_DAT === true);

  const dsDat = formatChuyenList(danhSachDat);
  const dsKhongDat = formatChuyenList(danhSachKhongDat);

  const parts = [];

  if (dsDat) {
    parts.push(
      `Sản lượng các ${dsDat} đã đạt định mức. Xin chúc mừng và khen ngợi tinh thần làm việc của chuyền trưởng cùng toàn thể anh chị em công nhân. Mong mọi người tiếp tục phát huy để giữ vững kết quả tốt trong các giờ tiếp theo.`
    );
  }

  if (dsKhongDat) {
    parts.push(
      `Sản lượng các ${dsKhongDat} chưa đạt định mức. Đề nghị chuyền trưởng và toàn thể anh chị em công nhân cố gắng đẩy nhanh tiến độ để đạt sản lượng ở giờ tiếp theo.`
    );
  }

  const message = parts.join("\n\n");

  return {
    message
  };
};
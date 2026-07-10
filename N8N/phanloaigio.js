export const code = async (inputs) => {
  const now = new Date();

  // Việt Nam UTC+7
  const vietnamTime = new Date(now.getTime() + 7 * 60 * 60 * 1000);

  const hour = vietnamTime.getUTCHours();
  const minute = vietnamTime.getUTCMinutes();

  const caLamTimes = [
    "9:15",
    "10:15",
    "11:15",
    "12:45",
    "13:45",
    "14:45",
    "15:45"
  ];

  const ca = caLamTimes.includes(`${hour}:${minute}`)
    ? "CA LÀM"
    : "KHÔNG PHẢI CA LÀM";

  return {
    hour,
    minute,
    ca,
    time: `${hour}:${minute}`
  };
};
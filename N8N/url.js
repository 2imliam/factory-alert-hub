export const code = async (inputs) => {
  const apiKey = "xxxxxx";

  const message =
    inputs.message ??
    inputs.text ??
    inputs.data?.message ??
    inputs.rows?.message ??
    "";

  return {
    url: "xxxxxxxxxxxx",
    apiKey,
    factoryId: "xxxxx",
    text: xxxx
  };
};
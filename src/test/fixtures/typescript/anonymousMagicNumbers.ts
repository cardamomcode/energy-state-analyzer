const DELAY = 37;

export const cleanConstant = () => schedule(DELAY);

export const flaggedArrow = () => schedule(37);

export const flaggedFunctionExpression = function () {
  return schedule(41);
};

export const flaggedCallback = () => register(() => schedule(43));

export function flaggedNamed() {
  return schedule(47);
}

export const cleanDefault = (delay = 53) => schedule(delay);

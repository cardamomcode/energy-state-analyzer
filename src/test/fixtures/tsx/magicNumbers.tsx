const DELAY = 37;

export const CleanConstant = () => <Widget delay={DELAY} />;

export const FlaggedArrow = () => <Widget delay={37} />;

export const FlaggedFunctionExpression = function () {
  return <Widget delay={41} />;
};

export const FlaggedCallback = () => <button onClick={() => schedule(43)}>Schedule</button>;

export function FlaggedNamed() {
  return <Widget delay={47} />;
}

export const CleanDefault = (delay = 53) => <Widget delay={delay} />;

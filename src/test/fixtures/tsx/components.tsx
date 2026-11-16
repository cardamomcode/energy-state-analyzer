import type { ReactNode } from "react";

type Props = { active: boolean; items: string[]; children?: ReactNode };

export function CleanMarkup({ children }: Props) {
  return <main><section><article><div><span>{children}</span></div></article></section></main>;
}

export function FlaggedBranches({ items }: Props) {
  if (items.length === 0) return <p>Empty</p>;
  if (items.length === 1) return <p>One</p>;
  if (items.length === 2) return <p>Two</p>;
  if (items.length === 3) return <p>Three</p>;
  if (items.length === 4) return <p>Four</p>;
  if (items.length === 5) return <p>Five</p>;
  if (items.length === 6) return <p>6 items</p>;
  if (items.length === 7) return <p>7 items</p>;
  if (items.length === 8) return <p>8 items</p>;
  if (items.length === 9) return <p>9 items</p>;
  if (items.length === 10) return <p>10 items</p>;
  return <ul>{items.map(item => <li key={item}>{item}</li>)}</ul>;
}

export const ExpressionComponent = ({ active, items }: Props) => (
  <>
    {active ? <strong>Ready</strong> : <em>Waiting</em>}
    {items.map(item => item ? <span>{item}</span> : <br />)}
    <button onClick={() => save(true)}>Save</button>
  </>
);

export function FlaggedParameters(a: number, b: number, c: number, d: number, e: number, f: number) {
  return <output>{a + b + c + d + e + f}</output>;
}

export const GenericComponent = <T,>({ value }: { value: T }) => <span>{String(value)}</span>;

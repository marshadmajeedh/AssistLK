import {
  cardStyles,
} from "../theme";

function AppCard({
  children,
  style = {},
}) {
  return (
    <div
      style={{
        ...cardStyles,
        ...style,
      }}
    >
      {children}
    </div>
  );
}

export default AppCard;

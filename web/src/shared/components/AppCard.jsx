import {
  cardStyles,
} from "../theme";

function AppCard({
  children,
  className = "",
  style = {},
}) {
  return (
    <div
      className={className ? `app-card ${className}` : "app-card"}
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

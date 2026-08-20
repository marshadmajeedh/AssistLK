import {
  colors,
  typography,
} from "../theme";

function PlaceholderPage({ title }) {
  return (
    <div>
      <h1
        style={{
          ...typography.pageTitle,
          color: colors.textPrimary,
        }}
      >
        {title}
      </h1>

      <p
        style={{
          ...typography.body,
          color: colors.textSecondary,
        }}
      >
        This module will be implemented by its
        assigned component owner.
      </p>
    </div>
  );
}

export default PlaceholderPage;

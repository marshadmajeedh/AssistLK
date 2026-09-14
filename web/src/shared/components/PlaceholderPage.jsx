import {
  colors,
  typography,
} from "../theme";

function PlaceholderPage({ title }) {
  return (
    <div className="app-page">
      <section className="page-hero">
        <div className="page-hero-content">
          <div>
            <div className="page-kicker">Module Surface</div>
            <h1 className="page-hero-title">{title}</h1>
            <p className="page-hero-copy">This route already exists in the shared application shell and is ready for its assigned component implementation.</p>
          </div>
        </div>
      </section>
      <div
        className="glass-panel"
        style={{ padding: 28 }}
      >
        <p
          style={{
            ...typography.body,
            color: colors.textSecondary,
          }}
        >
          This module will be implemented by its assigned component owner.
        </p>
      </div>
    </div>
  );
}

export default PlaceholderPage;

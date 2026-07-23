const { NavRail, Icon } = window.NagareDesignSystem_9475eb;
const { useState } = React;

function TitleBar() {
  return (
    <div className="win-titlebar">
      <div className="win-title"><span className="win-mark">流</span><span>Nagare</span></div>
      <div className="win-caption">
        <div className="win-cap-btn" aria-hidden="true"><span className="m-min" /></div>
        <div className="win-cap-btn" aria-hidden="true"><span className="m-max" /></div>
        <div className="win-cap-btn win-close" aria-hidden="true"><Icon name="x" size={14} /></div>
      </div>
    </div>
  );
}

// The Fluent shell: Windows caption bar + NavigationView rail + content sheet.
// A reserved, disabled "Planifications" slot holds space for iteration 2.
function AppShell({ page, onNavigate, children }) {
  const items = [
    { tag: "dashboard", label: "Tableau de bord", icon: "gauge" },
    { tag: "profiles", label: "Profils", icon: "sliders-horizontal" },
    { tag: "channels", label: "Channels", icon: "radio-tower" },
    { tag: "scheduling", label: "Planifications", icon: "calendar-clock", disabled: true, soon: true },
  ];
  return (
    <div className="win">
      <TitleBar />
      <div className="win-body">
        <div className="win-nav">
          <NavRail selected={page} onSelect={onNavigate} items={items} />
        </div>
        <main className="win-content">{children}</main>
      </div>
    </div>
  );
}

Object.assign(window, { AppShell });

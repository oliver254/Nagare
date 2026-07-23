**NavRail** is the Fluent `NavigationView` left pane. Three destinations today, with a reserved disabled slot for the upcoming Planifications page.

```jsx
<NavRail
  selected="dashboard"
  onSelect={setPage}
  items={[
    { tag: "dashboard", label: "Tableau de bord", icon: "gauge" },
    { tag: "profiles", label: "Profils", icon: "sliders-horizontal" },
    { tag: "channels", label: "Channels", icon: "radio-tower" },
    { tag: "scheduling", label: "Planifications", icon: "calendar-clock", disabled: true, soon: true },
  ]}
/>
```

- Selected item shows the accent pill + accent icon; `aria-current="page"`.
- Add the redesign's icons (the current shell ships without them).
- Don't invent custom navigation — this is the Windows-standard pattern (Jakob's Law).

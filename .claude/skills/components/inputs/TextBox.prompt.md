**TextBox** is the Fluent text field with an optional `header` label above it and a focus underline in the accent color.

```jsx
<TextBox header="Nom" placeholder="Ma chaîne principale" />
<TextBox header="URL de base" defaultValue="rtmp://live.twitch.tv/app" />
<TextBox header="Nom" error="Channel name cannot be empty." />
<TextBox mono readOnly value="ffmpeg -re -stream_loop -1 -i in.mp4 …" multiline />
```

- `header` / `hint` / `error` — label, helper, and error text. Put the domain's own validation message into `error`; never rewrite rules in the UI.
- `mono` — monospace, for the command preview and RTMP URLs.
- `multiline` — renders a resizable `<textarea>`.
- Standard input props (`value`, `defaultValue`, `onChange`, `placeholder`, `readOnly`, `disabled`) pass straight through.

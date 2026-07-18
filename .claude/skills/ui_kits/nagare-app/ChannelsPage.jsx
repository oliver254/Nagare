const DS = window.NagareDesignSystem_9475eb;
const { useState } = React;

// Channels CRUD. The stream key is entered in a PasswordBox and NEVER read back
// (ADR-0005): editing starts with an empty key field; empty = keep current key.
// Validation messages are the domain's own (surfaced in an InfoBar), not rewritten.
function ChannelsPage() {
  const { Card, Button, IconButton, TextBox, ComboBox, PasswordBox, InfoBar, EmptyState, ContentDialog, Icon } = DS;
  const seed = window.NagareSeed;
  const [channels, setChannels] = useState(seed.channels.map((c) => ({ ...c })));
  const [selectedId, setSelectedId] = useState(channels[0]?.id ?? null);
  const [editing, setEditing] = useState(null); // null | "new" | id
  const [form, setForm] = useState(null);
  const [error, setError] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);

  const selected = channels.find((c) => c.id === selectedId) || null;
  const isCreating = editing === "new";
  const platformLabel = (p) => (p === "CustomRtmp" ? "RTMP custom" : p);
  const defaultUrl = (p) => (p === "Twitch" ? "rtmp://live.twitch.tv/app" : p === "YouTube" ? "rtmp://a.rtmp.youtube.com/live2" : "");

  const startNew = () => { setError(null); setEditing("new"); setForm({ name: "", platform: "Twitch", baseUrl: defaultUrl("Twitch"), key: "" }); };
  const startEdit = () => { if (!selected) return; setError(null); setEditing(selected.id); setForm({ name: selected.name, platform: selected.platform, baseUrl: selected.baseUrl, key: "" }); };
  const cancel = () => { setEditing(null); setError(null); };

  const save = () => {
    // Mimic the domain invariants surfaced verbatim to the UI.
    if (!form.name.trim()) return setError("Channel name cannot be empty.");
    const url = form.baseUrl.trim().toLowerCase();
    if (!url.startsWith("rtmp://") && !url.startsWith("rtmps://")) return setError("Base URL must use the rtmp:// or rtmps:// scheme.");
    if (isCreating && !form.key) return setError("A protected stream key is required.");
    if (isCreating) {
      const id = "c" + Date.now();
      setChannels((cs) => [...cs, { id, name: form.name.trim(), platform: form.platform, baseUrl: form.baseUrl.trim(), keyConfigured: true }]);
      setSelectedId(id);
    } else {
      setChannels((cs) => cs.map((c) => (c.id === editing ? { ...c, name: form.name.trim(), platform: form.platform, baseUrl: form.baseUrl.trim(), keyConfigured: c.keyConfigured || !!form.key } : c)));
    }
    setEditing(null); setError(null);
  };

  const confirmDelete = () => {
    setChannels((cs) => cs.filter((c) => c.id !== deleteTarget.id));
    if (selectedId === deleteTarget.id) setSelectedId(null);
    if (editing === deleteTarget.id) setEditing(null);
    setDeleteTarget(null);
  };

  return (
    <div className="page">
      <div className="page-head">
        <h1 className="page-title">Channels</h1>
        <p className="page-sub">Vos destinations de diffusion. La clé de stream est chiffrée et n'est jamais réaffichée.</p>
      </div>

      <div className="toolbar">
        <Button variant="accent" icon="plus" onClick={startNew}>Nouveau channel</Button>
        <Button icon="pencil" disabled={!selected} onClick={startEdit}>Modifier</Button>
        <Button variant="danger" icon="trash-2" disabled={!selected} onClick={() => setDeleteTarget(selected)}>Supprimer</Button>
      </div>

      {channels.length === 0 ? (
        <Card>
          <EmptyState icon="radio-tower" title="Aucun channel"
            message="Créez un channel pour choisir où diffuser — Twitch, YouTube ou un RTMP custom."
            action={<Button variant="accent" icon="plus" onClick={startNew}>Nouveau channel</Button>} />
        </Card>
      ) : (
        <div className="crud">
          <div className="list">
            {channels.map((c) => (
              <button key={c.id} className={"list-row" + (c.id === selectedId ? " list-row--sel" : "")} onClick={() => setSelectedId(c.id)}>
                <Icon className="list-icon" name={c.platform === "YouTube" ? "radio" : "radio-tower"} size={18} />
                <div className="list-row__body">
                  <span className="list-name">{c.name}</span>
                  <span className="list-sub">{platformLabel(c.platform)} · {c.baseUrl}</span>
                </div>
                <span className="chip"><Icon name="check" size={12} style={{ marginRight: 4 }} />Clé configurée</span>
              </button>
            ))}
          </div>

          {editing !== null && form && (
            <Card title={isCreating ? "Nouveau channel" : "Modifier le channel"} icon="radio-tower">
              <div className="editor">
                {error && <InfoBar severity="error" title="Réglage refusé" message={error} isClosable onClose={() => setError(null)} />}
                <TextBox header="Nom" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Ma chaîne principale" />
                <ComboBox header="Plateforme" value={form.platform} items={window.NagareSeed.platforms}
                  onChange={(v) => setForm({ ...form, platform: v, baseUrl: defaultUrl(v) || form.baseUrl })} />
                <TextBox header="URL de base" value={form.baseUrl} onChange={(e) => setForm({ ...form, baseUrl: e.target.value })} mono />
                <PasswordBox header="Clé de stream" value={form.key} onChange={(e) => setForm({ ...form, key: e.target.value })}
                  placeholder="•••••••••••"
                  hint={isCreating ? "Requise. Chiffrée au repos ; jamais réaffichée." : "Laisser vide conserve la clé actuelle : une clé enregistrée ne peut jamais être réaffichée."} />
                <div className="editor__actions">
                  <Button variant="accent" onClick={save}>Enregistrer</Button>
                  <Button onClick={cancel}>Annuler</Button>
                </div>
              </div>
            </Card>
          )}
        </div>
      )}

      {deleteTarget && (
        <ContentDialog
          title={`Supprimer le channel « ${deleteTarget.name} » ?`}
          primaryText="Supprimer" primaryVariant="danger" onPrimary={confirmDelete}
          closeText="Annuler" onClose={() => setDeleteTarget(null)}>
          Cette action est définitive. La clé de stream chiffrée sera perdue ; vous devrez la ressaisir pour recréer ce channel.
        </ContentDialog>
      )}
    </div>
  );
}

Object.assign(window, { ChannelsPage });

const DSP = window.NagareDesignSystem_9475eb;

// Profiles CRUD with the Laws-of-UX editor: pick a PRESET instead of 15 fields
// (Hick's Law), fields CHUNKED into Vidéo / Débit / Audio / Entrée (Miller's Law),
// and an "Avancé" disclosure for keyint_min / bufsize / GOP (progressive disclosure).
// Invariants E1-E8 are the domain's; the editor surfaces the message, doesn't invent it.
function ProfilesPage() {
  const { useState } = React;
  const { Card, Button, TextBox, NumberBox, ComboBox, ToggleSwitch, InfoBar, EmptyState, ContentDialog, Icon } = DSP;
  const seed = window.NagareSeed;

  const summarize = (p) => `${p.codec} · ${p.preset} · ${p.rc} · ${p.bitrate} kbps · ${p.w}×${p.h} · ${p.fps} fps`;
  const presetValues = {
    twitch1080: { codec: "h264_nvenc", preset: "p5", rc: "CBR", bitrate: 6000, maxrate: 6000, bufsize: 6000, gop: 120, keyint: 120, w: 1920, h: 1080, fps: 60, audioBitrate: 160, sampleRate: 48000 },
    yt1440: { codec: "hevc_nvenc", preset: "p6", rc: "CBR", bitrate: 12000, maxrate: 12000, bufsize: 12000, gop: 120, keyint: 120, w: 2560, h: 1440, fps: 60, audioBitrate: 192, sampleRate: 48000 },
    twitch720: { codec: "h264_nvenc", preset: "p4", rc: "CBR", bitrate: 4500, maxrate: 4500, bufsize: 4500, gop: 120, keyint: 120, w: 1280, h: 720, fps: 60, audioBitrate: 160, sampleRate: 48000 },
    cpu720: { codec: "libx264", preset: "veryfast", rc: "CBR", bitrate: 3500, maxrate: 3500, bufsize: 3500, gop: 60, keyint: 60, w: 1280, h: 720, fps: 30, audioBitrate: 128, sampleRate: 44100 },
  };

  const [profiles, setProfiles] = useState(seed.profiles.map((p) => ({ ...p })));
  const [selectedId, setSelectedId] = useState(profiles[0]?.id ?? null);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(null);
  const [advanced, setAdvanced] = useState(false);
  const [activePreset, setActivePreset] = useState(null);
  const [error, setError] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);

  const selected = profiles.find((p) => p.id === selectedId) || null;
  const isCreating = editing === "new";
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));

  const startNew = () => { setError(null); setAdvanced(false); setActivePreset("twitch1080"); setEditing("new"); setForm({ name: "Nouveau profil", ...presetValues.twitch1080 }); };
  const startEdit = () => { if (!selected) return; setError(null); setAdvanced(false); setActivePreset(null); setEditing(selected.id); setForm({ ...selected }); };
  const applyPreset = (key) => { setActivePreset(key); setForm((f) => ({ ...f, ...presetValues[key] })); };

  const save = () => {
    const f = form;
    if (f.bitrate <= 0 || f.maxrate <= 0 || f.bufsize <= 0) return setError("E1: bitrate, maxrate and bufsize must be strictly positive.");
    if (f.rc === "CBR" && f.maxrate !== f.bitrate) return setError("E2: in CBR, maxrate must equal bitrate.");
    if (f.bufsize < f.bitrate) return setError("E4: bufsize must be greater than or equal to bitrate.");
    if (f.gop <= 0 || f.keyint <= 0 || f.keyint > f.gop) return setError("E5: GOP must be positive and 0 < keyint_min <= g.");
    if (isCreating) { const id = "p" + Date.now(); setProfiles((ps) => [...ps, { ...f, id }]); setSelectedId(id); }
    else setProfiles((ps) => ps.map((p) => (p.id === editing ? { ...f, id: editing } : p)));
    setEditing(null); setError(null);
  };

  const confirmDelete = () => {
    setProfiles((ps) => ps.filter((p) => p.id !== deleteTarget.id));
    if (selectedId === deleteTarget.id) setSelectedId(null);
    if (editing === deleteTarget.id) setEditing(null);
    setDeleteTarget(null);
  };

  const presetList = form ? window.NagareSeed.presetsFor(form.codec) : [];

  return (
    <div className="page">
      <div className="page-head">
        <h1 className="page-title">Profils d'encodage</h1>
        <p className="page-sub">Des réglages ffmpeg réutilisables. Partez d'un preset ; l'app absorbe la complexité.</p>
      </div>

      <div className="toolbar">
        <Button variant="accent" icon="plus" onClick={startNew}>Nouveau profil</Button>
        <Button icon="pencil" disabled={!selected} onClick={startEdit}>Modifier</Button>
        <Button variant="danger" icon="trash-2" disabled={!selected} onClick={() => setDeleteTarget(selected)}>Supprimer</Button>
      </div>

      {profiles.length === 0 ? (
        <Card>
          <EmptyState icon="sliders-horizontal" title="Aucun profil d'encodage"
            message="Un profil décrit comment encoder : codec, débit, résolution. Commencez par un preset prêt à l'emploi."
            action={<Button variant="accent" icon="plus" onClick={startNew}>Nouveau profil</Button>} />
        </Card>
      ) : (
        <div className="crud crud--wide">
          <div className="list">
            {profiles.map((p) => (
              <button key={p.id} className={"list-row" + (p.id === selectedId ? " list-row--sel" : "")} onClick={() => setSelectedId(p.id)}>
                <Icon className="list-icon" name="sliders-horizontal" size={18} />
                <div className="list-row__body">
                  <span className="list-name">{p.name}</span>
                  <span className="list-sub">{summarize(p)}</span>
                </div>
              </button>
            ))}
          </div>

          {editing !== null && form && (
            <Card title={isCreating ? "Nouveau profil" : "Modifier le profil"} icon="sliders-horizontal">
              <div className="editor">
                {error && <InfoBar severity="error" title="Réglage refusé" message={error} isClosable onClose={() => setError(null)} />}
                <TextBox header="Nom" value={form.name} onChange={(e) => set({ name: e.target.value })} />

                {isCreating && (
                  <>
                    <div className="editor__section"><Icon name="zap" size={16} />Preset de départ</div>
                    <div className="presets">
                      {window.NagareSeed.presets.map((ps) => (
                        <button key={ps.key} className={"preset" + (activePreset === ps.key ? " preset--sel" : "")} onClick={() => applyPreset(ps.key)}>
                          <Icon className="preset__icon" name={ps.icon} size={18} />
                          <span><span className="preset__name">{ps.label}</span><br /><span className="preset__sub">{ps.sub}</span></span>
                        </button>
                      ))}
                    </div>
                  </>
                )}

                <div className="editor__section"><Icon name="film" size={16} />Vidéo</div>
                <div className="editor__row">
                  <ComboBox header="Codec" value={form.codec} items={window.NagareSeed.codecs}
                    onChange={(v) => set({ codec: v, preset: window.NagareSeed.presetsFor(v)[0] })} />
                  <ComboBox header="Preset" value={form.preset} items={presetList} onChange={(v) => set({ preset: v })} />
                </div>
                <div className="editor__row">
                  <NumberBox header="Largeur" value={form.w} step={2} onChange={(v) => set({ w: v })} />
                  <NumberBox header="Hauteur" value={form.h} step={2} onChange={(v) => set({ h: v })} />
                </div>
                <NumberBox header="fps" value={form.fps} onChange={(v) => set({ fps: v })} />

                <div className="editor__section"><Icon name="activity" size={16} />Débit</div>
                <ComboBox header="Rate control" value={form.rc} items={["CBR", "VBR"]}
                  onChange={(v) => set({ rc: v, maxrate: v === "CBR" ? form.bitrate : form.maxrate })} />
                <div className="editor__row">
                  <NumberBox header="Bitrate (kbps)" value={form.bitrate} step={100}
                    onChange={(v) => set({ bitrate: v, maxrate: form.rc === "CBR" ? v : form.maxrate, bufsize: form.bufsize < v ? v : form.bufsize })} />
                  <NumberBox header="Maxrate (kbps)" value={form.maxrate} step={100} disabled={form.rc === "CBR"} onChange={(v) => set({ maxrate: v })} />
                </div>

                <div className="editor__section"><Icon name="radio" size={16} />Audio</div>
                <div className="editor__row">
                  <NumberBox header="Bitrate audio (kbps)" value={form.audioBitrate} step={16} onChange={(v) => set({ audioBitrate: v })} />
                  <ComboBox header="Sample rate (Hz)" value={form.sampleRate} items={[44100, 48000]} onChange={(v) => set({ sampleRate: v })} />
                </div>

                <div className="disclose">
                  <button className="disclose__toggle" aria-expanded={advanced} onClick={() => setAdvanced((a) => !a)}>
                    <Icon className="chev" name="chevron-right" size={16} />Avancé
                    <span style={{ marginLeft: "auto", fontWeight: 400, fontSize: 12, color: "var(--text-tertiary)" }}>bufsize · GOP · keyint_min</span>
                  </button>
                  {advanced && (
                    <div className="disclose__body">
                      <NumberBox header="Bufsize (kbps)" value={form.bufsize} step={100} onChange={(v) => set({ bufsize: v })} />
                      <div className="editor__row">
                        <NumberBox header="GOP (-g)" value={form.gop} onChange={(v) => set({ gop: v })} />
                        <NumberBox header="keyint_min" value={form.keyint} onChange={(v) => set({ keyint: v })} />
                      </div>
                    </div>
                  )}
                </div>

                <div className="editor__actions">
                  <Button variant="accent" onClick={save}>Enregistrer</Button>
                  <Button onClick={() => { setEditing(null); setError(null); }}>Annuler</Button>
                </div>
              </div>
            </Card>
          )}
        </div>
      )}

      {deleteTarget && (
        <ContentDialog
          title={`Supprimer le profil « ${deleteTarget.name} » ?`}
          primaryText="Supprimer" primaryVariant="danger" onPrimary={confirmDelete}
          closeText="Annuler" onClose={() => setDeleteTarget(null)}>
          Cette action est définitive. Les diffusions à venir ne pourront plus utiliser ce profil.
        </ContentDialog>
      )}
    </div>
  );
}

Object.assign(window, { ProfilesPage });

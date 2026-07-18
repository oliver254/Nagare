const DSD = window.NagareDesignSystem_9475eb;

// Dashboard — the broadcast page. Idle → Démarrer → live monitoring → Arrêter →
// session summary (Peak-End). The wall of stacked controls becomes four belted
// cards: Source · Diffusion · Santé · Journal (Common Region). A LaunchChecklist
// makes a disabled Démarrer explain itself (Zeigarnik). Red is used only for a
// real anomaly. No modal / no focus theft while live (Flow).
function DashboardPage() {
  const { useState, useEffect, useRef } = React;
  const { Card, Button, IconButton, ComboBox, InfoBar, ProgressRing, StatusBadge, StatTile, LaunchChecklist, Icon } = DSD;
  const seed = window.NagareSeed;

  const [file, setFile] = useState(seed.media);
  const [profileId, setProfileId] = useState("p1");
  const [channelId, setChannelId] = useState("c1");
  const [status, setStatus] = useState("idle"); // idle|starting|live|reconnecting|stopped|failed
  const [stats, setStats] = useState({ fps: 0, bitrate: 0, speed: 0, drops: 0, reconnects: 0 });
  const [logs, setLogs] = useState(window.NagareLogSeed.slice());
  const [elapsed, setElapsed] = useState(0);
  const [summary, setSummary] = useState(null);
  const [copied, setCopied] = useState(false);
  const frameRef = useRef(1);
  const consoleRef = useRef(null);

  const profile = seed.profiles.find((p) => p.id === profileId) || null;
  const channel = seed.channels.find((c) => c.id === channelId) || null;
  const isLive = status === "live" || status === "reconnecting";
  const locked = isLive || status === "starting";
  const ready = file && profile && channel && (status === "idle" || status === "stopped");
  const warn = isLive && stats.speed > 0 && stats.speed < 1.0;
  const command = window.NagareBuildCommand(profile, channel, file && file.name);

  const checklist = [
    { label: "Environnement ffmpeg", done: true },
    { label: "Fichier vidéo", done: !!file },
    { label: "Profil d'encodage", done: !!profile },
    { label: "Channel", done: !!channel },
  ];
  const missing = checklist.find((i) => !i.done);

  // Live engine: one coalesced tick per second (matches the 1/s stats throttle).
  useEffect(() => {
    if (!isLive) return;
    const id = setInterval(() => {
      setElapsed((e) => e + 1);
      setStats((s) => {
        const base = profile ? profile.bitrate : 6000;
        const speed = status === "reconnecting" ? 0 : (warnRef.current ? 0.80 + Math.random() * 0.12 : 0.99 + Math.random() * 0.02);
        const drops = s.drops + (warnRef.current ? Math.floor(Math.random() * 6) : 0);
        return { fps: profile ? profile.fps : 60, bitrate: Math.round(base * (0.985 + Math.random() * 0.02)), speed: Number(speed.toFixed(2)), drops, reconnects: s.reconnects };
      });
      frameRef.current += profile ? profile.fps : 60;
      const f = frameRef.current;
      const line = status === "reconnecting"
        ? "[flv] Connection to tcp://live.twitch.tv failed — retrying"
        : `frame=${String(f).padStart(6)} fps=${profile ? profile.fps : 60} q=22.0 bitrate=${(profile ? profile.bitrate : 6000)}.0kbits/s speed=1.00x`;
      setLogs((ls) => [...ls.slice(-180), line]);
    }, 1000);
    return () => clearInterval(id);
  }, [isLive, status, profile]);

  const warnRef = useRef(false);
  useEffect(() => { warnRef.current = false; }, [status]);

  useEffect(() => { if (consoleRef.current) consoleRef.current.scrollTop = consoleRef.current.scrollHeight; }, [logs]);

  const start = () => {
    if (!ready) return;
    setSummary(null); setElapsed(0); frameRef.current = 1;
    setStats({ fps: 0, bitrate: 0, speed: 0, drops: 0, reconnects: 0 });
    setLogs(window.NagareLogSeed.slice());
    setStatus("starting");
    setTimeout(() => { setStatus("live"); setStats((s) => ({ ...s, fps: profile ? profile.fps : 60, bitrate: profile ? profile.bitrate : 6000, speed: 1.0 })); }, 1300);
  };
  const stop = (failed) => {
    setSummary({ duration: elapsed, drops: stats.drops, reconnects: stats.reconnects, failed: !!failed,
      reason: failed ? "Échec : connexion RTMP refusée par le serveur." : "Arrêt par l'utilisateur." });
    setStatus("stopped");
  };
  const reset = () => { setSummary(null); setStatus("idle"); setElapsed(0); setStats({ fps: 0, bitrate: 0, speed: 0, drops: 0, reconnects: 0 }); };

  // Demo aids (kit only)
  const demoDrop = () => { warnRef.current = true; };
  const demoRecover = () => { warnRef.current = false; setStatus("live"); };
  const demoReconnect = () => { setStatus("reconnecting"); setStats((s) => ({ ...s, reconnects: s.reconnects + 1, speed: 0 })); setTimeout(() => setStatus("live"), 2600); };

  const fmt = (s) => [Math.floor(s / 3600), Math.floor(s / 60) % 60, s % 60].map((n) => String(n).padStart(2, "0")).join(":");
  const fr = (n) => Math.round(n).toLocaleString("fr-FR");
  const spd = (n) => n.toFixed(2).replace(".", ",");

  const badge = status === "starting" ? <StatusBadge tone="attention">Démarrage…</StatusBadge>
    : status === "reconnecting" ? <StatusBadge tone="attention">Reconnexion…</StatusBadge>
    : status === "live" ? (warn ? <StatusBadge tone="critical">En direct · vitesse basse</StatusBadge> : <StatusBadge tone="live">En direct</StatusBadge>)
    : status === "stopped" ? (summary && summary.failed ? <StatusBadge tone="critical">Échec</StatusBadge> : <StatusBadge tone="neutral">Arrêtée</StatusBadge>)
    : <StatusBadge tone="neutral">Aucune session</StatusBadge>;

  return (
    <div className="page">
      <div className="page-head">
        <h1 className="page-title">Tableau de bord</h1>
        <p className="page-sub">Choisir un fichier, un profil, un channel — puis diffuser et surveiller.</p>
      </div>

      <div className="dash">
        {/* SOURCE */}
        <div className="a-source">
          <Card title="Source" icon="folder-open">
            {file ? (
              <div className={"dropzone dropzone--filled"}>
                <Icon name="film" size={22} />
                <div className="dropzone__body">
                  <span className="dropzone__name">{file.name}</span>
                  <div className="media-chips">
                    <span className="chip">{file.duration}</span>
                    <span className="chip">{file.w}×{file.h}</span>
                    <span className="chip">{file.fps} fps</span>
                    <span className="chip">{file.vcodec}</span>
                    <span className="chip">{file.acodec}</span>
                  </div>
                </div>
                {!locked && <IconButton icon="x" label="Retirer le fichier" onClick={() => setFile(null)} style={{ marginLeft: "auto" }} />}
              </div>
            ) : (
              <button className="dropzone" onClick={() => setFile(seed.media)}>
                <Icon name="upload" size={22} />
                <div className="dropzone__body">
                  <span className="dropzone__name">Choisir un fichier…</span>
                  <span>ou déposez un .mp4 ici · collez un chemin</span>
                </div>
              </button>
            )}
          </Card>
        </div>

        {/* DIFFUSION */}
        <div className="a-diffusion">
          <Card title="Diffusion" icon="cast">
            <div className="editor__row" style={{ marginBottom: 12 }}>
              <ComboBox header="Profil d'encodage" value={profileId || undefined} placeholder="Choisir un profil" disabled={locked}
                items={seed.profiles.map((p) => ({ value: p.id, label: p.name }))} onChange={setProfileId} />
              <ComboBox header="Channel" value={channelId || undefined} placeholder="Choisir un channel" disabled={locked}
                items={seed.channels.map((c) => ({ value: c.id, label: c.name }))} onChange={setChannelId} />
            </div>
            <div style={{ fontSize: 12, color: "var(--text-secondary)", marginBottom: 4 }}>Commande ffmpeg — clé masquée</div>
            <div className="cmd">
              <div className={"cmd__box" + (command ? "" : " cmd__box--empty")}>{command || "Sélectionnez un profil et un channel pour voir la commande."}</div>
              {command && <IconButton className="cmd__copy" icon={copied ? "check" : "copy"} label="Copier la commande (clé masquée)" onClick={() => { setCopied(true); setTimeout(() => setCopied(false), 1200); }} />}
            </div>
          </Card>
        </div>

        {/* SANTÉ */}
        <div className="a-health">
          <Card title="Santé" icon="activity" badge={badge}>
            {status === "starting" && (
              <div style={{ padding: "24px 0", display: "flex", justifyContent: "center" }}><ProgressRing size={40} label="Démarrage…" /></div>
            )}

            {(status === "idle" || (status === "stopped" && !summary)) && (
              <div className="health-idle">
                <LaunchChecklist items={checklist} />
                {missing ? (
                  <div className="why"><Icon name="triangle-alert" size={16} /><span>Il manque : <b>{missing.label.toLowerCase()}</b>. Renseignez-le pour activer la diffusion.</span></div>
                ) : (
                  <div className="why"><Icon name="check" size={16} style={{ color: "var(--success)" }} /><span>Tout est prêt. Vous pouvez diffuser.</span></div>
                )}
                <div className="primary-action">
                  <Button variant="accent" icon="play" disabled={!ready} onClick={start}>Démarrer</Button>
                </div>
              </div>
            )}

            {isLive && (
              <div className="stack">
                <div style={{ display: "flex", alignItems: "baseline", gap: 8 }}>
                  <span style={{ fontSize: 28, fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>{fmt(elapsed)}</span>
                  <span style={{ fontSize: 12, color: "var(--text-secondary)" }}>en direct · {channel && channel.name}</span>
                </div>
                <div className="stats">
                  <StatTile label="Images" value={String(stats.fps)} unit="fps" icon="film" />
                  <StatTile label="Débit" value={fr(stats.bitrate)} unit="kbits/s" icon="activity" />
                  <StatTile label="Vitesse" value={status === "reconnecting" ? "—" : spd(stats.speed)} unit="x" icon="gauge" warning={warn} />
                  <StatTile label="Drops" value={fr(stats.drops)} unit="drops" warning={stats.drops > 0} />
                </div>
                <StatTile label="Reconnexions" value={fr(stats.reconnects)} unit="" icon="refresh-cw" warning={stats.reconnects > 0} />
                <div className="primary-action">
                  <Button icon="circle-stop" onClick={() => stop(false)}>Arrêter</Button>
                </div>
              </div>
            )}

            {status === "stopped" && summary && (
              <div className="stack">
                <InfoBar severity={summary.failed ? "error" : "success"}
                  title={summary.failed ? "Session en échec" : "Session terminée"}
                  message={summary.reason} />
                <div className="summary-grid">
                  <StatTile label="Durée" value={fmt(summary.duration)} />
                  <StatTile label="Drops" value={fr(summary.drops)} warning={summary.drops > 0} />
                  <StatTile label="Reconnexions" value={fr(summary.reconnects)} warning={summary.reconnects > 0} />
                </div>
                <div className="primary-action">
                  <Button variant="accent" icon="play" disabled={!ready} onClick={start}>Rediffuser</Button>
                  <Button icon="rotate-cw" onClick={reset}>Nouvelle session</Button>
                </div>
              </div>
            )}
          </Card>
        </div>

        {/* JOURNAL */}
        <div className="a-journal">
          <Card title="Journal ffmpeg" icon="terminal" badge={<span style={{ fontSize: 12, color: "var(--text-tertiary)", fontFamily: "var(--font-mono)" }}>500 dernières lignes</span>}>
            <div className="console" ref={consoleRef}>
              {logs.map((l, i) => {
                const err = /failed|error|refused|introuvable/i.test(l);
                const wl = /retry|reconnect|drop|warn/i.test(l);
                return <div key={i} className={"console__line" + (err ? " console__line--err" : wl ? " console__line--warn" : "")}>{l}</div>;
              })}
            </div>
          </Card>
        </div>
      </div>

      {/* Demo aids — kit only, not part of the product UI */}
      <div className="demo-strip">
        <b>Démo :</b>
        {status === "idle" && <Button size="small" onClick={() => { setFile(seed.media); setProfileId("p1"); setChannelId("c1"); }}>Pré-remplir</Button>}
        {status === "idle" && <Button size="small" onClick={() => { setFile(null); setProfileId(null); setChannelId(null); }}>Config vide</Button>}
        {isLive && <Button size="small" onClick={demoDrop}>Chute du flux</Button>}
        {isLive && <Button size="small" onClick={demoRecover}>Rétablir</Button>}
        {isLive && <Button size="small" onClick={demoReconnect}>Reconnexion</Button>}
        {isLive && <Button size="small" variant="danger" onClick={() => stop(true)}>Simuler un échec</Button>}
        <span>parcourez les états sans vraie diffusion.</span>
      </div>
    </div>
  );
}

Object.assign(window, { DashboardPage });

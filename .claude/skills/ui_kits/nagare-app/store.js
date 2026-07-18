// Seed data for the Nagare UI kit (fake, in-memory). Mirrors the shapes the real
// ViewModels expose: StreamProfileDto, ChannelDto, and the encoding summary the
// EncodingSummaryConverter produces. English identifiers, French display strings.
window.NagareSeed = {
  profiles: [
    { id: "p1", name: "Twitch 1080p60", codec: "h264_nvenc", preset: "p5", rc: "CBR",
      bitrate: 6000, maxrate: 6000, bufsize: 6000, gop: 120, keyint: 120, w: 1920, h: 1080, fps: 60,
      audioBitrate: 160, sampleRate: 48000 },
    { id: "p2", name: "YouTube 1440p60", codec: "hevc_nvenc", preset: "p6", rc: "CBR",
      bitrate: 12000, maxrate: 12000, bufsize: 12000, gop: 120, keyint: 120, w: 2560, h: 1440, fps: 60,
      audioBitrate: 192, sampleRate: 48000 },
    { id: "p3", name: "libx264 720p · CPU", codec: "libx264", preset: "veryfast", rc: "CBR",
      bitrate: 3500, maxrate: 3500, bufsize: 3500, gop: 60, keyint: 60, w: 1280, h: 720, fps: 30,
      audioBitrate: 128, sampleRate: 44100 },
  ],
  channels: [
    { id: "c1", name: "Twitch principal", platform: "Twitch", baseUrl: "rtmp://live.twitch.tv/app", keyConfigured: true },
    { id: "c2", name: "YouTube — live", platform: "YouTube", baseUrl: "rtmp://a.rtmp.youtube.com/live2", keyConfigured: true },
  ],
  // Ready-made presets for the profile editor (Hick's Law: choose a preset, not 15 fields).
  presets: [
    { key: "twitch1080", label: "Twitch 1080p60", sub: "h264_nvenc · 6000 kbps", icon: "radio-tower" },
    { key: "yt1440", label: "YouTube 1440p60", sub: "hevc_nvenc · 12000 kbps", icon: "radio-tower" },
    { key: "twitch720", label: "Twitch 720p60 · faible débit", sub: "h264_nvenc · 4500 kbps", icon: "signal" },
    { key: "cpu720", label: "libx264 720p · sans GPU", sub: "libx264 · 3500 kbps", icon: "hard-drive" },
  ],
  media: { name: "boucle-attente.mp4", duration: "00:04:12", w: 1920, h: 1080, fps: 60, vcodec: "h264", acodec: "aac", size: "247 Mo" },
  codecs: [
    { value: "h264_nvenc", label: "h264_nvenc · GPU" },
    { value: "hevc_nvenc", label: "hevc_nvenc · GPU" },
    { value: "libx264", label: "libx264 · CPU" },
  ],
  platforms: [
    { value: "Twitch", label: "Twitch" },
    { value: "YouTube", label: "YouTube" },
    { value: "CustomRtmp", label: "RTMP custom" },
  ],
  presetsFor: function (codec) {
    return codec === "libx264"
      ? ["ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"]
      : ["p1", "p2", "p3", "p4", "p5", "p6", "p7"];
  },
};

// A short ffmpeg-style startup log (the console rehydrates ~these on load).
window.NagareLogSeed = [
  "ffmpeg version 6.1.1 Copyright (c) 2000-2024 the FFmpeg developers",
  "  built with gcc 13.2.0",
  "Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'boucle-attente.mp4':",
  "  Duration: 00:04:12.00, start: 0.000000, bitrate: 8123 kb/s",
  "  Stream #0:0(und): Video: h264 (High), yuv420p, 1920x1080, 60 fps, 60 tbr",
  "  Stream #0:1(und): Audio: aac (LC), 48000 Hz, stereo, fltp, 160 kb/s",
  "Stream mapping:",
  "  Stream #0:0 -> #0:0 (h264 (native) -> h264_nvenc)",
  "  Stream #0:1 -> #0:1 (aac (native) -> aac)",
  "[h264_nvenc @ 0x5581] Using NVENC preset p5 (rc=cbr)",
  "Output #0, flv, to 'rtmp://live.twitch.tv/app/••••':",
  "Press [q] to stop, [?] for help",
];

// Build the masked ffmpeg command from a profile + channel (SPEC §4: key masked).
window.NagareBuildCommand = function (p, c, file) {
  if (!p || !c) return "";
  const rc = p.rc.toLowerCase();
  const parts = [
    "ffmpeg", "-re", "-stream_loop", "-1", "-i", `"${file || "boucle-attente.mp4"}"`,
    "-c:v", p.codec, "-preset", p.preset, "-rc", rc,
    "-b:v", `${p.bitrate}k`, "-maxrate", `${p.maxrate}k`, "-bufsize", `${p.bufsize}k`,
    "-g", String(p.gop), "-keyint_min", String(p.keyint),
    "-vf", `scale=${p.w}:${p.h}`, "-r", String(p.fps),
    "-c:a", "aac", "-b:a", `${p.audioBitrate}k`, "-ar", String(p.sampleRate),
    "-f", "flv", `${c.baseUrl}/••••`,
  ];
  return parts.join(" ");
};

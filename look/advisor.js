/**
 * LOOK Advisor — metric-driven personalization + multi-factor scoring helpers.
 * Tips/schedules are synthesized from this face’s measured deficits (skin, bone,
 * eyes, hair, expression) so two different reads never share the same plan copy.
 * Optional Gemini rewrite when an API key is set in Settings.
 */

const IDEAL = {
  heightWidth: { t: 1.32, tol: 0.22 },
  thirdsTop: { t: 0.33, tol: 0.08 },
  thirdsMid: { t: 0.22, tol: 0.06 },
  thirdsLow: { t: 0.45, tol: 0.1 },
  eyeSpacing: { t: 2.05, tol: 0.28 },
  interocularFW: { t: 0.46, tol: 0.07 },
  mouthToEyes: { t: 0.95, tol: 0.22 },
  noseToFace: { t: 0.22, tol: 0.06 },
  noseToMouth: { t: 0.62, tol: 0.12 },
  jawDominance: { t: 0.78, tol: 0.14 },
  jawToCheek: { t: 0.86, tol: 0.1 },
  templeToCheek: { t: 0.98, tol: 0.12 },
  browRatio: { t: 0.9, tol: 0.12 },
  browEye: { t: 0.16, tol: 0.055 },
  eyeAperture: { t: 0.3, tol: 0.08 },
  cheekBalance: { t: 2.05, tol: 0.28 },
  canthalLift: { t: 0.012, tol: 0.018 },
  philtrum: { t: 0.2, tol: 0.06 },
  midLower: { t: 0.85, tol: 0.16 },
  chinProj: { t: -0.18, tol: 0.14 },
  symmetry: { t: 0.94, tol: 0.16 }
};

function clamp(v, lo, hi) {
  return Math.max(lo, Math.min(hi, v));
}

function near(value, target, tolerance) {
  return clamp(1 - Math.abs(value - target) / tolerance, 0, 1);
}

/** Peaked appeal around a center. sigma is the drop-off; two lobes cover different face shapes. */
function appeal(value, center, sigma) {
  if (typeof value !== "number" || Number.isNaN(value)) return 0.55;
  const z = (value - center) / (sigma || 0.1);
  return clamp(0.94 * Math.exp(-0.5 * z * z), 0.16, 0.94);
}

function bestAppeal(value, lobes) {
  let best = 0.16;
  for (let i = 0; i < lobes.length; i++) best = Math.max(best, appeal(value, lobes[i][0], lobes[i][1]));
  return best;
}

function pct(n) {
  return Math.round(clamp(n, 0, 1) * 100);
}

function fmt(n, d) {
  if (typeof n !== "number" || Number.isNaN(n)) return "—";
  const f = Math.pow(10, d == null ? 2 : d);
  return (Math.round(Number(n) * f) / f).toFixed(d == null ? 2 : d);
}

/** Sample cheek / forehead ROIs from a landmark-aligned canvas for skin cues. */
export function analyzeSkin(sourceCanvas, landmarks, L) {
  if (!sourceCanvas || !landmarks || !L) {
    return { clarity: 0.55, evenness: 0.55, brightness: 0.5, redness: 0.5 };
  }
  const w = sourceCanvas.width;
  const h = sourceCanvas.height;
  if (!w || !h) return { clarity: 0.55, evenness: 0.55, brightness: 0.5, redness: 0.5 };

  const ctx = sourceCanvas.getContext("2d", { willReadFrequently: true });
  if (!ctx) return { clarity: 0.55, evenness: 0.55, brightness: 0.5, redness: 0.5 };

  const pts = [
    landmarks[L.leftCheek],
    landmarks[L.rightCheek],
    landmarks[L.forehead],
    landmarks[L.noseBridge]
  ].filter(Boolean);

  const samples = [];
  for (let i = 0; i < pts.length; i++) {
    const px = clamp(Math.floor(pts[i].x * w), 2, w - 3);
    const py = clamp(Math.floor(pts[i].y * h), 2, h - 3);
    const size = Math.max(6, Math.floor(Math.min(w, h) * 0.03));
    try {
      const data = ctx.getImageData(px - size, py - size, size * 2, size * 2).data;
      for (let j = 0; j < data.length; j += 16) {
        const r = data[j];
        const g = data[j + 1];
        const b = data[j + 2];
        const y = 0.299 * r + 0.587 * g + 0.114 * b;
        // Drop background black and clipped highlights so studio light isn't "bad skin".
        if (y < 48 || y > 225) continue;
        samples.push({ r, g, b, y });
      }
    } catch (_) {
      /* tainted canvas — skip */
    }
  }
  if (samples.length < 12) {
    return { clarity: 0.62, evenness: 0.62, brightness: 0.6, redness: 0.6 };
  }

  let sumY = 0;
  let sumR = 0;
  let sumG = 0;
  let sumB = 0;
  for (let i = 0; i < samples.length; i++) {
    sumY += samples[i].y;
    sumR += samples[i].r;
    sumG += samples[i].g;
    sumB += samples[i].b;
  }
  const n = samples.length;
  const meanY = sumY / n;
  let varY = 0;
  for (let i = 0; i < n; i++) {
    const d = samples[i].y - meanY;
    varY += d * d;
  }
  varY /= n;
  const std = Math.sqrt(varY);
  const evenness = clamp(1 - std / 95, 0.35, 0.98);
  const brightness = clamp(0.7 + (1 - Math.abs(meanY - 140) / 180) * 0.28, 0.45, 0.98);
  const meanR = sumR / n;
  const meanG = sumG / n;
  const meanB = sumB / n;
  const redRatio = meanR / (meanG + meanB + 1e-6);
  const redness = clamp(1 - Math.abs(redRatio - 0.7) / 0.7, 0.4, 0.98);
  const clarity = clamp(evenness * 0.5 + brightness * 0.28 + redness * 0.22, 0.2, 0.98);
  return { clarity, evenness, brightness, redness, std, meanY };
}

/** Hair pixels just above the forehead — styled hair reads higher than a bare scalp crop. */
export function analyzeHairPixels(sourceCanvas, landmarks, L) {
  if (!sourceCanvas || !landmarks || !L || !landmarks[L.forehead]) return 0.6;
  const w = sourceCanvas.width;
  const h = sourceCanvas.height;
  const ctx = sourceCanvas.getContext("2d", { willReadFrequently: true });
  if (!ctx || !w || !h) return 0.6;
  const f = landmarks[L.forehead];
  const px = clamp(Math.floor(f.x * w), 4, w - 5);
  const py = clamp(Math.floor(f.y * h) - Math.floor(h * 0.06), 4, h - 5);
  const size = Math.max(8, Math.floor(Math.min(w, h) * 0.05));
  let n = 0;
  let colored = 0;
  try {
    const data = ctx.getImageData(px - size, Math.max(0, py - size), size * 2, size * 2).data;
    for (let j = 0; j < data.length; j += 16) {
      const y = 0.299 * data[j] + 0.587 * data[j + 1] + 0.114 * data[j + 2];
      n++;
      if (y > 18 && y < 210) colored++;
    }
  } catch (_) {
    return 0.6;
  }
  if (!n) return 0.6;
  return clamp(0.45 + (colored / n) * 0.5, 0.35, 0.95);
}

/** Hair / framing proxies from landmarks (forehead height, temple taper). */
export function analyzeHair(metrics) {
  if (!metrics) return { framing: 0.55, foreheadBalance: 0.55, templeTaper: 0.55 };
  const foreheadBalance = appeal(metrics.thirdsTop, 0.34, 0.06);
  const templeTaper = appeal(metrics.templeToCheek, 1.0, 0.08);
  const brow = appeal(metrics.browRatio, 0.86, 0.08);
  const framing = clamp(foreheadBalance * 0.4 + templeTaper * 0.35 + brow * 0.25, 0.12, 0.98);
  return { framing, foreheadBalance, templeTaper, brow };
}

export function analyzeEyes(metrics) {
  if (!metrics) return { quality: 0.55, aperture: 0.55, canthal: 0.55, spacing: 0.55 };
  const aperture = appeal(metrics.eyeAperture, 0.33, 0.07);
  const canthal = appeal(metrics.canthalLift, 0.012, 0.01);
  const spacing = appeal(metrics.eyeSpacing, 2.12, 0.28);
  const browEye = appeal(metrics.browEye, 0.16, 0.05);
  const quality = clamp(aperture * 0.32 + canthal * 0.22 + spacing * 0.28 + browEye * 0.18, 0.12, 0.98);
  return { quality, aperture, canthal, spacing, browEye };
}

export function analyzeNose(metrics) {
  if (!metrics) return { quality: 0.55, width: 0.55, length: 0.55 };
  const width = appeal(metrics.noseToMouth, 0.63, 0.09);
  const length = appeal(metrics.noseToFace, 0.22, 0.045);
  const quality = clamp(width * 0.55 + length * 0.45, 0.12, 0.98);
  return { quality, width, length };
}

export function analyzeMouth(metrics) {
  if (!metrics) return { quality: 0.55, width: 0.55, philtrum: 0.55, lips: 0.55 };
  const width = appeal(metrics.mouthToEyes, 0.9, 0.14);
  const phil = appeal(metrics.philtrum, 0.21, 0.05);
  const lips = typeof metrics.lipFullness === "number" ? appeal(metrics.lipFullness, 0.22, 0.08) : 0.72;
  const quality = clamp(width * 0.45 + phil * 0.3 + lips * 0.25, 0.12, 0.98);
  return { quality, width, philtrum: phil, lips };
}

export function analyzeBone(metrics) {
  if (!metrics) return { structure: 0.55, jaw: 0.55, chin: 0.55, thirds: 0.55 };
  const jaw = bestAppeal(metrics.jawToCheek, [[0.84, 0.07], [0.76, 0.06]]);
  const chin = appeal(metrics.chinProj, -0.2, 0.07);
  const hw = bestAppeal(metrics.heightWidth, [[1.24, 0.09], [1.1, 0.07]]);
  const thirds = clamp(
    (appeal(metrics.thirdsTop, 0.34, 0.05) +
      appeal(metrics.thirdsMid, 0.22, 0.04) +
      appeal(metrics.thirdsLow, 0.5, 0.06)) /
      3,
    0,
    1
  );
  const midLower = appeal(metrics.midLower, 0.85, 0.16);
  const structure = clamp(jaw * 0.32 + chin * 0.3 + hw * 0.16 + thirds * 0.14 + midLower * 0.08, 0.1, 0.98);
  return { structure, jaw, chin, hw, thirds, midLower };
}

export function idealMatch(metrics) {
  if (!metrics) return 0;
  let sum = 0;
  let n = 0;
  const keys = Object.keys(IDEAL);
  for (let i = 0; i < keys.length; i++) {
    const k = keys[i];
    if (typeof metrics[k] !== "number") continue;
    sum += near(metrics[k], IDEAL[k].t, IDEAL[k].tol);
    n++;
  }
  return n ? sum / n : 0;
}

/**
 * Steep multi-factor PSL. Average faces land mid/low-normie; only strong
 * structure + skin + eyes climb into Chad lite+.
 */
/**
 * On-device look judge. Six features — bone, skin, hair, eyes, nose, mouth —
 * each 0–1, then a PSL from how they sit together.
 */
export function judgeLook(metrics, appearance) {
  const m = metrics || {};
  const bone = analyzeBone(m);
  const eyes = analyzeEyes(m);
  const nose = analyzeNose(m);
  const mouth = analyzeMouth(m);
  const hairGeo = analyzeHair(m);
  const skin = (appearance && appearance.skin) || { clarity: 0.62 };
  const hairPx = appearance && typeof appearance.hairPx === "number" ? appearance.hairPx : hairGeo.framing;
  const hair = clamp(hairGeo.framing * 0.6 + hairPx * 0.4, 0.12, 0.98);
  // Keep some pose tolerance, but a clearly uneven read still costs points.
  const symmetry = clamp(0.22 + (m.symmetry ?? 0.5) * 0.78, 0, 1);
  const parts = {
    bone: bone.structure,
    skin: clamp(skin.clarity ?? 0.62, 0, 1),
    hair,
    eyes: eyes.quality,
    nose: nose.quality,
    mouth: mouth.quality,
    symmetry
  };
  const raw =
    parts.bone * 0.28 +
    parts.eyes * 0.18 +
    parts.nose * 0.12 +
    parts.mouth * 0.1 +
    parts.skin * 0.12 +
    parts.hair * 0.08 +
    parts.symmetry * 0.12;
  return { parts, raw, bone, eyes, nose, mouth, hair: hairGeo, skin };
}

export function scoreFromFactors(factors) {
  const f = factors || {};
  const raw =
    typeof f.raw === "number"
      ? f.raw
      : clamp(f.bone ?? 0.5, 0, 1) * 0.28 +
        clamp(f.eyes ?? 0.5, 0, 1) * 0.18 +
        clamp(f.nose ?? 0.55, 0, 1) * 0.12 +
        clamp(f.mouth ?? 0.55, 0, 1) * 0.1 +
        clamp(f.skin ?? 0.55, 0, 1) * 0.12 +
        clamp(f.hair ?? 0.55, 0, 1) * 0.08 +
        clamp(f.symmetry ?? 0.6, 0, 1) * 0.12;

  // Community PSL anchors: ~3 low/average, ~4 mid, ~5 high tier, ~5.5–6.5 chad lite, 6.5+ rare.
  let score;
  if (raw < 0.42) score = 1.2 + (raw / 0.42) * 1.4;
  else if (raw < 0.55) score = 2.6 + ((raw - 0.42) / 0.13) * 0.9;
  else if (raw < 0.65) score = 3.5 + ((raw - 0.55) / 0.1) * 0.75;
  else if (raw < 0.74) score = 4.25 + ((raw - 0.65) / 0.09) * 0.7;
  else if (raw < 0.82) score = 4.95 + ((raw - 0.74) / 0.08) * 0.7;
  else if (raw < 0.9) score = 5.65 + ((raw - 0.82) / 0.08) * 0.7;
  else score = 6.35 + ((raw - 0.9) / 0.1) * 0.8;

  return clamp(score, 1, 7.99);
}

/**
 * Community example faces (Looksmaxxing example table / PSL wiki).
 * A new face is scored by similarity to these measured reads, not a single ideal jaw.
 * Targets: Cera 3 LTN, Shia 4 MTN, Chalamet 5 HTN, Evans & Ronaldo 5.5 chadlite, Cavill & Hemsworth 6.5.
 */
const COMMUNITY_FIELDS = [
  ["chinProj", 0.07],
  ["symmetry", 0.16],
  ["jawToCheek", 0.045],
  ["canthalLift", 0.005],
  ["eyeAperture", 0.03],
  ["heightWidth", 0.05],
  ["noseToMouth", 0.06],
  ["mouthToEyes", 0.08],
  ["bone", 0.1],
  ["eyes", 0.1],
  ["nose", 0.1],
  ["mouth", 0.1]
];

const COMMUNITY_EXEMPLARS = [
  { name: "Vincent Schiavelli", psl: 2, v: { chinProj: -0.176, symmetry: 0.249, jawToCheek: 0.793, canthalLift: 0.008, eyeAperture: 0.342, heightWidth: 1.222, noseToMouth: 0.613, mouthToEyes: 0.908, bone: 0.874, eyes: 0.891, nose: 0.929, mouth: 0.801 } },
  { name: "Steve Buscemi", psl: 2.5, v: { chinProj: -0.229, symmetry: 0.336, jawToCheek: 0.776, canthalLift: 0.009, eyeAperture: 0.303, heightWidth: 1.19, noseToMouth: 0.647, mouthToEyes: 0.915, bone: 0.881, eyes: 0.898, nose: 0.929, mouth: 0.832 } },
  { name: "Danny DeVito", psl: 3, v: { chinProj: -0.156, symmetry: 0.828, jawToCheek: 0.839, canthalLift: 0.01, eyeAperture: 0.246, heightWidth: 1.066, noseToMouth: 0.641, mouthToEyes: 1.031, bone: 0.861, eyes: 0.746, nose: 0.899, mouth: 0.593 } },
  { name: "Michael Cera", psl: 3, v: { chinProj: -0.083, symmetry: 0.306, jawToCheek: 0.805, canthalLift: 0.006, eyeAperture: 0.288, heightWidth: 1.134, noseToMouth: 0.513, mouthToEyes: 1.093, bone: 0.665, eyes: 0.832, nose: 0.645, mouth: 0.653 } },
  { name: "Shia LaBeouf", psl: 4, v: { chinProj: -0.09, symmetry: 0, jawToCheek: 0.792, canthalLift: 0.003, eyeAperture: 0.25, heightWidth: 1.136, noseToMouth: 0.553, mouthToEyes: 0.954, bone: 0.643, eyes: 0.578, nose: 0.653, mouth: 0.697 } },
  { name: "Timothée Chalamet", psl: 5, v: { chinProj: -0.255, symmetry: 0.806, jawToCheek: 0.764, canthalLift: 0.012, eyeAperture: 0.337, heightWidth: 1.249, noseToMouth: 0.681, mouthToEyes: 0.85, bone: 0.853, eyes: 0.932, nose: 0.862, mouth: 0.719 } },
  { name: "Chris Evans", psl: 5.5, v: { chinProj: -0.147, symmetry: 0.701, jawToCheek: 0.843, canthalLift: 0.004, eyeAperture: 0.324, heightWidth: 1.193, noseToMouth: 0.58, mouthToEyes: 0.878, bone: 0.843, eyes: 0.857, nose: 0.862, mouth: 0.789 } },
  { name: "Cristiano Ronaldo", psl: 5.5, v: { chinProj: -0.079, symmetry: 0.037, jawToCheek: 0.823, canthalLift: 0.003, eyeAperture: 0.321, heightWidth: 1.105, noseToMouth: 0.566, mouthToEyes: 0.916, bone: 0.7, eyes: 0.8, nose: 0.798, mouth: 0.903 } },
  { name: "Henry Cavill", psl: 6.5, v: { chinProj: -0.175, symmetry: 0.903, jawToCheek: 0.825, canthalLift: 0.006, eyeAperture: 0.292, heightWidth: 1.12, noseToMouth: 0.604, mouthToEyes: 1.004, bone: 0.906, eyes: 0.848, nose: 0.915, mouth: 0.634 } },
  { name: "Chris Hemsworth", psl: 6.5, v: { chinProj: -0.157, symmetry: 0.702, jawToCheek: 0.861, canthalLift: 0.007, eyeAperture: 0.304, heightWidth: 1.128, noseToMouth: 0.693, mouthToEyes: 0.851, bone: 0.857, eyes: 0.874, nose: 0.822, mouth: 0.801 } }
];

function communityVector(metrics, parts) {
  const m = metrics || {};
  const p = parts || {};
  const vec = {
    chinProj: m.chinProj,
    symmetry: m.symmetry,
    jawToCheek: m.jawToCheek,
    canthalLift: m.canthalLift,
    eyeAperture: m.eyeAperture,
    heightWidth: m.heightWidth,
    noseToMouth: m.noseToMouth,
    mouthToEyes: m.mouthToEyes,
    bone: p.bone,
    eyes: p.eyes,
    nose: p.nose,
    mouth: p.mouth
  };
  // A turned head breaks symmetry. Drop that cue past a small yaw so profile shots are not punished.
  if (typeof m.yaw === "number" && Math.abs(m.yaw) > 0.095) delete vec.symmetry;
  return vec;
}

function communityDistance(a, b) {
  let s = 0;
  for (let i = 0; i < COMMUNITY_FIELDS.length; i++) {
    const key = COMMUNITY_FIELDS[i][0];
    const scale = COMMUNITY_FIELDS[i][1];
    const av = a[key];
    const bv = b[key];
    if (typeof av !== "number" || typeof bv !== "number") continue;
    const z = (av - bv) / scale;
    s += z * z;
  }
  return Math.sqrt(s);
}

/** 4 is average. Chadlite (about 5.5) is only for faces close to those community examples. */
export function scoreFromCommunity(metrics, parts) {
  const vec = communityVector(metrics, parts);
  const geo = scoreFromFactors(parts || {});
  const sigma = 0.55;
  const priorPsl = 4;
  let wsum = 0;
  let psum = 0;
  let best = Infinity;
  let bestName = "";
  for (let i = 0; i < COMMUNITY_EXEMPLARS.length; i++) {
    const ex = COMMUNITY_EXEMPLARS[i];
    const d = communityDistance(vec, ex.v);
    if (d < best) {
      best = d;
      bestName = ex.name;
    }
    const w = Math.exp(-(d * d) / (2 * sigma * sigma));
    wsum += w;
    psum += w * ex.psl;
  }
  // No pull toward average when this face is an example. The prior grows as the match gets worse.
  const priorW = 0.65 * (1 - Math.exp(-(best * best) / (2 * 0.28 * 0.28)));
  const local = (priorPsl * priorW + psum) / (priorW + (wsum || 1e-6));
  // Far from every example: sit on average (4), with only a small geometric nudge.
  const close = clamp(1 - Math.max(0, best - 0.35) / 1.1, 0, 1);
  const nudged = priorPsl + (geo - priorPsl) * 0.28;
  const score = clamp(local * close + nudged * (1 - close), 1, 7.99);
  return { score, nearest: bestName, distance: best, confidence: close, local };
}

function issue(id, domain, severity, title, detail, levers) {
  return { id, domain, severity, title, detail, levers: levers || [] };
}

/** Ranked, face-specific issues from measured factors + raw metrics. */
export function buildFaceProfile(metrics, factors, extras) {
  const m = metrics || {};
  const f = factors || {};
  const skin = extras && extras.skin ? extras.skin : { clarity: f.skin || 0.55 };
  // Always derive bone/eyes/hair from metrics when present so issue gates see .jaw/.aperture/etc.
  const hair = Object.keys(m).length ? analyzeHair(m) : extras && extras.hair
    ? extras.hair
    : { framing: f.hair || 0.55 };
  const eyes = Object.keys(m).length ? analyzeEyes(m) : extras && extras.eyes
    ? extras.eyes
    : { quality: f.eyes || 0.55 };
  const bone = Object.keys(m).length ? analyzeBone(m) : extras && extras.bone
    ? extras.bone
    : { structure: f.bone || 0.55 };
  const smile = (extras && extras.smile) || 0;
  const issues = [];

  if ((f.symmetry ?? m.symmetry ?? 1) < 0.72) {
    issues.push(
      issue(
        "symmetry",
        "recovery",
        1 - (f.symmetry ?? m.symmetry ?? 0.5),
        "Asymmetry cues are dragging the read",
        "Landmark left/right error sits at " +
          pct(1 - (m.symmetry || 0)) +
          "% relative load. Sleep debt, uneven light, and morning fluid exaggerate this more than bone.",
        ["fixed wake time 7 nights", "front fill light for every re-scan", "ease late salt/alcohol"]
      )
    );
  }
  if ((bone.jaw || 0) < 0.62) {
    issues.push(
      issue(
        "jaw",
        "bone",
        1 - bone.jaw,
        "Jaw-to-cheek ratio reads soft vs ideal",
        "Bigonial/bizygomatic nearness scores " +
          pct(bone.jaw) +
          "% of ideal (" +
          fmt(m.jawToCheek) +
          " vs ~" +
          IDEAL.jawToCheek.t +
          "). Controllable levers are body-composition and silhouette grooming — not contour gadgets.",
        ["full-body strength 3×/week", "slight calorie deficit if appropriate", "clean cheek/neck hairline"]
      )
    );
  }
  if ((bone.chin || 0) < 0.58) {
    issues.push(
      issue(
        "chin",
        "bone",
        1 - bone.chin,
        "Chin projection / lower-third balance is off ideal",
        "Chin depth cue scores " +
          pct(bone.chin) +
          "%. Posture (ear over shoulder, soft chin) and lower-face adiposity move the capture more than most topicals.",
        ["eye-level camera habit", "posture resets hourly", "lower-face definition via training"]
      )
    );
  }
  if ((bone.thirds || 0) < 0.6) {
    issues.push(
      issue(
        "thirds",
        "bone",
        1 - bone.thirds,
        "Facial thirds are uneven in this capture",
        "Vertical third balance is " +
          pct(bone.thirds) +
          "% of ideal (top " +
          fmt(m.thirdsTop) +
          " · mid " +
          fmt(m.thirdsMid) +
          " · low " +
          fmt(m.thirdsLow) +
          "). Hairline / bangs / beard edges can reframe without changing bone.",
        ["barber who understands face shape", "brow density tidy", "avoid chin-down phone posture"]
      )
    );
  }
  if ((eyes.quality || 0) < 0.64) {
    issues.push(
      issue(
        "eyes",
        "eyes",
        1 - eyes.quality,
        "Eye aperture / canthal / spacing underperform",
        "Eye composite " +
          pct(eyes.quality) +
          "% (aperture " +
          pct(eyes.aperture || 0) +
          " · canthal " +
          pct(eyes.canthal || 0) +
          " · spacing " +
          pct(eyes.spacing || 0) +
          "). Brow lift/set and sleep (less hooding/puff) are the main non-surgical moves.",
        ["brow gel + light trim", "7–9h sleep for less hooding", "front light at eye height"]
      )
    );
  }
  if ((skin.clarity || 0) < 0.62) {
    issues.push(
      issue(
        "skin",
        "skin",
        1 - skin.clarity,
        "Skin clarity / evenness is capping the rating",
        "On-device cheek ROI clarity " +
          pct(skin.clarity) +
          "% (evenness " +
          pct(skin.evenness || 0) +
          "). Homogeneous skin is a top cross-cultural attractiveness cue — SPF + barrier + one active beats new serums weekly.",
        ["SPF 30–50 every morning", "niacinamide 5%", "slow retinoid nights"]
      )
    );
  }
  if ((hair.framing || 0) < 0.6) {
    issues.push(
      issue(
        "hair",
        "hair",
        1 - hair.framing,
        "Hair / upper-face framing is weak for your proportions",
        "Framing score " +
          pct(hair.framing) +
          "% from forehead third + temple taper + brow span. A shape-aware cut usually beats another serum for perceived proportion.",
        ["face-shape haircut this week", "temple/side fade or volume as needed", "brow set daily"]
      )
    );
  }
  if (smile < 0.28) {
    issues.push(
      issue(
        "smile",
        "expression",
        0.55,
        "Expression / smile signal is underused",
        "Smile blendshape averaged " +
          pct(smile) +
          "%. Neutral locks under-rate even solid bone. A soft Duchenne smile is the fastest harmony lift.",
        ["mirror eye-smile reps before lock", "lip comfort balm", "enamel-safe whitening if needed"]
      )
    );
  }

  issues.sort((a, b) => b.severity - a.severity);
  // Always keep at least one maintain issue for strong faces
  if (!issues.length) {
    issues.push(
      issue(
        "maintain",
        "maintain",
        0.2,
        "Baseline is strong — protect it",
        "Bone/eyes/skin composites are in a favorable band. Consistency (SPF, sleep, monthly grooming) beats novelty chasing.",
        ["daily SPF you will reapply", "fixed sleep window", "monthly barber / brow pass"]
      )
    );
  }

  return {
    issues: issues.slice(0, 6),
    factors: {
      bone: bone.structure ?? f.bone,
      eyes: eyes.quality ?? f.eyes,
      skin: skin.clarity ?? f.skin,
      hair: hair.framing ?? f.hair,
      symmetry: f.symmetry ?? m.symmetry,
      expression: f.expression
    },
    metrics: {
      jawToCheek: m.jawToCheek,
      chinProj: m.chinProj,
      eyeAperture: m.eyeAperture,
      canthalLift: m.canthalLift,
      thirdsTop: m.thirdsTop,
      thirdsMid: m.thirdsMid,
      thirdsLow: m.thirdsLow,
      heightWidth: m.heightWidth,
      symmetry: m.symmetry,
      skinClarity: skin.clarity,
      hairFraming: hair.framing
    }
  };
}

function tipFromIssue(iss, tone) {
  const clinical = tone === "clinical";
  const productsByDomain = {
    recovery: [
      {
        name: "Fixed wake alarm + cool dark room",
        why: "Highest ROI for asymmetry-looking fluid and dull tone.",
        ingredients: "Habit — consistent circadian anchor"
      },
      {
        name: "Magnesium glycinate (clinician-okayed)",
        why: "Optional wind-down support for some people.",
        ingredients: "Magnesium glycinate"
      }
    ],
    bone: [
      {
        name: "Protein-forward meals / powder",
        why: "Supports training while cutting facial adiposity.",
        ingredients: "Whey or pea isolate; creatine 3–5g if training"
      },
      {
        name: "Edge trimmer + balm",
        why: "Clean jaw silhouette without changing bone.",
        ingredients: "Plant oils + vitamin E after trim"
      }
    ],
    eyes: [
      {
        name: "Brow gel / clear mascara",
        why: "Sets upper-third so eyes read more open.",
        ingredients: "Film-formers + panthenol"
      },
      {
        name: "Caffeine eye gel (AM, optional)",
        why: "Temporary de-puff before scans/photos.",
        ingredients: "Caffeine + glycerin / HA"
      }
    ],
    skin: [
      {
        name: "Mineral or hybrid SPF 30–50",
        why: "UV is the main preventable uneven-tone driver.",
        ingredients: "Zinc oxide and/or modern UV filters"
      },
      {
        name: "Niacinamide 5%",
        why: "Barrier + more even-looking tone for most.",
        ingredients: "Niacinamide 4–5%, glycerin"
      },
      {
        name: "Adapalene 0.1% (slow start)",
        why: "Evidence-backed texture / photoaging — 2–3 nights/week.",
        ingredients: "Adapalene 0.1%; buffer with moisturizer"
      }
    ],
    hair: [
      {
        name: "Shape-aware haircut",
        why: "Reframes thirds and jaw without surgery.",
        ingredients: "Barber consult — bring LOOK bars screenshot"
      },
      {
        name: "Brow gel",
        why: "Daily upper-third polish.",
        ingredients: "Fragrance-free film former"
      }
    ],
    expression: [
      {
        name: "Humectant lip balm",
        why: "Comfortable lips make a natural smile easier.",
        ingredients: "HA, glycerin, shea / squalane"
      },
      {
        name: "Enamel-safe whitening toothpaste",
        why: "Dental aesthetics move smile ratings.",
        ingredients: "Low-dose peroxide; skip charcoal abrasives"
      }
    ],
    maintain: [
      {
        name: "Daily SPF you will reapply",
        why: "Protects a strong baseline.",
        ingredients: "Texture you actually wear"
      },
      {
        name: "Azelaic acid 10% (optional)",
        why: "Redness / tone for many with low irritation.",
        ingredients: "Azelaic acid 10%"
      }
    ]
  };

  return {
    id: iss.id + "-" + Math.round(iss.severity * 100),
    title: iss.title,
    body: clinical
      ? iss.detail + " Priority levers: " + iss.levers.join("; ") + "."
      : iss.detail + " Start with: " + iss.levers.slice(0, 2).join(" · ") + ".",
    cite: "LOOK advisor · on-device " + iss.domain + " analysis",
    domain: iss.domain,
    severity: iss.severity,
    products: productsByDomain[iss.domain] || productsByDomain.maintain
  };
}

const DAY_NAMES = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

/**
 * Build a unique plan + 7-day schedule from this face’s ranked issues.
 * No shared canned tip order — schedule maps to the individual’s deficits.
 */
export function buildPersonalPlan(result, profile, settings) {
  const tone = settings && settings.planTone === "clinical" ? "clinical" : "coach";
  const score = result.score;
  const sym = result.sym;
  const prop = result.prop;
  const harm = result.harm;
  const smile = result.smile || 0;
  const tier = result.tier;
  const tierLabel = result.tierLabel;
  const issues = (profile && profile.issues) || [];
  const tips = issues.slice(0, 4).map((iss) => tipFromIssue(iss, tone));

  const factors = (profile && profile.factors) || {};
  const focuses = [
    { label: "Bone", weak: (factors.bone || 1) < 0.62 },
    { label: "Eyes", weak: (factors.eyes || 1) < 0.64 },
    { label: "Skin", weak: (factors.skin || 1) < 0.62 },
    { label: "Hair", weak: (factors.hair || 1) < 0.6 },
    { label: "Symmetry", weak: (factors.symmetry || 1) < 0.72 },
    { label: "Expression", weak: smile < 0.28 }
  ];

  const metrics = (profile && profile.metrics) || {};
  const days = writeMetricSchedule(issues, metrics, fPreview(profile), smile, tips);

  const routine = issues.slice(0, 5).map((iss) => ({
    title: iss.title,
    detail: iss.detail + " → " + iss.levers.slice(0, 2).join("; ") + "."
  }));

  const f = factors;
  const narrative =
    "Locked <strong>" +
    tierLabel +
    "</strong> (PSL " +
    fmt(score, 1) +
    "). This plan was synthesized from your on-device read — bone " +
    pct(f.bone || 0) +
    "%, eyes " +
    pct(f.eyes || 0) +
    "%, skin " +
    pct(f.skin || 0) +
    "%, hair framing " +
    pct(f.hair || 0) +
    "%, symmetry " +
    pct(f.symmetry || 0) +
    "%. Top constraint: <strong>" +
    (issues[0] ? issues[0].title : "maintenance") +
    "</strong>.";

  const summary =
    tone === "clinical"
      ? "PSL <strong>" +
        tierLabel +
        "</strong>. Interventions ranked by measured deficits (skin ROI, bone ratios, eye composite, hair framing)."
      : "You’re <strong>" +
        tierLabel +
        "</strong> on the 1–8 scale. Tips and the 7-day schedule below are unique to this scan’s weak domains — not a generic list.";

  const weakSkin = (f.skin || 1) < 0.62;
  const weakBone = (f.bone || 1) < 0.62;
  const weakEyes = (f.eyes || 1) < 0.64;
  const weakSym = (f.symmetry || 1) < 0.72;
  const fast =
    (smile < 0.3 ? 0.4 : 0.12) + (weakSym ? 0.22 : 0.06) + (weakEyes ? 0.12 : 0.04);
  const mid = (weakSkin ? 0.35 : 0.14) + (score < 5 ? 0.12 : 0.05);
  const slow = weakBone ? 0.5 : 0.18;
  const totalLift = Math.min(1.55, Math.max(0.25, fast + mid + slow) * (score >= 6.6 ? 0.5 : 1));
  const target = clamp(score + totalLift, score, 7.99);

  return {
    score,
    sym,
    prop,
    harm,
    smile,
    label: tierLabel,
    tier,
    tierLabel,
    focuses,
    tips,
    routine,
    days,
    narrative,
    summary,
    profile,
    factors: f,
    lift: { fast, mid, slow, total: totalLift, target },
    at: Date.now(),
    advisor: "look-metrics-v2",
    scheduleVersion: "v30"
  };
}

function fPreview(profile) {
  return (profile && profile.factors) || {};
}

function signature(metrics, factors) {
  const m = metrics || {};
  const f = factors || {};
  const bits = [];
  if (f.bone != null) bits.push("bone " + pct(f.bone) + "%");
  if (f.eyes != null) bits.push("eyes " + pct(f.eyes) + "%");
  if (f.skin != null) bits.push("skin " + pct(f.skin) + "%");
  if (f.hair != null) bits.push("hair " + pct(f.hair) + "%");
  if (f.symmetry != null) bits.push("symmetry " + pct(f.symmetry) + "%");
  if (m.jawToCheek != null) bits.push("jaw/cheek " + fmt(m.jawToCheek));
  if (m.chinProj != null) bits.push("chin z " + fmt(m.chinProj));
  if (m.eyeAperture != null) bits.push("eye aperture " + fmt(m.eyeAperture));
  if (m.canthalLift != null) bits.push("canthal " + fmt(m.canthalLift, 3));
  if (m.heightWidth != null) bits.push("H/W " + fmt(m.heightWidth));
  if (m.skinClarity != null) bits.push("clarity " + pct(m.skinClarity) + "%");
  return bits.join(" · ");
}

function productsFor(domain) {
  const map = {
    recovery: [
      { name: "Fixed wake time", why: "Cuts asymmetric morning fluid.", ingredients: "Habit" },
      { name: "Front fill light", why: "Stops lamp-side false asymmetry.", ingredients: "3000–4000K, CRI 90+ if listed" }
    ],
    bone: [
      { name: "Protein at each meal", why: "Supports training while facial fat drops.", ingredients: "Whey or pea isolate" },
      { name: "Neckline trimmer", why: "Sharpens the jaw you measured.", ingredients: "Balm after trim" }
    ],
    eyes: [
      { name: "Brow gel", why: "Opens the upper third around your aperture.", ingredients: "Panthenol, fragrance-free" },
      { name: "Caffeine eye gel", why: "Short de-puff before a re-scan.", ingredients: "Caffeine + glycerin" }
    ],
    skin: [
      { name: "SPF 30–50", why: "Protects the clarity ROI from this scan.", ingredients: "Zinc oxide and/or modern UV filters" },
      { name: "Niacinamide 5%", why: "Evenness without stacking actives.", ingredients: "Niacinamide 4–5%" }
    ],
    hair: [
      { name: "Shape-aware cut", why: "Reframes your measured thirds.", ingredients: "Barber — bring these ratios" },
      { name: "Brow set", why: "Daily upper-face frame.", ingredients: "Clear film former" }
    ],
    expression: [
      { name: "Humectant lip balm", why: "Makes the smile reps comfortable.", ingredients: "Glycerin, HA, squalane" }
    ],
    maintain: [
      { name: "SPF you will reapply", why: "Holds a strong baseline.", ingredients: "Texture you actually wear" }
    ]
  };
  return map[domain] || map.maintain;
}

/**
 * Seven different jobs. A day never repeats another day's task.
 * The wording still uses this scan's weakest areas and numbers.
 */
export function writeMetricSchedule(issues, metrics, factors, smile, tips) {
  const ranked = (issues && issues.length ? issues : []).slice();
  const m = metrics || {};
  const f = factors || {};
  const smilePct = pct(smile || 0);
  const jaw = fmt(m.jawToCheek);
  const chin = fmt(m.chinProj);
  const aperture = fmt(m.eyeAperture);
  const canthal = fmt(m.canthalLift, 3);
  const top = fmt(m.thirdsTop);
  const skinPct = pct(m.skinClarity != null ? m.skinClarity : f.skin || 0);
  const bonePct = pct(f.bone || 0);
  const eyePct = pct(f.eyes || 0);
  const hairPct = pct(f.hair || 0);
  const symPct = pct(f.symmetry || m.symmetry || 0);

  function iss(i) {
    return ranked[i] || { title: "Hold the baseline", domain: "maintain", detail: "No large deficit on this scan.", levers: ["SPF", "sleep"], severity: 0.2 };
  }
  function lever(issue, n) {
    const list = issue.levers && issue.levers.length ? issue.levers : [issue.title];
    return list[n % list.length];
  }
  function prods(domain) {
    const tip = (tips || []).find((t) => t.domain === domain);
    return ((tip && tip.products) || productsFor(domain) || []).slice(0, 2);
  }

  const a = iss(0);
  const b = iss(1);
  const c = iss(2);

  const specs = [
    {
      name: "Monday",
      title: "Monday — " + a.title,
      focus: "Only today's job: " + lever(a, 0),
      domain: a.domain,
      am: "Skip the rest of the list. Do this once before noon: " + lever(a, 0) + ".",
      pm: "Nothing else from the plan tonight. Lights out on time so this change can show up (" + a.title.toLowerCase() + ").",
      habit: lever(a, 0) + ". That is the whole Monday.",
      why: a.detail + " Monday is only this lever, not the whole routine.",
      products: prods(a.domain)
    },
    {
      name: "Tuesday",
      title: "Tuesday — eyes and brows",
      focus: "Brow set and a shorter screen night. Aperture " + aperture + ", canthal " + canthal + ".",
      domain: "eyes",
      am: "Cool splash, then brow gel only. Do not repeat Monday's task (" + lever(a, 0) + ").",
      pm: "Screens off 45 minutes earlier than last night. Eye score on this scan is " + eyePct + "%.",
      habit: "One brow tidy. No second workout.",
      why: "Tuesday is the eye day. " + (b.domain === "eyes" ? b.detail : "Canthal " + canthal + " and aperture " + aperture + " are what you are training."),
      products: prods("eyes")
    },
    {
      name: "Wednesday",
      title: "Wednesday — jaw and steps",
      focus: "One strength session or a long walk. Jaw/cheek " + jaw + ", chin z " + chin + ".",
      domain: "bone",
      am: "Eye-level phone only. No chin-down scrolling. Bone on this scan is " + bonePct + "%.",
      pm: "Protein with dinner. Moisturizer only — no new serum.",
      habit: bonePct < 70 ? "Full-body strength or 8–10k steps. Not yesterday's brow task." : "A 40-minute walk at eye level. Training stays light because bone is already " + bonePct + "%.",
      why: "Wednesday moves the jaw and posture numbers (" + jaw + " / " + chin + "). It is not a repeat of " + a.title + ".",
      products: prods("bone")
    },
    {
      name: "Thursday",
      title: "Thursday — skin only",
      focus: "SPF plus one active, or none if clarity is already " + skinPct + "%.",
      domain: "skin",
      am: "This is the only morning with a full SPF step. Clarity " + skinPct + "%.",
      pm: skinPct < 65 ? "Buffered retinoid or niacinamide on two spots. No acids stacked on top." : "Barrier cream only. Skin is " + skinPct + "% — do not add a new active.",
      habit: "One skin step. Leave brows, hair, and training alone.",
      why: "Thursday is skin. The other days do not repeat this product list.",
      products: prods("skin")
    },
    {
      name: "Friday",
      title: "Friday — hair and frame",
      focus: "Hair and upper third. Hair framing " + hairPct + "%, forehead third " + top + ".",
      domain: "hair",
      am: "Style for the thirds you measured. " + (hairPct < 70 ? "Book or do the cut today." : "Keep the cut you have; set the hairline once."),
      pm: "No new products. Check a photo: did hair hide or expose the forehead?",
      habit: hairPct < 70 ? "Barber or a deliberate restyle. Bring the scan numbers." : "Five-minute style check only. Hair is already " + hairPct + "%.",
      why: "Friday is framing, not skin and not the gym. " + (c.domain === "hair" ? c.detail : "Upper-third " + top + "."),
      products: prods("hair")
    },
    {
      name: "Saturday",
      title: "Saturday — one real photo",
      focus: "Expression practice. Smile signal " + smilePct + "%.",
      domain: "expression",
      am: "Lip balm, then one soft eye-smile in the mirror. Stop after one rep.",
      pm: "Take a single photo at eye level in the same light as your scan. Do not reshoot a set.",
      habit: "One photo, one smile. Symmetry on the scan was " + symPct + "%.",
      why: "Saturday is expression and a reference photo. It is not Monday's lever and not Thursday's products.",
      products: prods("expression")
    },
    {
      name: "Sunday",
      title: "Sunday — re-scan only",
      focus: "Measure. Do not add a new habit.",
      domain: "maintain",
      am: "Same room, eye-level camera, soft front light. Re-scan once.",
      pm: "Early night. No acids, no extra workout, no new product.",
      habit: "Compare bone " + bonePct + "%, eyes " + eyePct + "%, skin " + skinPct + "% to last week. Then stop.",
      why: "Sunday is the measurement day. The work happened Monday through Saturday.",
      products: prods("maintain")
    }
  ];

  const habits = {};
  return specs.map((d) => {
    if (habits[d.habit]) d.habit = d.name + ": " + d.habit;
    habits[d.habit] = true;
    return d;
  });
}

async function onDeviceModel() {
  const LM =
    globalThis.LanguageModel ||
    (globalThis.ai && (globalThis.ai.languageModel || globalThis.ai.assistant));
  if (!LM || typeof LM.create !== "function") return null;
  try {
    if (typeof LM.availability === "function") {
      const avail = await LM.availability();
      if (avail === "unavailable" || avail === "no" || avail === "downloadable") return null;
    }
    return await LM.create({ temperature: 0.85, topK: 12 });
  } catch (_) {
    return null;
  }
}

function schedulePrompt(plan) {
  return (
    "You write a 7-day LOOK schedule for ONE face. It must be obviously different from any other face. " +
    "Every day must cite at least one number from the measurements (bone %, jaw/cheek, eye aperture, skin clarity, etc). " +
    "No surgery. No medical diagnosis. No generic 'cleanse moisturizer SPF' copied across days. " +
    "Each day must be a different job. Monday is the top deficit only, Tuesday eyes, Wednesday jaw or steps, Thursday skin, Friday hair, Saturday one photo, Sunday a re-scan. Do not repeat the same AM, PM, or habit on two days. " +
    "Return ONLY JSON: {days:[{name,title,focus,am,pm,habit,why,products:[{name,why,ingredients}]}], summary}. " +
    "names must be Monday through Sunday in order.\n\nMEASUREMENTS:\n" +
    JSON.stringify(
      {
        score: plan.score,
        tier: plan.tierLabel,
        factors: plan.factors,
        smile: plan.smile,
        issues: ((plan.profile && plan.profile.issues) || []).map((i) => ({
          title: i.title,
          domain: i.domain,
          severity: i.severity,
          detail: i.detail,
          levers: i.levers
        })),
        metrics: (plan.profile && plan.profile.metrics) || {}
      },
      null,
      2
    )
  );
}

function parseScheduleJson(text) {
  if (!text) return null;
  let parsed;
  try {
    parsed = JSON.parse(text);
  } catch (_) {
    const m = String(text).match(/\{[\s\S]*\}/);
    if (!m) return null;
    try {
      parsed = JSON.parse(m[0]);
    } catch (e2) {
      return null;
    }
  }
  const days = parsed && parsed.days;
  if (!Array.isArray(days) || days.length < 7) return null;
  const names = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
  const normalized = names.map((name, i) => {
    const d = days[i] || {};
    return {
      name,
      title: d.title || name,
      focus: d.focus || d.title || "",
      domain: d.domain || "ai",
      am: d.am || "",
      pm: d.pm || "",
      habit: d.habit || "",
      why: d.why || "",
      products: Array.isArray(d.products) ? d.products.slice(0, 3) : []
    };
  });
  if (normalized.some((d) => !d.am || !d.habit)) return null;
  return { days: normalized, summary: parsed.summary || "" };
}

/**
 * Rewrite the 7-day schedule with on-device AI, then Gemini if a key is set.
 * Returns the plan unchanged when no model is available.
 */
export async function personalizeScheduleWithAI(plan, apiKey) {
  if (!plan) return plan;
  const prompt = schedulePrompt(plan);
  const local = await onDeviceModel();
  if (local) {
    try {
      const raw = local.prompt ? await local.prompt(prompt) : await local.generate(prompt);
      const text = typeof raw === "string" ? raw : (raw && (raw.text || raw.output)) || "";
      const parsed = parseScheduleJson(text);
      if (parsed) {
        if (local.destroy) local.destroy();
        return {
          ...plan,
          days: parsed.days,
          summary: parsed.summary || plan.summary,
          advisor: "on-device-ai"
        };
      }
    } catch (_) {
      /* fall through */
    }
    try {
      if (local.destroy) local.destroy();
    } catch (_) {}
  }
  return enhancePlanWithAI(plan, apiKey);
}

/** Gemini rewrite of tips + schedule when a key is set. */
export async function enhancePlanWithAI(plan, apiKey, model) {
  const key = (apiKey || "").trim();
  if (!key || !plan) return plan;
  const useModel = model || "gemini-2.0-flash";
  const payload = {
    contents: [
      {
        role: "user",
        parts: [
          {
            text:
              "You are LOOK, an on-device face aesthetics coach. Rewrite tips and a 7-day schedule for THIS face only. " +
              "Be specific to the measured deficits. No surgery. No claims of medical diagnosis. Keep PSL framing. " +
              "Return ONLY JSON: { tips:[{title,body,cite,products:[{name,why,ingredients}]}], days:[{name,title,focus,am,pm,habit,products:[{name,why,ingredients}],why}], summary, narrative }.\n\nPROFILE:\n" +
              JSON.stringify(
                {
                  score: plan.score,
                  tier: plan.tierLabel,
                  factors: plan.factors,
                  issues: (plan.profile && plan.profile.issues) || [],
                  metrics: (plan.profile && plan.profile.metrics) || {}
                },
                null,
                2
              )
          }
        ]
      }
    ],
    generationConfig: { temperature: 0.7, responseMimeType: "application/json" }
  };

  const url =
    "https://generativelanguage.googleapis.com/v1beta/models/" +
    encodeURIComponent(useModel) +
    ":generateContent?key=" +
    encodeURIComponent(key);
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!res.ok) {
    const errText = await res.text().catch(() => "");
    throw new Error("AI enhance failed (" + res.status + "). " + errText.slice(0, 160));
  }
  const data = await res.json();
  const text =
    (((data || {}).candidates || [])[0] || {}).content &&
    (((data.candidates[0].content.parts || [])[0] || {}).text || "");
  if (!text) throw new Error("AI returned empty content.");
  let parsed;
  try {
    parsed = JSON.parse(text);
  } catch (_) {
    const m = text.match(/\{[\s\S]*\}/);
    if (!m) throw new Error("AI returned non-JSON.");
    parsed = JSON.parse(m[0]);
  }
  return {
    ...plan,
    tips: Array.isArray(parsed.tips) && parsed.tips.length ? parsed.tips : plan.tips,
    days: Array.isArray(parsed.days) && parsed.days.length === 7 ? parsed.days : plan.days,
    summary: parsed.summary || plan.summary,
    narrative: parsed.narrative || plan.narrative,
    advisor: "gemini:" + useModel
  };
}

export { IDEAL };

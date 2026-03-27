import { randomUUID } from "node:crypto";

function nowIso() {
  return new Date().toISOString();
}

function makeJoinCode() {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let code = "";
  for (let i = 0; i < 6; i += 1) code += alphabet[Math.floor(Math.random() * alphabet.length)];
  return code;
}

function clampInt(value, min, max) {
  const asNumber = Number(value);
  if (!Number.isFinite(asNumber)) return min;
  return Math.max(min, Math.min(max, Math.trunc(asNumber)));
}

class InMemoryStore {
  constructor() {
    this.sessions = new Map();
    this.profiles = new Map();
  }

  createProfile({ name }) {
    const id = randomUUID();
    const profile = { id, name, xp: 0, gold: 0, updatedAt: nowIso() };
    this.profiles.set(id, profile);
    return profile;
  }

  getProfile(playerId) {
    return this.profiles.get(playerId) ?? null;
  }

  updateProgression(playerId, { xpDelta, goldDelta }) {
    const profile = this.profiles.get(playerId);
    if (!profile) return null;

    profile.xp = clampInt(profile.xp + xpDelta, 0, 2_000_000_000);
    profile.gold = clampInt(profile.gold + goldDelta, 0, 2_000_000_000);
    profile.updatedAt = nowIso();

    return profile;
  }

  createSession({ hostName }) {
    const id = randomUUID();
    const joinCode = makeJoinCode();

    const hostProfile = this.createProfile({ name: hostName });
    const session = {
      id,
      joinCode,
      createdAt: nowIso(),
      players: [{ playerId: hostProfile.id, name: hostProfile.name }]
    };

    this.sessions.set(id, session);
    return session;
  }

  getSession(id) {
    return this.sessions.get(id) ?? null;
  }

  joinSession(id, { playerName }) {
    const session = this.sessions.get(id);
    if (!session) return null;

    if (session.players.length >= 2) {
      return {
        ...session,
        error: "SESSION_FULL"
      };
    }

    const profile = this.createProfile({ name: playerName });
    session.players.push({ playerId: profile.id, name: profile.name });

    return session;
  }
}

export const store = new InMemoryStore();


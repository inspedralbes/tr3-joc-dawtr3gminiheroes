import express from "express";
import cors from "cors";
import morgan from "morgan";
import { store } from "./store.js";

const app = express();

app.use(cors());
app.use(express.json({ limit: "256kb" }));
app.use(morgan("dev"));

app.get("/health", (_req, res) => {
  res.json({ ok: true });
});

app.post("/sessions", (req, res) => {
  const { hostName } = req.body ?? {};
  const session = store.createSession({ hostName: hostName ?? "Host" });
  res.status(201).json(session);
});

app.post("/sessions/:id/join", (req, res) => {
  const { id } = req.params;
  const { playerName } = req.body ?? {};

  const result = store.joinSession(id, { playerName: playerName ?? "Player" });
  if (!result) return res.status(404).json({ error: "SESSION_NOT_FOUND" });

  return res.status(200).json(result);
});

app.get("/sessions/:id", (req, res) => {
  const session = store.getSession(req.params.id);
  if (!session) return res.status(404).json({ error: "SESSION_NOT_FOUND" });
  return res.status(200).json(session);
});

app.get("/profiles/:playerId", (req, res) => {
  const profile = store.getProfile(req.params.playerId);
  if (!profile) return res.status(404).json({ error: "PROFILE_NOT_FOUND" });
  return res.status(200).json(profile);
});

app.post("/profiles/:playerId/progression", (req, res) => {
  const { playerId } = req.params;
  const { xpDelta, goldDelta } = req.body ?? {};

  const profile = store.updateProgression(playerId, {
    xpDelta: Number.isFinite(xpDelta) ? xpDelta : 0,
    goldDelta: Number.isFinite(goldDelta) ? goldDelta : 0
  });

  if (!profile) return res.status(404).json({ error: "PROFILE_NOT_FOUND" });
  return res.status(200).json(profile);
});

const port = Number(process.env.PORT ?? 3000);
app.listen(port, () => {
  // eslint-disable-next-line no-console
  console.log(`MiniHeroes2D backend listening on :${port}`);
});

